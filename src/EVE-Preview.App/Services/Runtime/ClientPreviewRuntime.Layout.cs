using System.Windows.Threading;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime
{
    public async Task<PixelRectangle?> SaveClientWindowLayoutAsync(
        string originalWindowTitle,
        CancellationToken cancellationToken = default)
    {
        return await _dispatcher.InvokeAsync(
            () => SaveClientWindowLayoutCore(originalWindowTitle, cancellationToken),
            DispatcherPriority.Normal,
            cancellationToken).Task.ConfigureAwait(false);
    }

    public async Task<ClientWindowLayoutBatchResult> SaveAllClientWindowLayoutsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dispatcher.InvokeAsync(
            () => SaveAllClientWindowLayoutsCore(cancellationToken),
            DispatcherPriority.Normal,
            cancellationToken).Task.ConfigureAwait(false);
    }

    public async Task<bool> ApplyClientWindowLayoutAsync(
        string originalWindowTitle,
        CancellationToken cancellationToken = default)
    {
        return await _dispatcher.InvokeAsync(
            () => ApplySavedClientWindowLayoutAsync(originalWindowTitle, automatic: false, cancellationToken),
            DispatcherPriority.Normal,
            cancellationToken).Task.Unwrap().ConfigureAwait(false);
    }

    private PixelRectangle? SaveClientWindowLayoutCore(string originalWindowTitle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var preview = FindPreview(originalWindowTitle);
        if (preview is null || _windowQueue is null)
        {
            _lastError = "无法保存布局：客户端已离线。";
            PublishSnapshot(null);
            return null;
        }

        if (_windowQueue.IsMinimized(preview.Client.MainWindowHandle))
        {
            _lastError = "最小化或已退出的客户端没有可保存的窗口布局。";
            PublishSnapshot(null);
            return null;
        }

        if (!_windowQueue.TryGetWindowBounds(preview.Client.MainWindowHandle, out var bounds) ||
            bounds.Width <= 0 || bounds.Height <= 0)
        {
            _lastError = "无法读取客户端窗口布局。";
            PublishSnapshot(null);
            return null;
        }

        var clientSettings = GetOrAddClientSettings(originalWindowTitle);

        var saved = new PixelRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        clientSettings.ClientWindowBounds = saved;
        ScheduleSave();
        return saved;
    }

    private ClientWindowLayoutBatchResult SaveAllClientWindowLayoutsCore(CancellationToken cancellationToken)
    {
        var savedLayouts = new Dictionary<string, PixelRectangle>(StringComparer.OrdinalIgnoreCase);
        var skippedCount = 0;
        foreach (var preview in _previews.Values.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_windowQueue is null ||
                _windowQueue.IsMinimized(preview.Client.MainWindowHandle) ||
                !_windowQueue.TryGetWindowBounds(preview.Client.MainWindowHandle, out var bounds) ||
                bounds.Width <= 0 || bounds.Height <= 0)
            {
                skippedCount++;
                continue;
            }

            var saved = new PixelRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            GetOrAddClientSettings(preview.Client.MainWindowTitle).ClientWindowBounds = saved;
            savedLayouts[preview.Client.MainWindowTitle] = saved;
        }

        if (savedLayouts.Count > 0)
        {
            ScheduleSave();
        }
        return new ClientWindowLayoutBatchResult(savedLayouts, skippedCount);
    }

    private void ApplySavedClientWindowLayoutOnLogin(ClientIdentity identity)
    {
        if (!_previews.TryGetValue(identity, out var preview))
        {
            return;
        }

        var title = preview.Client.MainWindowTitle;
        if (ClientWindowTitle.IsLoginWindow(title) || FindClientSettings(title)?.ClientWindowBounds is null)
        {
            return;
        }

        _ = ApplySavedClientWindowLayoutOnLoginAsync(title);
    }

    private async Task<bool> ApplySavedClientWindowLayoutAsync(
        string originalWindowTitle,
        bool automatic,
        CancellationToken cancellationToken)
    {
        if (automatic && !_settings.General.AutoRestoreClientWindowOnStartup)
        {
            return false;
        }

        return await ApplyClientWindowLayoutCoreAsync(originalWindowTitle, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ApplySavedClientWindowLayoutOnLoginAsync(string originalWindowTitle)
    {
        try
        {
            await ApplySavedClientWindowLayoutAsync(
                originalWindowTitle,
                automatic: true,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            await ReportErrorAsync($"自动应用客户端窗口位置失败：{exception.Message}", CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private async Task<bool> ApplyClientWindowLayoutCoreAsync(
        string originalWindowTitle,
        CancellationToken cancellationToken)
    {
        var preview = FindPreview(originalWindowTitle);
        var clientSettings = FindClientSettings(originalWindowTitle);
        if (preview is null || clientSettings?.ClientWindowBounds is null)
        {
            _lastError = "无法应用布局：客户端已离线或尚未保存布局。";
            PublishSnapshot(null);
            return false;
        }

        var saved = clientSettings.ClientWindowBounds.Value;
        var restored = await EnqueueAndReportAsync(
            new WindowOperationRequest(
                WindowOperationKind.Restore,
                preview.Client.MainWindowHandle,
                ExpectedProcessId: preview.Client.ProcessId),
            cancellationToken);
        if (!restored.Succeeded)
        {
            return false;
        }

        var moved = await EnqueueAndReportAsync(
            new WindowOperationRequest(
                WindowOperationKind.MoveResize,
                preview.Client.MainWindowHandle,
                new WindowBounds(saved.X, saved.Y, saved.Width, saved.Height),
                ExpectedProcessId: preview.Client.ProcessId),
            cancellationToken);
        if (!moved.Succeeded)
        {
            return false;
        }

        return await VerifySavedClientWindowBoundsAsync(
            preview.Client.MainWindowHandle,
            saved,
            cancellationToken);
    }

    private async Task<bool> VerifySavedClientWindowBoundsAsync(
        nint windowHandle,
        PixelRectangle saved,
        CancellationToken cancellationToken)
    {
        await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        var target = new WindowBounds(saved.X, saved.Y, saved.Width, saved.Height);
        var targetMonitor = MonitorGeometry.FromRectangleCenter(saved);
        if (_windowQueue is not null &&
            _windowQueue.TryGetWindowBounds(windowHandle, out var actual) &&
            actual == target &&
            IsOnTargetMonitor(actual, targetMonitor))
        {
            return true;
        }

        _lastError = "应用窗口布局后，客户端窗口尺寸或所属显示器与保存值不一致。";
        await _dispatcher.InvokeAsync(
            () => PublishSnapshot(null),
            DispatcherPriority.Background,
            cancellationToken).Task.ConfigureAwait(false);
        return false;
    }

    private static bool IsOnTargetMonitor(WindowBounds bounds, MonitorGeometry? targetMonitor)
    {
        if (targetMonitor is null)
        {
            return true;
        }

        var actualMonitor = MonitorGeometry.FromRectangleCenter(
            new PixelRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        return actualMonitor is not null &&
               actualMonitor.WorkArea == targetMonitor.WorkArea &&
               Math.Abs(actualMonitor.DpiScaleX - targetMonitor.DpiScaleX) < 0.001 &&
               Math.Abs(actualMonitor.DpiScaleY - targetMonitor.DpiScaleY) < 0.001;
    }

}
