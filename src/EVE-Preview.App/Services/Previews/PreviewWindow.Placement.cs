using System.Windows;
using System.Windows.Media;
using EvePreview.Core;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    public void ApplyPlacement(PreviewPlacement placement)
    {
        if (_rightDown)
        {
            return;
        }

        if (PresentationSource.FromVisual(this) is not null)
        {
            ApplyPhysicalPlacement(placement);
            return;
        }

        placement = ConstrainToScreen(placement);
        _placement = placement;
        _basePlacement = placement;
        var systemDpi = PreviewMonitorDpi.GetPrimaryScale();
        var windowDpi = VisualTreeHelper.GetDpi(this);
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width * systemDpi.X / windowDpi.DpiScaleX;
        Height = placement.Height * systemDpi.Y / windowDpi.DpiScaleY;

        UpdateThumbnailBounds();
        SyncOverlay();
    }

    private void ApplyPhysicalPlacement(PreviewPlacement placement)
    {
        var systemDpi = PreviewMonitorDpi.GetPrimaryScale();
        var bounds = new PixelRectangle(
            (int)Math.Round(placement.Left * systemDpi.X),
            (int)Math.Round(placement.Top * systemDpi.Y),
            Math.Max(1, (int)Math.Round(placement.Width * systemDpi.X)),
            Math.Max(1, (int)Math.Round(placement.Height * systemDpi.Y)));
        var monitor = MonitorGeometry.FromRectangleCenter(bounds);
        if (monitor is not null)
        {
            bounds = PreviewSnapGeometry.ConstrainToWorkArea(
                bounds,
                monitor.WorkArea,
                _settings.Display.ExpansionThreshold,
                CrossesAnotherMonitor(bounds, monitor));
        }

        SetPhysicalWindowBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        SyncManagedSizeToPhysical();
        _placement = new PreviewPlacement(
            bounds.X / systemDpi.X,
            bounds.Y / systemDpi.Y,
            bounds.Width / systemDpi.X,
            bounds.Height / systemDpi.Y);
        _basePlacement = _placement;
        _physicalDragTarget = bounds;
        _dragSettleTimer.Stop();
        _dragSettleTimer.Start();
        UpdateThumbnailBounds();
        SyncOverlay();
    }

    public PreviewPlacement ApplyDefaultPhysicalSize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _useDefaultPhysicalSize = true;
        var systemDpi = PreviewMonitorDpi.GetPrimaryScale();
        var physicalLeft = Left * systemDpi.X;
        var physicalTop = Top * systemDpi.Y;
        if (TryGetPhysicalWindowBounds(out var currentBounds))
        {
            physicalLeft = currentBounds.X;
            physicalTop = currentBounds.Y;
        }

        var bounds = new PixelRectangle(
            (int)Math.Round(physicalLeft),
            (int)Math.Round(physicalTop),
            Math.Clamp(settings.Preview.DefaultWidth, settings.Preview.MinWidth, settings.Preview.MaxWidth),
            Math.Clamp(settings.Preview.DefaultHeight, settings.Preview.MinHeight, settings.Preview.MaxHeight));
        SetPhysicalWindowBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        SyncManagedSizeToPhysical();
        _placement = new PreviewPlacement(
            bounds.X / systemDpi.X,
            bounds.Y / systemDpi.Y,
            bounds.Width / systemDpi.X,
            bounds.Height / systemDpi.Y);
        _basePlacement = _placement;
        _physicalDragTarget = bounds;
        _dragSettleTimer.Stop();
        _dragSettleTimer.Start();
        UpdateThumbnailBounds();
        SyncOverlay();
        return _placement;
    }

    private PreviewPlacement ConstrainToScreen(PreviewPlacement placement)
    {
        if (!_settings.Display.HideWhenOutOfBounds)
        {
            return placement;
        }

        if (TryConstrainToMonitor(placement, out var constrained))
        {
            return constrained;
        }

        var screen = new PixelRectangle(
            (int)Math.Round(SystemParameters.VirtualScreenLeft),
            (int)Math.Round(SystemParameters.VirtualScreenTop),
            Math.Max(1, (int)Math.Round(SystemParameters.VirtualScreenWidth)),
            Math.Max(1, (int)Math.Round(SystemParameters.VirtualScreenHeight)));
        var dpi = VisualTreeHelper.GetDpi(this);
        var layout = new PreviewLayout
        {
            Bounds = new PixelRectangle(
                (int)Math.Round(placement.Left),
                (int)Math.Round(placement.Top),
                Math.Max(1, (int)Math.Round(placement.Width)),
                Math.Max(1, (int)Math.Round(placement.Height))),
            HideWhenOutOfBounds = true,
            ExpansionThreshold = (int)Math.Round(
                PreviewPixelSize.ToDip(_settings.Display.ExpansionThreshold, dpi.DpiScaleX),
                MidpointRounding.AwayFromZero)
        };
        var clamped = layout.ClampToVisibleScreen(screen);
        return new PreviewPlacement(clamped.X, clamped.Y, clamped.Width, clamped.Height);
    }

    private bool TryConstrainToMonitor(
        PreviewPlacement placement,
        out PreviewPlacement constrained)
    {
        constrained = placement;
        if (PresentationSource.FromVisual(this) is null)
        {
            return false;
        }

        var systemDpi = PreviewMonitorDpi.GetPrimaryScale();
        var currentTopLeft = PointToScreen(new Point(0, 0));
        var physicalLeft = currentTopLeft.X + (placement.Left - Left) * systemDpi.X;
        var physicalTop = currentTopLeft.Y + (placement.Top - Top) * systemDpi.Y;
        var physicalWidth = placement.Width * systemDpi.X;
        var physicalHeight = placement.Height * systemDpi.Y;
        var width = Math.Max(1, (int)Math.Round(physicalWidth));
        var height = Math.Max(1, (int)Math.Round(physicalHeight));
        var physicalBounds = new PixelRectangle(
            (int)Math.Round(physicalLeft),
            (int)Math.Round(physicalTop),
            width,
            height);
        var monitor = MonitorGeometry.FromRectangleCenter(physicalBounds);
        if (monitor is null)
        {
            return false;
        }

        var keepFullyVisible = CrossesAnotherMonitor(physicalBounds, monitor);
        var constrainedPhysical = PreviewSnapGeometry.ConstrainToWorkArea(
            physicalBounds,
            monitor.WorkArea,
            _settings.Display.ExpansionThreshold,
            keepFullyVisible);

        constrained = new PreviewPlacement(
            constrainedPhysical.X / systemDpi.X,
            constrainedPhysical.Y / systemDpi.Y,
            constrainedPhysical.Width / systemDpi.X,
            constrainedPhysical.Height / systemDpi.Y);
        return true;
    }

    private static bool CrossesAnotherMonitor(PixelRectangle bounds, MonitorGeometry target)
    {
        var points = new[]
        {
            (bounds.X, bounds.Y),
            (bounds.X + bounds.Width - 1, bounds.Y),
            (bounds.X, bounds.Y + bounds.Height - 1),
            (bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1)
        };
        foreach (var (x, y) in points)
        {
            var monitor = MonitorGeometry.FromPoint(x, y);
            if (monitor is null || !monitor.WorkArea.Equals(target.WorkArea))
            {
                return true;
            }
        }

        return false;
    }
}
