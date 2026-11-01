namespace EvePreview.Windows.Tests;

public sealed class User32WindowApiTests
{
    [Fact]
    public void Activate_returns_failure_when_windows_rejects_foreground_activation()
    {
        var native = new FakeUser32Native
        {
            SetForegroundWindowResult = false,
            ForegroundWindow = (nint)999
        };
        var api = new User32WindowApi(native);

        var result = api.Activate((nint)123);

        Assert.False(result.Succeeded);
        Assert.Equal(WindowOperationError.ActivationRejected, result.Error);
        Assert.Equal((nint)123, native.ActivationAttempt);
    }

    [Fact]
    public void Minimize_uses_bounded_send_message_timeout_and_reports_timeout()
    {
        var native = new FakeUser32Native
        {
            SysCommandResult = new NativeMessageResult(false, 1460)
        };
        var api = new User32WindowApi(native);

        var result = api.Minimize((nint)123);

        Assert.False(result.Succeeded);
        Assert.Equal(WindowOperationError.Timeout, result.Error);
        Assert.Equal((nint)0xF020, native.SysCommand);
        Assert.Equal(500u, native.SysCommandTimeout);
    }

    [Fact]
    public void Restore_does_not_disturb_a_normal_window()
    {
        var native = new FakeUser32Native();
        var api = new User32WindowApi(native);

        var result = api.Restore((nint)123);

        Assert.True(result.Succeeded);
        Assert.Equal((nint)0, native.SysCommand);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Restore_sends_sys_command_for_minimized_or_maximized_windows(
        bool minimized,
        bool maximized)
    {
        var native = new FakeUser32Native
        {
            Minimized = minimized,
            Maximized = maximized
        };
        var api = new User32WindowApi(native);

        var result = api.Restore((nint)123);

        Assert.True(result.Succeeded);
        Assert.Equal((nint)0xF120, native.SysCommand);
    }

    private sealed class FakeUser32Native : IUser32Native
    {
        public bool Minimized { get; set; }
        public bool Maximized { get; set; }
        public nint ActivationAttempt { get; private set; }
        public nint ForegroundWindow { get; set; }
        public bool SetForegroundWindowResult { get; set; } = true;
        public NativeMessageResult SysCommandResult { get; set; } = new(true, null);
        public nint SysCommand { get; private set; }
        public uint SysCommandTimeout { get; private set; }

        public bool IsWindow(nint windowHandle) => true;
        public bool IsIconic(nint windowHandle) => Minimized;
        public bool IsZoomed(nint windowHandle) => Maximized;
        public int GetWindowProcessId(nint windowHandle) => 42;
        public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds)
        {
            bounds = new WindowBounds(100, 200, 1280, 720);
            return true;
        }
        public nint GetForegroundWindow() => ForegroundWindow;

        public bool SetForegroundWindow(nint windowHandle)
        {
            ActivationAttempt = windowHandle;
            return SetForegroundWindowResult;
        }

        public NativeMessageResult SendSysCommand(nint windowHandle, nint command, uint timeoutMilliseconds)
        {
            SysCommand = command;
            SysCommandTimeout = timeoutMilliseconds;
            return SysCommandResult;
        }

        public bool SetWindowPosition(nint windowHandle, WindowBounds bounds) => true;
    }
}
