namespace EvePreview.Windows.Tests;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void Registrations_and_service_dispose_explicitly_unregister_native_resources()
    {
        var native = new FakeHotkeyNative();
        var service = new GlobalHotkeyService(native);
        var keyboard = service.Register(
            new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x70),
            _ => Task.CompletedTask);
        var mouse = service.Register(
            new Hotkey(HotkeyModifiers.Control, 0, MouseSideButton.XButton1),
            _ => Task.CompletedTask);

        keyboard.Registration!.Dispose();
        keyboard.Registration.Dispose();
        service.Dispose();
        service.Dispose();

        Assert.True(keyboard.Succeeded);
        Assert.True(mouse.Succeeded);
        Assert.Equal([1, 2], native.UnregisteredIds);
        Assert.Equal(1, native.DisposeCount);
    }

    [Fact]
    public async Task Mouse_side_button_callback_only_enqueues_the_program_action()
    {
        var native = new FakeHotkeyNative();
        using var service = new GlobalHotkeyService(native);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = service.Register(
            new Hotkey(HotkeyModifiers.Control, 0, MouseSideButton.XButton1),
            _ =>
            {
                started.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return Task.CompletedTask;
            });
        Assert.True(registration.Succeeded);

        var callback = Task.Run(() => native.MouseCallback!(MouseSideButton.XButton1, HotkeyModifiers.Control));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await callback.WaitAsync(TimeSpan.FromMilliseconds(200));
        Assert.False(release.Task.IsCompleted);
        release.TrySetResult();
    }

    [Fact]
    public async Task Keyboard_hotkey_triggers_its_registered_action()
    {
        var native = new FakeHotkeyNative();
        using var service = new GlobalHotkeyService(native);
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = service.Register(
            new Hotkey(HotkeyModifiers.Alt, 0x71),
            _ =>
            {
                invoked.TrySetResult();
                return Task.CompletedTask;
            });
        Assert.True(registration.Succeeded);

        native.KeyboardCallback!(1);

        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Registering_the_same_mouse_side_button_twice_is_rejected()
    {
        var native = new FakeHotkeyNative();
        using var service = new GlobalHotkeyService(native);
        var hotkey = new Hotkey(HotkeyModifiers.Control, 0, MouseSideButton.XButton1);

        var first = service.Register(hotkey, _ => Task.CompletedTask);
        var second = service.Register(hotkey, _ => Task.CompletedTask);

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.NotNull(second.Error);
    }

    private sealed class FakeHotkeyNative : IHotkeyNative
    {
        public List<int> UnregisteredIds { get; } = [];
        public int DisposeCount { get; private set; }
        public Action<int>? KeyboardCallback { get; private set; }
        public Action<MouseSideButton, HotkeyModifiers>? MouseCallback { get; private set; }

        public void Start(Action<int> keyboardCallback, Action<MouseSideButton, HotkeyModifiers> mouseCallback)
        {
            KeyboardCallback = keyboardCallback;
            MouseCallback = mouseCallback;
        }

        public HotkeyNativeResult Register(int registrationId, Hotkey hotkey) => new(true, null);

        public void Unregister(int registrationId, Hotkey hotkey) => UnregisteredIds.Add(registrationId);

        public void Dispose() => DisposeCount++;
    }
}
