using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;

namespace EvePreview.App.Services;

public static class AppIconProvider
{
    public static DrawingIcon LoadTrayIcon()
    {
        var uri = BuildUri();
        try
        {
            var stream = Application.GetResourceStream(uri)?.Stream;
            return stream is null ? System.Drawing.SystemIcons.Application : new DrawingIcon(stream);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            return System.Drawing.SystemIcons.Application;
        }
    }

    public static BitmapImage? LoadWindowIcon()
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = BuildUri();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static Uri BuildUri() =>
        new("pack://application:,,,/Assets/Icons/Default.ico", UriKind.Absolute);
}
