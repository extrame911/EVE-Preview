using System.Runtime.InteropServices;
using System.Windows.Interop;
using EvePreview.Core;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const int WmWindowPosChanging = 0x0046;

    private nint NativeMessageHook(
        nint windowHandle,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmWindowPosChanging &&
            _rightDown &&
            _physicalDragTarget is { } target &&
            lParam != 0)
        {
            var position = Marshal.PtrToStructure<NativeWindowPosition>(lParam);
            position.Width = Math.Max(1, target.Width);
            position.Height = Math.Max(1, target.Height);
            Marshal.StructureToPtr(position, lParam, false);
        }

        return 0;
    }

    private void SetPhysicalWindowBounds(int x, int y, int width, int height)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        SetWindowPos(
            handle,
            0,
            x,
            y,
            Math.Max(1, width),
            Math.Max(1, height),
            SwpNoZOrder | SwpNoActivate | SwpNoOwnerZOrder);
    }

    private bool TryGetPhysicalWindowBounds(out PixelRectangle bounds)
    {
        bounds = default;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0 || !GetWindowRect(handle, out var rect))
        {
            return false;
        }

        bounds = new PixelRectangle(
            rect.Left,
            rect.Top,
            Math.Max(1, rect.Right - rect.Left),
            Math.Max(1, rect.Bottom - rect.Top));
        return true;
    }

    private void SyncManagedSizeToPhysical()
    {
        if (!TryGetPhysicalWindowBounds(out var bounds))
        {
            return;
        }

        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        Width = bounds.Width / dpi.DpiScaleX;
        Height = bounds.Height / dpi.DpiScaleY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeWindowPosition
    {
        public nint WindowHandle;
        public nint InsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint windowHandle, ref Margins margins);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint gdiObject);
}
