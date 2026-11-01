using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EvePreview.App.Services;

/// <summary>
/// The visual stand-in for a thumbnail that is too small to render useful content.
/// All hit testing, dragging, zooming and window management remain on PreviewWindow;
/// this surface only replaces the rendered pixels.
/// </summary>
public sealed class CompactPreviewSurface : Border
{
    public const double MinimumDimension = 16;

    public CompactPreviewSurface()
    {
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        Background = Brushes.Transparent;
    }

    public static bool ShouldUse(double width, double height) =>
        width > 0 && height > 0 && width < MinimumDimension && height < MinimumDimension;

    public void Update(bool compact, Brush fill, double opacity)
    {
        Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        if (!compact)
        {
            return;
        }

        Background = fill;
        Opacity = opacity;
    }
}
