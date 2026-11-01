using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Storage.Xps;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EvePreview.Windows;

internal sealed class Win32GdiNative : IGdiNative
{
    public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds)
    {
        if (!PInvoke.GetWindowRect(new HWND(windowHandle), out var rect))
        {
            bounds = default;
            return false;
        }

        bounds = new WindowBounds(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
        return true;
    }

    public unsafe nint GetWindowDC(nint windowHandle) =>
        (nint)PInvoke.GetWindowDC(new HWND(windowHandle)).Value;

    public unsafe nint CreateCompatibleDC(nint windowDC) =>
        (nint)PInvoke.CreateCompatibleDC(ToDC(windowDC)).Value;

    public unsafe nint CreateCompatibleBitmap(nint windowDC, int width, int height) =>
        (nint)PInvoke.CreateCompatibleBitmap(ToDC(windowDC), width, height).Value;

    public unsafe nint SelectObject(nint deviceContext, nint gdiObject) =>
        (nint)PInvoke.SelectObject(ToDC(deviceContext), ToGdiObject(gdiObject)).Value;

    public bool PrintWindow(nint windowHandle, nint destinationDC) =>
        PInvoke.PrintWindow(new HWND(windowHandle), ToDC(destinationDC), (PRINT_WINDOW_FLAGS)2);

    public Bitmap CreateManagedBitmap(nint bitmapHandle) => Image.FromHbitmap(bitmapHandle);

    public void RestoreObject(nint deviceContext, nint previousObject) =>
        PInvoke.SelectObject(ToDC(deviceContext), ToGdiObject(previousObject));

    public void DeleteObject(nint gdiObject) => PInvoke.DeleteObject(ToGdiObject(gdiObject));

    public void DeleteDC(nint deviceContext) => PInvoke.DeleteDC(ToDC(deviceContext));

    public void ReleaseDC(nint windowHandle, nint deviceContext) =>
        PInvoke.ReleaseDC(new HWND(windowHandle), ToDC(deviceContext));

    private static unsafe HDC ToDC(nint handle) => new((void*)handle);

    private static unsafe HGDIOBJ ToGdiObject(nint handle) => new((void*)handle);
}
