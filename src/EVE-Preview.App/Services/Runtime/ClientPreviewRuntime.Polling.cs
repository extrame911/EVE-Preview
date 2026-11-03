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
            if (DateTime.UtcNow - lastPoll < interval)
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
        if (cancellationToken.IsCancellationRequested)
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
        var current = CreateCurrentSnapshots(rawSnapshots);
        var previous = _knownClients.Values
            .Select(process => new ClientSnapshot(CreateIdentity(process), process.MainWindowTitle))
            .ToArray();
        var diff = _differ.Compare(previous, current);
        foreach (var removed in diff.Removed)
        {
            _knownClients.Remove(removed.Identity);
            RemovePreview(removed.Identity);
        }

        foreach (var added in diff.Added)
        {
            var process = FindProcess(rawSnapshots, added.Identity);
            _knownClients[added.Identity] = process;
            if (_previewsEnabled)
            {
                CreatePreview(process);
            }

            ApplySavedClientWindowLayoutOnLogin(added.Identity);
        }

        foreach (var updated in diff.Updated)
        {
            var process = FindProcess(rawSnapshots, updated.Identity);
            var previousTitle = _knownClients.TryGetValue(updated.Identity, out var existing)
                ? existing.MainWindowTitle
                : updated.Title;
            _knownClients[updated.Identity] = process;
            UpdateClientTitle(updated.Identity, previousTitle, updated.Title);
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

    private static ClientIdentity CreateIdentity(ProcessSnapshot snapshot) =>
        new(snapshot.ProcessId, snapshot.MainWindowHandle, snapshot.ProcessStartTime.ToUniversalTime());

    private static ProcessSnapshot FindProcess(
        IReadOnlyList<ProcessSnapshot> snapshots,
        ClientIdentity identity) =>
        snapshots.First(snapshot =>
            snapshot.ProcessId == identity.ProcessId &&
            snapshot.MainWindowHandle == identity.MainWindowHandle);

    private static ClientSnapshot[] CreateCurrentSnapshots(IReadOnlyList<ProcessSnapshot> rawSnapshots) =>
        rawSnapshots
            .Select(snapshot => new ClientSnapshot(
                CreateIdentity(snapshot),
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

    private void UpdateClientTitle(ClientIdentity identity, string previousTitle, string currentTitle)
    {
        if (_previews.TryGetValue(identity, out var preview))
        {
            preview.UpdateWindowTitle(currentTitle);
            ApplyPreviewClientSettings(preview);
        }

        if (!string.Equals(previousTitle, currentTitle, StringComparison.OrdinalIgnoreCase))
        {
            MigrateClientTitle(previousTitle, currentTitle);
            if (ClientWindowTitle.IsLoginWindow(previousTitle))
            {
                ApplySavedClientWindowLayoutOnLogin(identity);
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
                preview.Placement.Width,
                preview.Placement.Height));
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

        var focused = _knownClients.Values.FirstOrDefault(process =>
            process.MainWindowHandle == foreground);
        var nextIdentity = focused is null ? null : CreateIdentity(focused);
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

        var visible = _knownClients.Values
            .Where(process => !_windowQueue.IsMinimized(process.MainWindowHandle))
            .ToArray();
        if (visible.Length == 0)
        {
            SetActiveClient(null);
            return;
        }

        if (_activeIdentity is not null &&
            visible.Any(process => CreateIdentity(process) == _activeIdentity))
        {
            return;
        }

        var foreground = _windowQueue.GetForegroundWindow();
        var foregroundClient = visible.FirstOrDefault(process =>
            process.MainWindowHandle == foreground);
        SetActiveClient(CreateIdentity(foregroundClient ?? visible[0]));
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
