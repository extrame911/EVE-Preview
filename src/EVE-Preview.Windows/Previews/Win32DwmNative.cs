using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace EvePreview.Windows;

internal sealed class Win32DwmNative : IDwmNative
{
    private const uint DwmTnpRectDestination = 0x00000001;
    private const uint DwmTnpRectSource = 0x00000002;
    private const uint DwmTnpOpacity = 0x00000004;
    private const uint DwmTnpVisible = 0x00000008;
    private const uint DwmTnpSourceClientAreaOnly = 0x00000010;
    public NativeDwmRegistration Register(nint sourceWindow, nint destinationWindow)
    {
        var hResult = PInvoke.DwmRegisterThumbnail(
            new HWND(destinationWindow),
            new HWND(sourceWindow),
            out var thumbnail);
        return new NativeDwmRegistration(hResult, thumbnail);
    }

    public int Update(nint thumbnailHandle, DwmThumbnailProperties properties)
    {
        var flags = DwmTnpRectDestination |
                    DwmTnpOpacity |
                    DwmTnpVisible |
                    DwmTnpSourceClientAreaOnly;
        if (properties.Source is not null)
        {
            flags |= DwmTnpRectSource;
        }

        var nativeProperties = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = flags,
            rcDestination = ToRect(properties.Destination),
            rcSource = ToRect(properties.Source ?? default),
            opacity = properties.Opacity,
            fVisible = new BOOL(properties.Visible),
            fSourceClientAreaOnly = new BOOL(properties.SourceClientAreaOnly)
        };
        return PInvoke.DwmUpdateThumbnailProperties(thumbnailHandle, in nativeProperties);
    }

    public void Unregister(nint thumbnailHandle) =>
        Marshal.ThrowExceptionForHR(PInvoke.DwmUnregisterThumbnail(thumbnailHandle));

    private static RECT ToRect(WindowBounds bounds) => new()
    {
        left = bounds.X,
        top = bounds.Y,
        right = bounds.X + bounds.Width,
        bottom = bounds.Y + bounds.Height
    };
}
