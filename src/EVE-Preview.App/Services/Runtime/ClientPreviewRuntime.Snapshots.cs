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

        var clients = _previews.Values
            .OrderBy(preview => preview.Client.ProcessId)
            .Select(preview =>
            {
                var dpi = VisualTreeHelper.GetDpi(preview);
                var clientSettings = FindClientSettings(preview.Client.MainWindowTitle);
                return new PreviewClientStatus(
                    preview.Identity,
                    preview.DisplayName,
                    preview.Client.MainWindowTitle,
                    preview.Identity == _activeIdentity,
                    preview.IsPreviewWindowVisible,
                    clientSettings?.ExcludeFromCycle == true,
                    _renamedClientTitles.GetValueOrDefault(preview.Client.MainWindowTitle),
                    _windowQueue is null || !_windowQueue.IsMinimized(preview.Client.MainWindowHandle),
                    preview.Placement,
                    dpi.DpiScaleX,
                    dpi.DpiScaleY,
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
