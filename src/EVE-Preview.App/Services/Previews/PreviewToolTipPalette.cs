using System.Windows;
using System.Windows.Media;

namespace EvePreview.App.Services;

/// <summary>
/// Keeps every application tooltip on the same adaptive palette as the thumbnail tooltip:
/// the name color for text and the contrast outline color for the background.
/// </summary>
public static class PreviewToolTipPalette
{
    public const string TextBrushKey = "PreviewToolTipTextBrush";
    public const string BackgroundBrushKey = "PreviewToolTipBackgroundBrush";

    public static void Apply(string textColor)
    {
        if (Application.Current is not { } application)
        {
            return;
        }

        var (text, background) = PreviewTextPalette.Resolve(textColor);
        application.Resources[TextBrushKey] = new SolidColorBrush(text);
        application.Resources[BackgroundBrushKey] = new SolidColorBrush(background);
    }
}
