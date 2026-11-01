namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activeHideTimer.Stop();
        _activeHideTimer.Tick -= OnActiveHideTick;
        _focusLostTimer.Stop();
        _focusLostTimer.Tick -= OnFocusLostTick;
        _zoomDelayTimer.Stop();
        _zoomDelayTimer.Tick -= OnZoomDelayTick;
        var timer = _gdiTimer;
        _gdiTimer = null;
        if (timer is not null)
        {
            timer.Stop();
            timer.Tick -= OnGdiTick;
        }

        var thumbnail = _thumbnail;
        _thumbnail = null;
        if (thumbnail is not null)
        {
            try
            {
                thumbnail.Dispose();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // The source window may already be gone. Never keep a preview alive because of it.
            }
        }

        _gdiImage.Source = null;
        var overlay = _overlay;
        _overlay = null;
        if (overlay is not null)
        {
            try
            {
                overlay.Close();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
            }
        }

        try
        {
            Close();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }
    }
}
