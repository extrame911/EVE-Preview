using System.Runtime.InteropServices;
using EvePreview.Core;

namespace EvePreview.App.Services;

public sealed record MonitorGeometry(
    PixelRectangle WorkArea,
    double DpiScaleX,
    double DpiScaleY)
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MonitorDpiTypeEffective = 0;
    private const uint DefaultDpi = 96;

    public static MonitorGeometry? FromPoint(double x, double y) =>
        FromMonitor(MonitorFromPoint(
            new NativePoint((int)Math.Round(x), (int)Math.Round(y)),
            MonitorDefaultToNearest));

    public static MonitorGeometry? FromRectangleCenter(PixelRectangle bounds) =>
        FromPoint(
            bounds.X + bounds.Width / 2.0,
            bounds.Y + bounds.Height / 2.0);

    private static MonitorGeometry? FromMonitor(nint monitor)
    {
        if (monitor == 0)
        {
            return null;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return null;
        }

        var dpiX = DefaultDpi;
        var dpiY = DefaultDpi;
        if (GetDpiForMonitor(monitor, MonitorDpiTypeEffective, out var monitorDpiX, out var monitorDpiY) != 0 ||
            monitorDpiX == 0 ||
            monitorDpiY == 0)
        {
            dpiX = GetDpiForSystem();
            dpiY = dpiX;
        }
        else
        {
            dpiX = monitorDpiX;
            dpiY = monitorDpiY;
        }

        if (dpiX == 0)
        {
            dpiX = DefaultDpi;
        }

        if (dpiY == 0)
        {
            dpiY = DefaultDpi;
        }

        var work = info.WorkArea;
        return new MonitorGeometry(
            new PixelRectangle(
                work.Left,
                work.Top,
                Math.Max(1, work.Right - work.Left),
                Math.Max(1, work.Bottom - work.Top)),
            dpiX / (double)DefaultDpi,
            dpiY / (double)DefaultDpi);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
