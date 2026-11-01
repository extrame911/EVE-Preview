using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace EvePreview.App.Services;

/// <summary>
/// Keeps the client-name text and its contrast outline in one place so the thumbnail
/// overlay and the preview tooltip always use the same adaptive colors.
/// </summary>
public static class PreviewTextPalette
{
    public static (MediaColor Text, MediaColor Outline) Resolve(
        string value,
        string fallback = "#F3F6F8")
    {
        var text = Parse(value, fallback);
        var luminance = (0.299 * text.R + 0.587 * text.G + 0.114 * text.B) / 255d;
        var outline = luminance < 0.5
            ? MediaColor.FromRgb(0xD8, 0xD8, 0xD8)
            : MediaColor.FromRgb(0x30, 0x30, 0x30);
        return (text, outline);
    }

    private static MediaColor Parse(string value, string fallback)
    {
        try
        {
            return (MediaColor)MediaColorConverter.ConvertFromString(value);
        }
        catch (FormatException)
        {
            return (MediaColor)MediaColorConverter.ConvertFromString(fallback);
        }
        catch (NotSupportedException)
        {
            return (MediaColor)MediaColorConverter.ConvertFromString(fallback);
        }
    }
}
