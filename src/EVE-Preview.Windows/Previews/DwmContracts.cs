namespace EvePreview.Windows;

public sealed record DwmThumbnailProperties(
    WindowBounds Destination,
    byte Opacity = 255,
    bool Visible = true,
    bool SourceClientAreaOnly = true,
    WindowBounds? Source = null);

public interface IDwmThumbnail : IDisposable
{
    nint SourceWindow { get; }
    nint DestinationWindow { get; }
    void Update(DwmThumbnailProperties properties);
}

internal readonly record struct NativeDwmRegistration(int HResult, nint Handle);

internal interface IDwmNative
{
    NativeDwmRegistration Register(nint sourceWindow, nint destinationWindow);
    int Update(nint thumbnailHandle, DwmThumbnailProperties properties);
    void Unregister(nint thumbnailHandle);
}
