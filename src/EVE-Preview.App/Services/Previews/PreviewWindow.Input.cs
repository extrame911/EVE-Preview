using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EvePreview.Core;
using Point = System.Windows.Point;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _leftDown = true;
        _suppressClick = false;
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_leftDown)
        {
            return;
        }

        _leftDown = false;
        e.Handled = true;
        if (_suppressClick || _rightDown)
        {
            _suppressClick = false;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var action = modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Shift)
            ? PreviewClickAction.ReturnToPrevious
            : modifiers.HasFlag(ModifierKeys.Control)
                ? PreviewClickAction.Minimize
                : modifiers.HasFlag(ModifierKeys.Shift)
                    ? PreviewClickAction.ToggleCycleExclusion
                    : PreviewClickAction.Activate;
        _clickAction(this, action);
    }

    private void ResetPointerState()
    {
        _leftDown = false;
        _rightDown = false;
        _resizing = false;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.Display.LockPosition)
        {
            return;
        }

        RestoreZoom();
        _rightDown = true;
        _resizing = Mouse.LeftButton == MouseButtonState.Pressed;
        _dragStartPhysical = GetPhysicalScreenPoint(e);
        if (TryGetPhysicalWindowBounds(out var windowBounds))
        {
            _dragLeftPhysical = windowBounds.X;
            _dragTopPhysical = windowBounds.Y;
            _dragWidthPhysical = windowBounds.Width;
            _dragHeightPhysical = windowBounds.Height;
        }
        else
        {
            var origin = PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(this);
            _dragLeftPhysical = origin.X;
            _dragTopPhysical = origin.Y;
            _dragWidthPhysical = Math.Max(1, ActualWidth * dpi.DpiScaleX);
            _dragHeightPhysical = Math.Max(1, ActualHeight * dpi.DpiScaleY);
        }

        _dragSettleTimer.Stop();
        _physicalDragTarget = new PixelRectangle(
            (int)Math.Round(_dragLeftPhysical),
            (int)Math.Round(_dragTopPhysical),
            (int)Math.Round(_dragWidthPhysical),
            (int)Math.Round(_dragHeightPhysical));

        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_rightDown)
        {
            return;
        }

        CompleteRightDrag(GetPhysicalScreenPoint(e));
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_rightDown)
        {
            return;
        }

        if (Mouse.RightButton == MouseButtonState.Pressed)
        {
            RestoreDragCapture();
            return;
        }

        CompleteRightDrag(PointToScreen(Mouse.GetPosition(this)));
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_rightDown && Mouse.RightButton == MouseButtonState.Pressed)
        {
            RestoreDragCapture();
            return;
        }

        ResetPointerState();
    }

    private void RestoreDragCapture()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (_rightDown && Mouse.RightButton == MouseButtonState.Pressed && !IsMouseCaptured)
            {
                CaptureMouse();
            }
        });
    }

    private void CompleteRightDrag(Point dropPoint)
    {
        _rightDown = false;
        _suppressClick = true;
        ReleaseMouseCapture();
        var releaseDeltaX = dropPoint.X - _dragStartPhysical.X;
        var releaseDeltaY = dropPoint.Y - _dragStartPhysical.Y;
        var physicalWidth = _useDefaultPhysicalSize
            ? Math.Clamp(_settings.Preview.DefaultWidth, _settings.Preview.MinWidth, _settings.Preview.MaxWidth)
            : _resizing
                ? Math.Clamp(_dragWidthPhysical + releaseDeltaX, _settings.Preview.MinWidth, _settings.Preview.MaxWidth)
                : _dragWidthPhysical;
        var physicalHeight = _useDefaultPhysicalSize
            ? Math.Clamp(_settings.Preview.DefaultHeight, _settings.Preview.MinHeight, _settings.Preview.MaxHeight)
            : _resizing
                ? Math.Clamp(_dragHeightPhysical + releaseDeltaY, _settings.Preview.MinHeight, _settings.Preview.MaxHeight)
                : _dragHeightPhysical;
        var releasedBounds = GetDropBounds(
            new PixelRectangle(
                (int)Math.Round(_dragLeftPhysical + releaseDeltaX),
                (int)Math.Round(_dragTopPhysical + releaseDeltaY),
                (int)Math.Round(physicalWidth),
                (int)Math.Round(physicalHeight)));
        SetPhysicalWindowBounds(
            releasedBounds.X,
            releasedBounds.Y,
            releasedBounds.Width,
            releasedBounds.Height);
        SyncManagedSizeToPhysical();
        _physicalDragTarget = releasedBounds;
        _dragSettleTimer.Stop();
        _dragSettleTimer.Start();

        var systemDpi = PreviewMonitorDpi.GetPrimaryScale();
        _placement = new PreviewPlacement(
            releasedBounds.X / systemDpi.X,
            releasedBounds.Y / systemDpi.Y,
            releasedBounds.Width / systemDpi.X,
            releasedBounds.Height / systemDpi.Y);
        _basePlacement = _placement;
        SyncOverlay();
        UpdateThumbnailBounds();
        _placementChanged(this, _placement);
    }

    private void OnDragSettleTick(object? sender, EventArgs e)
    {
        _dragSettleTimer.Stop();
        if (_physicalDragTarget is not { } target)
        {
            return;
        }

        SetPhysicalWindowBounds(target.X, target.Y, target.Width, target.Height);
        SyncManagedSizeToPhysical();
        _physicalDragTarget = null;
        SyncOverlay();
        UpdateThumbnailBounds();
    }

    private PixelRectangle GetDropBounds(PixelRectangle bounds)
    {
        var monitor = MonitorGeometry.FromRectangleCenter(bounds);
        if (monitor is null)
        {
            return bounds;
        }

        var snapped = PreviewSnapGeometry.Snap(
            bounds,
            monitor.WorkArea,
            _settings.Preview.SnapMode,
            _settings.Preview.GridSizeX,
            _settings.Preview.GridSizeY,
            _settings.Preview.MinWidth,
            _settings.Preview.MinHeight,
            _settings.Preview.MaxWidth,
            _settings.Preview.MaxHeight,
            (int)Math.Round(16 * monitor.DpiScaleX));
        return PreviewSnapGeometry.ConstrainToWorkArea(
            snapped,
            monitor.WorkArea,
            _settings.Display.ExpansionThreshold,
            keepFullyVisible: true);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_rightDown)
        {
            return;
        }

        var point = GetPhysicalScreenPoint(e);
        var deltaX = point.X - _dragStartPhysical.X;
        var deltaY = point.Y - _dragStartPhysical.Y;
        var targetLeft = _dragLeftPhysical + deltaX;
        var targetTop = _dragTopPhysical + deltaY;
        var targetWidth = _resizing
            ? Math.Clamp(_dragWidthPhysical + deltaX, _settings.Preview.MinWidth, _settings.Preview.MaxWidth)
            : _dragWidthPhysical;
        var targetHeight = _resizing
            ? Math.Clamp(_dragHeightPhysical + deltaY, _settings.Preview.MinHeight, _settings.Preview.MaxHeight)
            : _dragHeightPhysical;
        if (_resizing)
        {
            _useDefaultPhysicalSize = false;
        }

        var target = new PixelRectangle(
            (int)Math.Round(targetLeft),
            (int)Math.Round(targetTop),
            (int)Math.Round(targetWidth),
            (int)Math.Round(targetHeight));
        _physicalDragTarget = target;
        SetPhysicalWindowBounds(target.X, target.Y, target.Width, target.Height);

        e.Handled = true;
    }

    private Point GetPhysicalScreenPoint(MouseEventArgs e) =>
        PointToScreen(e.GetPosition(this));

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (_rightDown || _zoomed || _zoomPending || !_settings.Zoom.Enabled || _settings.Display.LockPosition)
        {
            return;
        }

        var delay = Math.Clamp(_settings.Zoom.DelayMs, 0, 3000);
        if (delay == 0)
        {
            StartZoom();
            return;
        }

        _zoomPending = true;
        _zoomDelayTimer.Interval = TimeSpan.FromMilliseconds(delay);
        _zoomDelayTimer.Start();
    }

    private void OnZoomDelayTick(object? sender, EventArgs e)
    {
        _zoomDelayTimer.Stop();
        if (!_zoomPending)
        {
            return;
        }

        _zoomPending = false;
        StartZoom();
    }

    private void StartZoom()
    {
        if (_rightDown || _zoomed || !_settings.Zoom.Enabled || _settings.Display.LockPosition)
        {
            return;
        }

        _zoomed = true;
        _basePlacement = new PreviewPlacement(Left, Top, Width, Height);
        Opacity = 1;
        var factor = Math.Clamp(_settings.Zoom.Factor, 1, 4);
        var dpi = VisualTreeHelper.GetDpi(this);
        var newWidth = Math.Min(PreviewPixelSize.ToDip(_settings.Preview.MaxWidth, dpi.DpiScaleX), Width * factor);
        var newHeight = Math.Min(PreviewPixelSize.ToDip(_settings.Preview.MaxHeight, dpi.DpiScaleY), Height * factor);
        var widthDelta = newWidth - Width;
        var heightDelta = newHeight - Height;
        Width = newWidth;
        Height = newHeight;
        Left -= _settings.Zoom.Anchor switch
        {
            ZoomAnchor.TopRight or ZoomAnchor.Right or ZoomAnchor.BottomRight => widthDelta,
            ZoomAnchor.Top or ZoomAnchor.Center or ZoomAnchor.Bottom => widthDelta / 2,
            _ => 0
        };
        Top -= _settings.Zoom.Anchor switch
        {
            ZoomAnchor.BottomLeft or ZoomAnchor.Bottom or ZoomAnchor.BottomRight => heightDelta,
            ZoomAnchor.Left or ZoomAnchor.Center or ZoomAnchor.Right => heightDelta / 2,
            _ => 0
        };
    }

    private void OnMouseLeave(object sender, MouseEventArgs e) => RestoreZoom();

    private void RestoreZoom()
    {
        CancelZoomDelay();
        if (!_zoomed)
        {
            return;
        }

        _zoomed = false;
        Left = _basePlacement.Left;
        Top = _basePlacement.Top;
        Width = _basePlacement.Width;
        Height = _basePlacement.Height;
        Opacity = 1;
    }

    private void CancelZoomDelay()
    {
        _zoomDelayTimer.Stop();
        _zoomPending = false;
    }

}
