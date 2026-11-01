using System.Drawing;

namespace EvePreview.Windows;

public interface IGdiScreenshotService
{
    Bitmap Capture(nint windowHandle);
}

internal interface IGdiNative
{
    bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds);
    nint GetWindowDC(nint windowHandle);
    nint CreateCompatibleDC(nint windowDC);
    nint CreateCompatibleBitmap(nint windowDC, int width, int height);
    nint SelectObject(nint deviceContext, nint gdiObject);
    bool PrintWindow(nint windowHandle, nint destinationDC);
    Bitmap CreateManagedBitmap(nint bitmapHandle);
    void RestoreObject(nint deviceContext, nint previousObject);
    void DeleteObject(nint gdiObject);
    void DeleteDC(nint deviceContext);
    void ReleaseDC(nint windowHandle, nint deviceContext);
}
