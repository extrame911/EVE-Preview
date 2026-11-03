using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using EvePreview.Core;
using EvePreview.Windows;
using Application = System.Windows.Application;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime
{
    private void PublishSnapshot(string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            _lastError = error;
        }

        var clients = _knownClients.Values
            .OrderBy(process => process.ProcessId)
            .Select(process =>
            {
                var identity = CreateIdentity(process);
                _previews.TryGetValue(identity, out var preview);
                var dpiX = preview is not null
                    ? VisualTreeHelper.GetDpi(preview).DpiScaleX
                    : PreviewMonitorDpi.GetPrimaryScale().X;
                var dpiY = preview is not null
                    ? VisualTreeHelper.GetDpi(preview).DpiScaleY
                    : PreviewMonitorDpi.GetPrimaryScale().Y;
                var clientSettings = FindClientSettings(process.MainWindowTitle);
                var displayName = string.IsNullOrWhiteSpace(clientSettings?.Alias)
                    ? process.MainWindowTitle
                    : clientSettings.Alias;
                return new PreviewClientStatus(
                    identity,
                    displayName,
                    process.MainWindowTitle,
                    identity == _activeIdentity,
                    preview?.IsPreviewWindowVisible == true,
                    clientSettings?.ExcludeFromCycle == true,
                    _renamedClientTitles.GetValueOrDefault(process.MainWindowTitle),
                    _windowQueue is null || !_windowQueue.IsMinimized(process.MainWindowHandle),
                    preview?.Placement ?? clientSettings?.Placement,
                    dpiX,
                    dpiY,
                    clientSettings?.Placement is not null);
            })
            .ToArray();
        _renamedClientTitles.Clear();
        var activeName = clients.FirstOrDefault(client => client.IsActive)?.DisplayName;
        _currentSnapshot = new PreviewRuntimeSnapshot(
            clients,
            activeName,
            _lastError,
            _previews.Count,
            _registrations.Count,
            _lastSaveError is not null,
            _lastSaveError);
        SnapshotChanged?.Invoke(this, _currentSnapshot);
    }

    private async Task ReportErrorAsync(string message, CancellationToken cancellationToken)
    {
        _lastError = message;
        await _dispatcher.InvokeAsync(
            () => PublishSnapshot(null),
            DispatcherPriority.Normal,
            cancellationToken);
    }

    private void ShowError(string message) => PublishSnapshot(message);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
