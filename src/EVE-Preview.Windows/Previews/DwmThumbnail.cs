using System.Runtime.InteropServices;

namespace EvePreview.Windows;

public sealed class DwmThumbnail : IDwmThumbnail
{
    private readonly IDwmNative _native;
    private nint _handle;

    public DwmThumbnail(nint sourceWindow, nint destinationWindow)
        : this(sourceWindow, destinationWindow, new Win32DwmNative())
    {
    }

    internal DwmThumbnail(nint sourceWindow, nint destinationWindow, IDwmNative native)
    {
        if (sourceWindow == 0)
        {
            throw new ArgumentException("A source window is required.", nameof(sourceWindow));
        }

        if (destinationWindow == 0)
        {
            throw new ArgumentException("A destination window is required.", nameof(destinationWindow));
        }

        SourceWindow = sourceWindow;
        DestinationWindow = destinationWindow;
        _native = native;
        var registration = native.Register(sourceWindow, destinationWindow);
        Marshal.ThrowExceptionForHR(registration.HResult);
        _handle = registration.Handle;
    }

    public nint SourceWindow { get; }
    public nint DestinationWindow { get; }

    public void Update(DwmThumbnailProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ObjectDisposedException.ThrowIf(_handle == 0, this);
        Marshal.ThrowExceptionForHR(_native.Update(_handle, properties));
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
        {
            try
            {
                _native.Unregister(handle);
            }
            catch (Exception exception) when (
                exception is COMException or ArgumentException or InvalidOperationException)
            {
                // The source/destination window may already be gone during client exit.
            }
        }
    }
}
