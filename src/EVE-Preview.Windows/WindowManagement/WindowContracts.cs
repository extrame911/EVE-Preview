namespace EvePreview.Windows;

public enum WindowOperationKind
{
    Activate,
    Restore,
    Minimize,
    Maximize,
    MoveResize
}

public enum WindowOperationError
{
    None,
    NativeFailure,
    Timeout,
    InvalidWindow,
    ActivationRejected,
    Canceled,
    QueueStopped
}

public readonly record struct WindowBounds(int X, int Y, int Width, int Height);

public sealed record WindowOperationRequest(
    WindowOperationKind Kind,
    nint WindowHandle,
    WindowBounds? Bounds = null,
    int? ExpectedProcessId = null,
    bool SuppressSystemAnimations = false);

public sealed record WindowOperationResult(
    bool Succeeded,
    WindowOperationError Error,
    int? NativeErrorCode,
    string? Message)
{
    public static WindowOperationResult Success() =>
        new(true, WindowOperationError.None, null, null);

    public static WindowOperationResult Failure(
        WindowOperationError error,
        int? nativeErrorCode = null,
        string? message = null) =>
        new(false, error, nativeErrorCode, message);
}

public interface IWindowOperationQueue : IAsyncDisposable, IDisposable
{
    nint GetForegroundWindow();
    bool IsMinimized(nint windowHandle);
    int GetWindowProcessId(nint windowHandle);
    bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds);
    Task<WindowOperationResult> EnqueueAsync(
        WindowOperationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWindowApi
{
    nint GetForegroundWindow();
    bool IsMinimized(nint windowHandle);
    int GetWindowProcessId(nint windowHandle);
    bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds);
    WindowOperationResult Activate(nint windowHandle);
    WindowOperationResult Restore(nint windowHandle);
    WindowOperationResult Minimize(nint windowHandle);
    WindowOperationResult Maximize(nint windowHandle);
    WindowOperationResult MoveResize(nint windowHandle, WindowBounds bounds);
}
