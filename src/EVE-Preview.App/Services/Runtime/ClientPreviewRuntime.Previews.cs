using System.Windows;
using System.Windows.Threading;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime
{
    private readonly Dictionary<ClientIdentity, int> _defaultPreviewSlots = [];

    public async Task ActivateClientAsync(
        ClientIdentity identity,
        CancellationToken cancellationToken = default)
    {
        await _dispatcher.InvokeAsync(
            () => ActivateClientCoreAsync(identity, cancellationToken),
            DispatcherPriority.Normal,
            cancellationToken).Task.Unwrap().ConfigureAwait(false);
    }
    private void CreatePreview(ProcessSnapshot process)
    {
        PreviewWindow? preview = null;
        try
        {
            var clientSettings = FindClientSettings(process.MainWindowTitle);
            var displayName = string.IsNullOrWhiteSpace(clientSettings?.Alias)
                ? process.MainWindowTitle
                : clientSettings.Alias;
            var storedPlacement = clientSettings?.Placement;
            var (defaultWidth, defaultHeight) = GetDefaultPreviewSize();
            int? defaultSlot = storedPlacement is null
                ? PreviewPlacementLayout.FindFirstFreeSlot(_defaultPreviewSlots.Values)
                : null;
            var primaryDpi = PreviewMonitorDpi.GetPrimaryScale();
            var placement = storedPlacement is { } stored
                ? new PreviewPlacement(stored.Left, stored.Top, defaultWidth, defaultHeight)
                : PreviewPlacementLayout.CreateDefault(
                    SystemParameters.WorkArea,
                    defaultSlot!.Value,
                    defaultWidth,
                    defaultHeight,
                    primaryDpi.X,
                    primaryDpi.Y);
            preview = new PreviewWindow(
                process,
                displayName,
                _settings,
                placement,
                OnPreviewAction,
                OnPreviewPlacementChanged,
                useDefaultPhysicalSize: true);
            preview.ApplyPlacement(placement);
            preview.ShowPreview();
            if (!preview.TryInitialize(out var error))
            {
                preview.Dispose();
                preview = null;
                if (!string.IsNullOrWhiteSpace(error))
                {
                    ShowError(error);
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                ShowError(error);
            }

            _previews[preview.Identity] = preview;
            preview.ApplySettings(_settings, preview.Identity == _activeIdentity);
            if (defaultSlot is { } slot)
            {
                _defaultPreviewSlots[preview.Identity] = slot;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (preview is not null)
            {
                _previews.Remove(preview.Identity);
                _defaultPreviewSlots.Remove(preview.Identity);
            }

            try
            {
                preview?.Dispose();
            }
            catch (Exception cleanupException) when (cleanupException is not OutOfMemoryException)
            {
            }

            ShowError($"创建客户端预览失败：{exception.Message}");
        }
    }

    private void OnPreviewAction(PreviewWindow preview, PreviewClickAction action)
    {
        _ = HandlePreviewActionAsync(preview, action);
    }

    private async Task HandlePreviewActionAsync(PreviewWindow preview, PreviewClickAction action)
    {
        try
        {
            switch (action)
            {
                case PreviewClickAction.Activate:
                    await ActivateClientCoreAsync(preview.Identity, CancellationToken.None);
                    break;
                case PreviewClickAction.Minimize:
                    await EnqueueAndReportAsync(
                        new WindowOperationRequest(
                            WindowOperationKind.Minimize,
                            preview.Client.MainWindowHandle,
                            ExpectedProcessId: preview.Client.ProcessId),
                        CancellationToken.None);
                    break;
                case PreviewClickAction.ToggleCycleExclusion:
                    await ToggleCycleExclusionAsync(preview.Client.MainWindowTitle);
                    break;
                case PreviewClickAction.ReturnToPrevious:
                    await ReturnToPreviousWindowAsync();
                    break;
            }
        }
        catch (Exception exception)
        {
            await ReportErrorAsync(exception.Message, CancellationToken.None);
        }
    }

    private async Task ActivateClientCoreAsync(ClientIdentity identity, CancellationToken cancellationToken)
    {
        if (!_previews.TryGetValue(identity, out var preview))
        {
            return;
        }

        var foreground = _windowQueue?.GetForegroundWindow() ?? 0;
        if (foreground != 0 && !_previews.Values.Any(item => item.Client.MainWindowHandle == foreground))
        {
            _previousForeignWindow = foreground;
            _previousForeignWindowProcessId = _windowQueue?.GetWindowProcessId(foreground) ?? 0;
        }

        var result = await EnqueueAndReportAsync(
            new WindowOperationRequest(
                WindowOperationKind.Activate,
                preview.Client.MainWindowHandle,
                ExpectedProcessId: preview.Client.ProcessId),
            cancellationToken);
        if (!result.Succeeded)
        {
            return;
        }

        _activeIdentity = identity;
        foreach (var item in _previews.Values)
        {
            item.ApplySettings(_settings, item.Identity == identity);
        }

        if (_settings.General.MinimizeInactiveClients)
        {
            await MinimizeInactiveClientsAsync(identity);
        }

        PublishSnapshot(null);
    }

    private async Task MinimizeInactiveClientsAsync(ClientIdentity activeIdentity)
    {
        foreach (var preview in _previews.Values.ToArray())
        {
            if (preview.Identity != activeIdentity &&
                _windowQueue?.IsMinimized(preview.Client.MainWindowHandle) != true)
            {
                await EnqueueAndReportAsync(
                    new WindowOperationRequest(
                        WindowOperationKind.Minimize,
                        preview.Client.MainWindowHandle,
                        ExpectedProcessId: preview.Client.ProcessId),
                    CancellationToken.None);
            }
        }
    }

    private async Task ReturnToPreviousWindowAsync()
    {
        if (_previousForeignWindow == 0)
        {
            return;
        }

        var previous = _previousForeignWindow;
        var previousProcessId = _previousForeignWindowProcessId;
        _previousForeignWindow = 0;
        _previousForeignWindowProcessId = 0;
        if (previousProcessId == 0 ||
            _windowQueue?.GetWindowProcessId(previous) != previousProcessId)
        {
            return;
        }

        await EnqueueAndReportAsync(
            new WindowOperationRequest(
                WindowOperationKind.Activate,
                previous,
                ExpectedProcessId: previousProcessId),
            CancellationToken.None);
    }

    private async Task ToggleCycleExclusionAsync(string originalTitle)
    {
        var settings = GetOrAddClientSettings(originalTitle);

        settings.ExcludeFromCycle = !settings.ExcludeFromCycle;
        PublishSnapshot(null);
        ScheduleSave();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task<WindowOperationResult> EnqueueAndReportAsync(
        WindowOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (_windowQueue is null)
        {
            return WindowOperationResult.Failure(WindowOperationError.QueueStopped);
        }

        request = request with
        {
            SuppressSystemAnimations = !_settings.General.UseWindowAnimations
        };
        var result = await _windowQueue.EnqueueAsync(request, cancellationToken);
        if (!result.Succeeded && result.Error is not WindowOperationError.Canceled)
        {
            await ReportErrorAsync(
                $"窗口操作失败（{result.Error}）：{result.Message ?? "客户端可能已退出或无响应。"}",
                cancellationToken);
        }

        return result;
    }

    private async Task ExecuteForAllAsync(WindowOperationKind kind, CancellationToken cancellationToken)
    {
        foreach (var preview in _previews.Values.ToArray())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (kind == WindowOperationKind.Minimize &&
                _windowQueue?.IsMinimized(preview.Client.MainWindowHandle) == true)
            {
                continue;
            }

            await EnqueueAndReportAsync(
                new WindowOperationRequest(
                    kind,
                    preview.Client.MainWindowHandle,
                    ExpectedProcessId: preview.Client.ProcessId),
                cancellationToken);
        }
    }

    private void RebuildPreviewWindows()
    {
        var snapshots = _previews.Values.Select(preview => preview.Client).ToArray();
        foreach (var preview in _previews.Values)
        {
            preview.Dispose();
        }

        _previews.Clear();
        _defaultPreviewSlots.Clear();
        foreach (var snapshot in snapshots)
        {
            CreatePreview(snapshot);
        }

        PublishSnapshot(null);
    }

    private void OnPreviewPlacementChanged(PreviewWindow preview, PreviewPlacement placement)
    {
        var validation = placement.Validate();
        if (!validation.IsValid)
        {
            return;
        }

        var settings = GetOrAddClientSettings(preview.Client.MainWindowTitle);

        settings.Placement = placement;
        ScheduleSave();
    }

    private (int Width, int Height) GetDefaultPreviewSize() =>
        (Math.Clamp(_settings.Preview.DefaultWidth, _settings.Preview.MinWidth, _settings.Preview.MaxWidth),
         Math.Clamp(_settings.Preview.DefaultHeight, _settings.Preview.MinHeight, _settings.Preview.MaxHeight));

    private PreviewWindow? FindPreview(string originalTitle) =>
        _previews.Values.FirstOrDefault(preview =>
            string.Equals(preview.Client.MainWindowTitle, originalTitle, StringComparison.OrdinalIgnoreCase));

    private ClientSettings GetOrAddClientSettings(string originalTitle)
    {
        var settings = FindClientSettings(originalTitle);
        if (settings is not null)
        {
            return settings;
        }

        settings = new ClientSettings { OriginalTitle = originalTitle };
        _settings.Clients.Add(settings);
        return settings;
    }

    private ClientSettings? FindClientSettings(string originalTitle) =>
        _settings.Clients.FirstOrDefault(client =>
            string.Equals(client.OriginalTitle, originalTitle, StringComparison.OrdinalIgnoreCase));

    private void MigrateClientTitle(string previousTitle, string currentTitle)
    {
        var migrated = ClientSettingsMigrator.MigrateTitle(_settings, previousTitle, currentTitle);
        _renamedClientTitles[currentTitle] = previousTitle;
        if (migrated)
        {
            ScheduleSave();
        }
    }
}
