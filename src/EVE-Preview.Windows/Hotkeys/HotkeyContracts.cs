namespace EvePreview.Windows;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public enum MouseSideButton
{
    None = 0,
    XButton1 = 1,
    XButton2 = 2
}

public sealed record Hotkey(
    HotkeyModifiers Modifiers,
    int VirtualKey = 0,
    MouseSideButton MouseButton = MouseSideButton.None);

public interface IHotkeyRegistration : IDisposable
{
    Hotkey Hotkey { get; }
}

public sealed record HotkeyRegistrationResult(
    bool Succeeded,
    string? Error,
    IHotkeyRegistration? Registration);

public interface IGlobalHotkeyService : IDisposable
{
    HotkeyRegistrationResult Register(Hotkey hotkey, Func<CancellationToken, Task> action);
}

internal readonly record struct HotkeyNativeResult(bool Succeeded, string? Error);

internal interface IHotkeyNative : IDisposable
{
    void Start(Action<int> keyboardCallback, Action<MouseSideButton, HotkeyModifiers> mouseCallback);
    HotkeyNativeResult Register(int registrationId, Hotkey hotkey);
    void Unregister(int registrationId, Hotkey hotkey);
}
