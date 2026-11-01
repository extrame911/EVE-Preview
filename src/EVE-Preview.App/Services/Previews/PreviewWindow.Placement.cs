using System.Windows;
using System.Windows.Media;
using EvePreview.Core;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    public void ApplyPlacement(PreviewPlacement placement)
    {
        placement = ConstrainToScreen(placement);
        _placement = placement;
        _basePlacement = placement;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;

        UpdateThumbnailBounds();
        SyncOverlay();
    }

    public PreviewPlacement ApplyDefaultPhysicalSize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _useDefaultPhysicalSize = true;
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = PreviewPixelSize.ToDip(
            Math.Clamp(settings.Preview.DefaultWidth, settings.Preview.MinWidth, settings.Preview.MaxWidth),
            dpi.DpiScaleX);
        var height = PreviewPixelSize.ToDip(
            Math.Clamp(settings.Preview.DefaultHeight, settings.Preview.MinHeight, settings.Preview.MaxHeight),
            dpi.DpiScaleY);
        ApplyPlacement(new PreviewPlacement(Left, Top, width, height));
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

    private bool TryConstrainToMonitor(PreviewPlacement placement, out PreviewPlacement constrained)
    {
        constrained = placement;
        if (PresentationSource.FromVisual(this) is null)
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var currentTopLeft = PointToScreen(new Point(0, 0));
        var physicalLeft = currentTopLeft.X + (placement.Left - Left) * dpi.DpiScaleX;
        var physicalTop = currentTopLeft.Y + (placement.Top - Top) * dpi.DpiScaleY;
        var physicalWidth = placement.Width * dpi.DpiScaleX;
        var physicalHeight = placement.Height * dpi.DpiScaleY;
        var monitor = MonitorGeometry.FromPoint(
            physicalLeft + physicalWidth / 2,
            physicalTop + physicalHeight / 2);
        if (monitor is null)
        {
            return false;
        }

        var width = Math.Max(1, (int)Math.Round(physicalWidth));
        var height = Math.Max(1, (int)Math.Round(physicalHeight));
        var visibleX = Math.Clamp(
            _settings.Display.ExpansionThreshold,
            0,
            Math.Max(0, width - 1));
        var visibleY = Math.Clamp(
            _settings.Display.ExpansionThreshold,
            0,
            Math.Max(0, height - 1));
        var work = monitor.WorkArea;
        var x = Math.Clamp(
            (int)Math.Round(physicalLeft),
            work.X - width + visibleX,
            work.X + work.Width - visibleX);
        var y = Math.Clamp(
            (int)Math.Round(physicalTop),
            work.Y - height + visibleY,
            work.Y + work.Height - visibleY);

        constrained = new PreviewPlacement(
            Left + (x - currentTopLeft.X) / dpi.DpiScaleX,
            Top + (y - currentTopLeft.Y) / dpi.DpiScaleY,
            placement.Width,
            placement.Height);
        return true;
    }
}
