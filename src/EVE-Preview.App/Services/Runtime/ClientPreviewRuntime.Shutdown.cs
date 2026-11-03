using System.Windows.Threading;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime
{
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Increment(ref _saveVersion);
        _lifetime?.Cancel();
        if (_dispatcher.CheckAccess())
        {
            DisposeUiResources();
        }
        else
        {
            // Shutdown is allowed to bypass queued work; the process is ending.
            _dispatcher.Invoke(DisposeUiResources, DispatcherPriority.Send);
        }

        // The poll task may still hold a registration on the token. Let process shutdown reclaim it
        // rather than racing CancellationTokenSource.Dispose against the task.
        _lifetime = null;
    }

    /// <summary>
    /// Synchronous shutdown path used by WPF's exit event. It avoids awaiting Dispatcher work while the UI thread is closing.
    /// Returns a user-facing error when the final configuration write fails.
    /// </summary>
    public string? StopForShutdown()
    {
        if (_disposed)
        {
            return null;
        }

        Interlocked.Increment(ref _saveVersion);
        _lifetime?.Cancel();
        string? saveError = null;
        if (_dispatcher.CheckAccess())
        {
            saveError = SaveOnShutdown();
            try
            {
                DisposeUiResources();
            }
            finally
            {
                _disposed = true;
            }
        }
        else
        {
            _dispatcher.Invoke(() =>
            {
                saveError = SaveOnShutdown();
                try
                {
                    DisposeUiResources();
                }
                finally
                {
                    _disposed = true;
                }
            }, DispatcherPriority.Send);
        }

        _lifetime = null;
        return saveError;
    }

    private void DisposeUiResources()
    {
        if (_uiResourcesDisposed)
        {
            return;
        }

        _uiResourcesDisposed = true;
        _started = false;
        _previewsEnabled = false;
        foreach (var registration in _registrations)
        {
            try
            {
                registration.Dispose();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
            }
        }

        _registrations.Clear();
        try
        {
            _hotkeys?.Dispose();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }

        _hotkeys = null;
        foreach (var preview in _previews.Values)
        {
            try
            {
                preview.Dispose();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
            }
        }

        _previews.Clear();
        _knownClients.Clear();
        _defaultPreviewSlots.Clear();
        _activeIdentity = null;
        try
        {
            _windowQueue?.Dispose();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }

        _windowQueue = null;
        try
        {
            PublishSnapshot(null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Dispatcher shutdown may already be in progress.
        }
    }
}
