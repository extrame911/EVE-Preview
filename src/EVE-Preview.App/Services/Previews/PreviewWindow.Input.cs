using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
        _dragStart = GetScreenPoint(e);
        _dragLeft = Left;
        _dragTop = Top;
        _dragWidth = Width;
        _dragHeight = Height;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_rightDown)
        {
            return;
        }

        _rightDown = false;
        _suppressClick = true;
        ReleaseMouseCapture();
        ApplyDragSnap();
        _placement = ConstrainToScreen(new PreviewPlacement(Left, Top, Width, Height));
        ApplyPlacement(_placement);
        _placementChanged(this, _placement);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_rightDown)
        {
            return;
        }

        var point = GetScreenPoint(e);
        var deltaX = point.X - _dragStart.X;
        var deltaY = point.Y - _dragStart.Y;
        if (_resizing)
        {
            _useDefaultPhysicalSize = false;
            var dpi = VisualTreeHelper.GetDpi(this);
            Width = Math.Clamp(
                _dragWidth + deltaX,
                PreviewPixelSize.ToDip(_settings.Preview.MinWidth, dpi.DpiScaleX),
                PreviewPixelSize.ToDip(_settings.Preview.MaxWidth, dpi.DpiScaleX));
            Height = Math.Clamp(
                _dragHeight + deltaY,
                PreviewPixelSize.ToDip(_settings.Preview.MinHeight, dpi.DpiScaleY),
                PreviewPixelSize.ToDip(_settings.Preview.MaxHeight, dpi.DpiScaleY));
        }
        else
        {
            Left = _dragLeft + deltaX;
            Top = _dragTop + deltaY;
        }

        e.Handled = true;
    }

    private Point GetScreenPoint(MouseEventArgs e)
    {
        var screen = PointToScreen(e.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Point(screen.X / dpi.DpiScaleX, screen.Y / dpi.DpiScaleY);
    }

    private void ApplyDragSnap()
    {
        if (TryApplyMonitorSnap())
        {
            return;
        }

        if (_settings.Preview.SnapMode == SnapMode.Grid)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var rectangle = GridSnapper.Snap(
                new PreviewPlacement(Left, Top, Width, Height),
                PreviewPixelSize.ToDip(Math.Max(1, _settings.Preview.GridSizeX), dpi.DpiScaleX),
                PreviewPixelSize.ToDip(Math.Max(1, _settings.Preview.GridSizeY), dpi.DpiScaleY));
            var previousWidth = Width;
            var previousHeight = Height;
            Left = rectangle.Left;
            Top = rectangle.Top;
            Width = Math.Clamp(
                rectangle.Width,
                PreviewPixelSize.ToDip(_settings.Preview.MinWidth, dpi.DpiScaleX),
                PreviewPixelSize.ToDip(_settings.Preview.MaxWidth, dpi.DpiScaleX));
            Height = Math.Clamp(
                rectangle.Height,
                PreviewPixelSize.ToDip(_settings.Preview.MinHeight, dpi.DpiScaleY),
                PreviewPixelSize.ToDip(_settings.Preview.MaxHeight, dpi.DpiScaleY));
            if (Width != previousWidth || Height != previousHeight)
            {
                _useDefaultPhysicalSize = false;
            }
        }
        else if (_settings.Preview.SnapMode == SnapMode.Edge)
        {
            const double threshold = 16;
            var area = SystemParameters.WorkArea;
            if (Math.Abs(Left - area.Left) < threshold)
            {
                Left = area.Left;
            }
            else if (Math.Abs(Left + Width - area.Right) < threshold)
            {
                Left = area.Right - Width;
            }

            if (Math.Abs(Top - area.Top) < threshold)
            {
                Top = area.Top;
            }
            else if (Math.Abs(Top + Height - area.Bottom) < threshold)
            {
                Top = area.Bottom - Height;
            }
        }
    }

    private bool TryApplyMonitorSnap()
    {
        if (_settings.Preview.SnapMode is not (SnapMode.Grid or SnapMode.Edge) ||
            PresentationSource.FromVisual(this) is null)
        {
            return false;
        }

        var center = PointToScreen(new Point(Width / 2, Height / 2));
        var monitor = MonitorGeometry.FromPoint(center.X, center.Y);
        if (monitor is null)
        {
            return false;
        }

        var windowDpi = VisualTreeHelper.GetDpi(this);
        var topLeft = PointToScreen(new Point(0, 0));
        var bottomRight = PointToScreen(new Point(Width, Height));
        var physical = new PixelRectangle(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            Math.Max(1, (int)Math.Round(bottomRight.X - topLeft.X)),
            Math.Max(1, (int)Math.Round(bottomRight.Y - topLeft.Y)));
        var snapped = PreviewSnapGeometry.Snap(
            physical,
            monitor.WorkArea,
            _settings.Preview.SnapMode,
            _settings.Preview.GridSizeX,
            _settings.Preview.GridSizeY,
            _settings.Preview.MinWidth,
            _settings.Preview.MinHeight,
            _settings.Preview.MaxWidth,
            _settings.Preview.MaxHeight,
            (int)Math.Round(16 * monitor.DpiScaleX));
        if (snapped == physical)
        {
            return true;
        }

        var previousWidth = Width;
        var previousHeight = Height;
        Left += (snapped.X - physical.X) / windowDpi.DpiScaleX;
        Top += (snapped.Y - physical.Y) / windowDpi.DpiScaleY;
        Width = snapped.Width / windowDpi.DpiScaleX;
        Height = snapped.Height / windowDpi.DpiScaleY;
        var sizeChanged = Width != previousWidth || Height != previousHeight;
        if (sizeChanged)
        {
            _useDefaultPhysicalSize = false;
        }

        return true;
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (_zoomed || _zoomPending || !_settings.Zoom.Enabled || _settings.Display.LockPosition)
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
        if (_zoomed || !_settings.Zoom.Enabled || _settings.Display.LockPosition)
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
