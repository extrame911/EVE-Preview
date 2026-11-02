using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EvePreview.Windows;

internal sealed class Win32User32Native : IUser32Native
{
    private const uint WmSysCommand = 0x0112;

    public bool IsWindow(nint windowHandle) => PInvoke.IsWindow(new HWND(windowHandle));

    public bool IsIconic(nint windowHandle) => PInvoke.IsIconic(new HWND(windowHandle));
    public bool IsZoomed(nint windowHandle) => PInvoke.IsZoomed(new HWND(windowHandle));

    public int GetWindowProcessId(nint windowHandle)
    {
        unsafe
        {
            _ = PInvoke.GetWindowThreadProcessId(new HWND(windowHandle), out var processId);
            return (int)processId;
        }
    }

    public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds)
    {
        if (!PInvoke.GetWindowRect(new HWND(windowHandle), out var rectangle))
        {
            bounds = default;
            return false;
        }

        bounds = new WindowBounds(
            rectangle.left,
            rectangle.top,
            rectangle.right - rectangle.left,
            rectangle.bottom - rectangle.top);
        return true;
    }

    public unsafe nint GetForegroundWindow() => (nint)PInvoke.GetForegroundWindow().Value;

    public bool SetForegroundWindow(nint windowHandle) =>
        PInvoke.SetForegroundWindow(new HWND(windowHandle));

    public unsafe NativeMessageResult SendSysCommand(
        nint windowHandle,
        nint command,
        uint timeoutMilliseconds)
    {
        var result = PInvoke.SendMessageTimeout(
            new HWND(windowHandle),
            WmSysCommand,
            new WPARAM((nuint)command),
            new LPARAM(0),
            SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG | SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_BLOCK,
            timeoutMilliseconds,
            out _);
        return result.Value == 0
            ? new NativeMessageResult(false, Marshal.GetLastWin32Error())
            : new NativeMessageResult(true, null);
    }

    public bool SetWindowPosition(nint windowHandle, WindowBounds bounds)
    {
        // The window-operation queue runs on a per-monitor-aware thread. Passing native
        // pixel rectangles from that context makes Windows/EVE apply a per-monitor DPI
        // adjustment when the window is near a negative-coordinate monitor seam. Apply
        // the saved native rectangle from a system-aware context instead, then restore
        // the worker thread's original awareness.
        var previousContext = SetThreadDpiAwarenessContext(SystemAwareDpiContext);
        try
        {
            return PInvoke.SetWindowPos(
                new HWND(windowHandle),
                new HWND(0),
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
                SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS |
                SET_WINDOW_POS_FLAGS.SWP_NOSENDCHANGING);
        }
        finally
        {
            if (previousContext != 0)
            {
                _ = SetThreadDpiAwarenessContext(previousContext);
            }
        }
    }

    private const nint SystemAwareDpiContext = -2;

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint dpiContext);
}
