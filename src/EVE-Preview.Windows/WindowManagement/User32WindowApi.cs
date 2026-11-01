namespace EvePreview.Windows;

internal readonly record struct NativeMessageResult(bool Succeeded, int? ErrorCode);

internal interface IUser32Native
{
    bool IsWindow(nint windowHandle);
    bool IsIconic(nint windowHandle);
    bool IsZoomed(nint windowHandle);
    int GetWindowProcessId(nint windowHandle);
    bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds);
    nint GetForegroundWindow();
    bool SetForegroundWindow(nint windowHandle);
    NativeMessageResult SendSysCommand(nint windowHandle, nint command, uint timeoutMilliseconds);
    bool SetWindowPosition(nint windowHandle, WindowBounds bounds);
}

public sealed class User32WindowApi : IWindowApi
{
    private readonly IUser32Native native;
    private const uint MessageTimeoutMilliseconds = 500;
    private const nint ScMinimize = 0xF020;
    private const nint ScMaximize = 0xF030;
    private const nint ScRestore = 0xF120;
    private const int ErrorTimeout = 1460;

    public User32WindowApi() : this(new Win32User32Native())
    {
    }

    internal User32WindowApi(IUser32Native native)
    {
        this.native = native;
    }

    public nint GetForegroundWindow() => native.GetForegroundWindow();

    public bool IsMinimized(nint windowHandle) => native.IsIconic(windowHandle);

    public int GetWindowProcessId(nint windowHandle) => native.GetWindowProcessId(windowHandle);

    public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds) =>
        native.TryGetWindowBounds(windowHandle, out bounds);

    public WindowOperationResult Activate(nint windowHandle)
    {
        if (!native.IsWindow(windowHandle))
        {
            return WindowOperationResult.Failure(WindowOperationError.InvalidWindow);
        }

        if (native.IsIconic(windowHandle))
        {
            var restored = SendSysCommand(windowHandle, ScRestore);
            if (!restored.Succeeded)
            {
                return restored;
            }
        }

        var activated = false;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            _ = native.SetForegroundWindow(windowHandle);
            if (native.GetForegroundWindow() == windowHandle)
            {
                activated = true;
                break;
            }

            if (attempt < 3)
            {
                Thread.Sleep(25);
            }
        }

        if (!activated)
        {
            return WindowOperationResult.Failure(
                WindowOperationError.ActivationRejected,
                message: "Windows did not make the requested window foreground.");
        }

        return WindowOperationResult.Success();
    }

    public WindowOperationResult Restore(nint windowHandle)
    {
        if (!native.IsWindow(windowHandle))
        {
            return WindowOperationResult.Failure(WindowOperationError.InvalidWindow);
        }

        return native.IsIconic(windowHandle) || native.IsZoomed(windowHandle)
            ? SendSysCommand(windowHandle, ScRestore)
            : WindowOperationResult.Success();
    }

    public WindowOperationResult Minimize(nint windowHandle) => SendSysCommand(windowHandle, ScMinimize);

    public WindowOperationResult Maximize(nint windowHandle) => SendSysCommand(windowHandle, ScMaximize);

    public WindowOperationResult MoveResize(nint windowHandle, WindowBounds bounds) =>
        native.IsWindow(windowHandle) && native.SetWindowPosition(windowHandle, bounds)
            ? WindowOperationResult.Success()
            : WindowOperationResult.Failure(WindowOperationError.NativeFailure);

    private WindowOperationResult SendSysCommand(nint windowHandle, nint command)
    {
        if (!native.IsWindow(windowHandle))
        {
            return WindowOperationResult.Failure(WindowOperationError.InvalidWindow);
        }

        var result = native.SendSysCommand(windowHandle, command, MessageTimeoutMilliseconds);
        if (result.Succeeded)
        {
            return WindowOperationResult.Success();
        }

        return WindowOperationResult.Failure(
            result.ErrorCode == ErrorTimeout ? WindowOperationError.Timeout : WindowOperationError.NativeFailure,
            result.ErrorCode);
    }
}
