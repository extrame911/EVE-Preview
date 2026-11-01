using System.Drawing;

namespace EvePreview.Windows.Tests;

public sealed class GdiScreenshotServiceTests
{
    [Fact]
    public void Capture_releases_every_native_gdi_resource()
    {
        var native = new FakeGdiNative();
        var service = new GdiScreenshotService(native);

        using var screenshot = service.Capture((nint)123);

        Assert.NotNull(screenshot);
        Assert.Equal(
            ["restore:900", "delete-bitmap:700", "delete-dc:500", "release-dc:400"],
            native.Releases);
    }

    [Fact]
    public void Capture_releases_resources_when_print_window_fails()
    {
        var native = new FakeGdiNative { PrintWindowResult = false };
        var service = new GdiScreenshotService(native);

        Assert.Throws<System.ComponentModel.Win32Exception>(() => service.Capture((nint)123));

        Assert.Equal(
            ["restore:900", "delete-bitmap:700", "delete-dc:500", "release-dc:400"],
            native.Releases);
    }

    private sealed class FakeGdiNative : IGdiNative
    {
        public List<string> Releases { get; } = [];
        public bool PrintWindowResult { get; set; } = true;

        public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds)
        {
            bounds = new WindowBounds(0, 0, 320, 200);
            return true;
        }

        public nint GetWindowDC(nint windowHandle) => 400;
        public nint CreateCompatibleDC(nint windowDC) => 500;
        public nint CreateCompatibleBitmap(nint windowDC, int width, int height) => 700;
        public nint SelectObject(nint deviceContext, nint gdiObject) => gdiObject == 700 ? 900 : 0;
        public bool PrintWindow(nint windowHandle, nint destinationDC) => PrintWindowResult;
        public Bitmap CreateManagedBitmap(nint bitmapHandle) => new(2, 2);

        public void RestoreObject(nint deviceContext, nint previousObject) => Releases.Add($"restore:{previousObject}");
        public void DeleteObject(nint gdiObject) => Releases.Add($"delete-bitmap:{gdiObject}");
        public void DeleteDC(nint deviceContext) => Releases.Add($"delete-dc:{deviceContext}");
        public void ReleaseDC(nint windowHandle, nint deviceContext) => Releases.Add($"release-dc:{deviceContext}");
    }
}
