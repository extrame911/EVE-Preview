using System.Windows.Threading;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime
{
    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        var lastPoll = DateTime.MinValue;
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            var interval = TimeSpan.FromMilliseconds(
                Math.Clamp(_settings.Monitoring.PollIntervalMs, 100, 2000));
            if (_paused || DateTime.UtcNow - lastPoll < interval)
            {
                continue;
            }

            lastPoll = DateTime.UtcNow;
            try
            {
                await PollOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                await ReportErrorAsync($"进程监控轮次失败：{exception.Message}", cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        if (_paused || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        IReadOnlyList<ProcessSnapshot> rawSnapshots;
        try
        {
            rawSnapshots = await Task.Run(
                () => _processMonitor.GetSnapshot(_settings.Monitoring.ProcessNames),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await ReportErrorAsync($"读取客户端进程失败：{exception.Message}", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _dispatcher.InvokeAsync(
            () => ApplySnapshots(rawSnapshots),
            DispatcherPriority.Background,
            cancellationToken);
    }

    private void ApplySnapshots(IReadOnlyList<ProcessSnapshot> rawSnapshots)
    {
        if (_paused)
        {
            return;
        }

        var current = CreateCurrentSnapshots(rawSnapshots);
        var previous = _previews.Keys
            .Select(identity => new ClientSnapshot(identity, _previews[identity].Client.MainWindowTitle))
            .ToArray();
        var diff = _differ.Compare(previous, current);
        foreach (var removed in diff.Removed)
        {
            RemovePreview(removed.Identity);
        }

        foreach (var added in diff.Added)
        {
            AddPreview(added, rawSnapshots);
        }

        foreach (var updated in diff.Updated)
        {
            UpdatePreviewTitle(updated);
        }

        if (diff.Added.Count > 0 || diff.Removed.Count > 0)
        {
            RebuildHotkeys();
        }

        var foreground = _windowQueue?.GetForegroundWindow() ?? 0;
        UpdateFocusedClientFromForeground(foreground);
        UpdateActiveClientFromWindowState();
        if (_settings.General.MinimizeInactiveClients && _activeIdentity is not null)
        {
            _ = MinimizeInactiveClientsAsync(_activeIdentity);
        }

        PublishSnapshot(null);
    }

    private static ClientSnapshot[] CreateCurrentSnapshots(IReadOnlyList<ProcessSnapshot> rawSnapshots) =>
        rawSnapshots
            .Select(snapshot => new ClientSnapshot(
                new ClientIdentity(
                    snapshot.ProcessId,
                    snapshot.MainWindowHandle,
                    snapshot.ProcessStartTime.ToUniversalTime()),
                snapshot.MainWindowTitle))
            .ToArray();

    private void RemovePreview(ClientIdentity identity)
    {
        _defaultPreviewSlots.Remove(identity);
        if (_previews.Remove(identity, out var preview))
        {
            try
            {
                preview.Dispose();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                _lastError = $"关闭客户端预览失败：{exception.Message}";
            }
        }

        if (_activeIdentity == identity)
        {
            _activeIdentity = null;
        }

        if (_focusedIdentity != identity)
        {
            return;
        }

        _focusedIdentity = null;
        foreach (var item in _previews.Values)
        {
            item.SetFocused(false);
        }
    }

    private void AddPreview(ClientSnapshot added, IReadOnlyList<ProcessSnapshot> rawSnapshots)
    {
        var process = rawSnapshots.First(snapshot =>
            snapshot.ProcessId == added.Identity.ProcessId &&
            snapshot.MainWindowHandle == added.Identity.MainWindowHandle);
        CreatePreview(process);
        ApplySavedClientWindowLayoutOnLogin(added.Identity);
    }

    private void UpdatePreviewTitle(ClientSnapshot updated)
    {
        if (!_previews.TryGetValue(updated.Identity, out var preview))
        {
            return;
        }

        var previousTitle = preview.Client.MainWindowTitle;
        preview.UpdateWindowTitle(updated.Title);
        if (!string.Equals(previousTitle, updated.Title, StringComparison.OrdinalIgnoreCase))
        {
            MigrateClientTitle(previousTitle, updated.Title);
            ApplyPreviewClientSettings(preview);
            if (ClientWindowTitle.IsLoginWindow(previousTitle))
            {
                ApplySavedClientWindowLayoutOnLogin(updated.Identity);
            }
        }
    }

    private void ApplyPreviewClientSettings(PreviewWindow preview)
    {
        var clientSettings = FindClientSettings(preview.Client.MainWindowTitle);
        var alias = string.IsNullOrWhiteSpace(clientSettings?.Alias) ? null : clientSettings.Alias;
        preview.UpdateDisplayName(alias ?? preview.Client.MainWindowTitle, alias);
        if (clientSettings?.Placement is { } stored)
        {
            preview.ApplyPlacement(new PreviewPlacement(
                stored.Left,
                stored.Top,
                preview.Width,
                preview.Height));
        }
    }

    /// <summary>
    /// Focus is the EVE client activated from the desktop. When the desktop, EVE-Preview, or
    /// another application is foreground, no client is focused; both hide options use this flag
    /// with inverse results.
    /// </summary>
    private void UpdateFocusedClientFromForeground(nint foreground)
    {
        if (foreground == 0)
        {
            // A transient failure to read the foreground window must not clear a valid state.
            return;
        }

        var focused = _previews.Values.FirstOrDefault(preview =>
            preview.Client.MainWindowHandle == foreground);
        var nextIdentity = focused?.Identity;
        if (_focusedIdentity == nextIdentity)
        {
            return;
        }

        _focusedIdentity = nextIdentity;
        foreach (var preview in _previews.Values)
        {
            preview.SetFocused(preview.Identity == _focusedIdentity);
        }
    }

    /// <summary>
    /// Activity follows the client window's shown/minimized state, not desktop focus. The current
    /// active client stays active while it remains visible; if it is minimized, another visible
    /// client takes over. No visible client means no active client.
    /// </summary>
    private void UpdateActiveClientFromWindowState()
    {
        if (_windowQueue is null)
        {
            return;
        }

        var visible = _previews.Values
            .Where(preview => !_windowQueue.IsMinimized(preview.Client.MainWindowHandle))
            .ToArray();
        if (visible.Length == 0)
        {
            SetActiveClient(null);
            return;
        }

        if (_activeIdentity is not null &&
            visible.Any(preview => preview.Identity == _activeIdentity))
        {
            return;
        }

        var foreground = _windowQueue.GetForegroundWindow();
        var foregroundClient = visible.FirstOrDefault(preview =>
            preview.Client.MainWindowHandle == foreground);
        SetActiveClient((foregroundClient ?? visible[0]).Identity);
    }

    private void SetActiveClient(ClientIdentity? identity)
    {
        if (_activeIdentity == identity)
        {
            return;
        }

        _activeIdentity = identity;
        foreach (var preview in _previews.Values)
        {
            preview.ApplySettings(_settings, preview.Identity == _activeIdentity);
        }
    }
}
