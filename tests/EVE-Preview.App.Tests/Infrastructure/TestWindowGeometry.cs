using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EvePreview.App.Tests;

internal static class TestWindowGeometry
{
    public static (int Width, int Height) GetPhysicalSize(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            (_, _) => frame.Continue = false,
            window.Dispatcher);
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (handle == 0 || !GetWindowRect(handle, out var rect))
        {
            return (0, 0);
        }

        return (Math.Max(0, rect.Right - rect.Left), Math.Max(0, rect.Bottom - rect.Top));
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
    private static extern bool GetWindowRect(nint handle, out NativeRect rect);
}
