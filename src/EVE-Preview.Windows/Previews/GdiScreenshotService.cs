using System.ComponentModel;
using System.Drawing;

namespace EvePreview.Windows;

public sealed class GdiScreenshotService : IGdiScreenshotService
{
    private readonly IGdiNative _native;

    public GdiScreenshotService() : this(new Win32GdiNative())
    {
    }

    internal GdiScreenshotService(IGdiNative native)
    {
        _native = native;
    }

    public Bitmap Capture(nint windowHandle)
    {
        if (!_native.TryGetWindowBounds(windowHandle, out var bounds) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("The source window has no capturable bounds.");
        }

        var windowDC = _native.GetWindowDC(windowHandle);
        if (windowDC == 0)
        {
            throw new Win32Exception();
        }

        try
        {
            var memoryDC = _native.CreateCompatibleDC(windowDC);
            if (memoryDC == 0)
            {
                throw new Win32Exception();
            }

            try
            {
                var bitmap = _native.CreateCompatibleBitmap(windowDC, bounds.Width, bounds.Height);
                if (bitmap == 0)
                {
                    throw new Win32Exception();
                }

                try
                {
                    var previousObject = _native.SelectObject(memoryDC, bitmap);
                    if (previousObject == 0 || previousObject == -1)
                    {
                        throw new Win32Exception();
                    }

                    try
                    {
                        if (!_native.PrintWindow(windowHandle, memoryDC))
                        {
                            throw new Win32Exception();
                        }

                        return _native.CreateManagedBitmap(bitmap);
                    }
                    finally
                    {
                        _native.RestoreObject(memoryDC, previousObject);
                    }
                }
                finally
                {
                    _native.DeleteObject(bitmap);
                }
            }
            finally
            {
                _native.DeleteDC(memoryDC);
            }
        }
        finally
        {
            _native.ReleaseDC(windowHandle, windowDC);
        }
    }
}
