using System.Runtime.InteropServices;

namespace EvePreview.Windows;

public interface ISystemAnimationSuppression
{
    IDisposable Suppress();
}

public sealed class SystemAnimationSuppression : ISystemAnimationSuppression
{
    private const uint SpiGetAnimation = 0x0048;
    private const uint SpiSetAnimation = 0x0049;

    public IDisposable Suppress()
    {
        var info = new AnimationInfo
        {
            Size = (uint)Marshal.SizeOf<AnimationInfo>()
        };
        if (!SystemParametersInfo(SpiGetAnimation, info.Size, ref info, 0) || info.MinAnimate == 0)
        {
            return NoOpDisposable.Instance;
        }

        var original = info.MinAnimate;
        info.MinAnimate = 0;
        if (!SystemParametersInfo(SpiSetAnimation, info.Size, ref info, 0))
        {
            return NoOpDisposable.Instance;
        }

        var restore = new AnimationInfo
        {
            Size = info.Size,
            MinAnimate = original
        };
        return new RestoreDisposable(() => SystemParametersInfo(
            SpiSetAnimation,
            restore.Size,
            ref restore,
            0));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AnimationInfo
    {
        public uint Size;
        public int MinAnimate;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        ref AnimationInfo value,
        uint update);

    private sealed class RestoreDisposable(Action restore) : IDisposable
    {
        private Action? _restore = restore;

        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static NoOpDisposable Instance { get; } = new();
        public void Dispose()
        {
        }
    }
}
