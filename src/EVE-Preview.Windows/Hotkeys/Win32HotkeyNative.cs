using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EvePreview.Windows;

internal sealed partial class Win32HotkeyNative : IHotkeyNative
{
    private const int WmHotkey = 0x0312;
    private const int WmXButtonDown = 0x020B;
    private const int WhMouseLl = 14;
    private const uint ModNoRepeat = 0x4000;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private static readonly nint HwndMessage = new(-3);

    private readonly HwndSourceHook _windowHook;
    private readonly LowLevelMouseProc _mouseProc;
    private readonly HashSet<int> _registeredKeyboardIds = [];
    private readonly object _gate = new();
    private Thread? _thread;
    private Dispatcher? _threadDispatcher;
    private HwndSource? _messageWindow;
    private DispatcherTimer? _hookHealthTimer;
    private nint _mouseHook;
    private Action<int>? _keyboardCallback;
    private Action<MouseSideButton, HotkeyModifiers>? _mouseCallback;
    private bool _started;
    private bool _disposed;

    public Win32HotkeyNative()
    {
        _windowHook = OnWindowMessage;
        _mouseProc = OnMouseMessage;
    }

    public void Start(
        Action<int> keyboardCallback,
        Action<MouseSideButton, HotkeyModifiers> mouseCallback)
    {
        ArgumentNullException.ThrowIfNull(keyboardCallback);
        ArgumentNullException.ThrowIfNull(mouseCallback);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            throw new InvalidOperationException("The native hotkey service has already started.");
        }

        _keyboardCallback = keyboardCallback;
        _mouseCallback = mouseCallback;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() => RunMessageThread(ready))
        {
            Name = "EVE-Preview.Hotkeys",
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!ready.Task.Wait(TimeSpan.FromSeconds(3)))
        {
            throw new TimeoutException("The dedicated hotkey message thread did not initialize in time.");
        }

        try
        {
            ready.Task.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Unable to initialize the dedicated hotkey message thread.", exception);
        }
    }

    public HotkeyNativeResult Register(int registrationId, Hotkey hotkey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started)
        {
            return new HotkeyNativeResult(false, "The native hotkey service is not started.");
        }

        var result = new HotkeyNativeResult(false, "The hotkey message thread is unavailable.");
        InvokeOnThread(() => result = RegisterCore(registrationId, hotkey));
        return result;
    }

    public void Unregister(int registrationId, Hotkey hotkey)
    {
        if (_disposed)
        {
            return;
        }

        InvokeOnThread(() => UnregisterCore(registrationId, hotkey));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var dispatcher = _threadDispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted &&
            !dispatcher.HasShutdownFinished)
        {
            try
            {
                if (dispatcher.CheckAccess())
                {
                    DisposeCore();
                }
                else
                {
                    dispatcher.Invoke(DisposeCore, DispatcherPriority.Send);
                }

                dispatcher.InvokeShutdown();
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or TaskCanceledException)
            {
            }
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _threadDispatcher = null;
    }

    private void RunMessageThread(TaskCompletionSource ready)
    {
        _threadDispatcher = Dispatcher.CurrentDispatcher;
        try
        {
            _messageWindow = new HwndSource(new HwndSourceParameters("EVE-Preview.HotkeyWindow")
            {
                WindowStyle = 0,
                ExtendedWindowStyle = 0,
                Width = 0,
                Height = 0,
                ParentWindow = HwndMessage
            });
            _messageWindow.AddHook(_windowHook);
            if (!InstallMouseHook())
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Unable to install the mouse side-button hook.");
            }

            _hookHealthTimer = new DispatcherTimer(DispatcherPriority.Background, _threadDispatcher)
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _hookHealthTimer.Tick += OnHookHealthTick;
            _hookHealthTimer.Start();
            _started = true;
            ready.TrySetResult();
            Dispatcher.Run();
        }
        catch (Exception exception)
        {
            ready.TrySetException(exception);
        }
        finally
        {
            DisposeCore();
            _started = false;
        }
    }

    private HotkeyNativeResult RegisterCore(int registrationId, Hotkey hotkey)
    {
        if (hotkey.MouseButton != MouseSideButton.None)
        {
            return new HotkeyNativeResult(true, null);
        }

        if (hotkey.VirtualKey is < 1 or > 0xFE)
        {
            return new HotkeyNativeResult(false, "The virtual key is outside the supported range.");
        }

        if (_messageWindow is null)
        {
            return new HotkeyNativeResult(false, "The hotkey message window is unavailable.");
        }

        var modifiers = ToNativeModifiers(hotkey.Modifiers) | ModNoRepeat;
        if (!RegisterHotKey(_messageWindow.Handle, registrationId, modifiers, (uint)hotkey.VirtualKey))
        {
            var error = Marshal.GetLastWin32Error();
            return new HotkeyNativeResult(false, $"RegisterHotKey failed with Win32 error {error}.");
        }

        lock (_gate)
        {
            _registeredKeyboardIds.Add(registrationId);
        }

        return new HotkeyNativeResult(true, null);
    }

    private void UnregisterCore(int registrationId, Hotkey hotkey)
    {
        if (_messageWindow is null || hotkey.MouseButton != MouseSideButton.None)
        {
            return;
        }

        lock (_gate)
        {
            if (_registeredKeyboardIds.Remove(registrationId))
            {
                UnregisterHotKey(_messageWindow.Handle, registrationId);
            }
        }
    }

    private void DisposeCore()
    {
        _hookHealthTimer?.Stop();
        _hookHealthTimer = null;
        if (_mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }

        if (_messageWindow is not null)
        {
            int[] registeredIds;
            lock (_gate)
            {
                registeredIds = _registeredKeyboardIds.ToArray();
                _registeredKeyboardIds.Clear();
            }

            foreach (var registrationId in registeredIds)
            {
                UnregisterHotKey(_messageWindow.Handle, registrationId);
            }

            _messageWindow.RemoveHook(_windowHook);
            _messageWindow.Dispose();
            _messageWindow = null;
        }

        _keyboardCallback = null;
        _mouseCallback = null;
    }

    private void OnHookHealthTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (_mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }

        _ = InstallMouseHook();
    }

    private bool InstallMouseHook()
    {
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(null), 0);
        return _mouseHook != 0;
    }

    private void InvokeOnThread(Action action)
    {
        var dispatcher = _threadDispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action, DispatcherPriority.Normal);
        }
    }

    private nint OnWindowMessage(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmHotkey && _keyboardCallback is not null)
        {
            _keyboardCallback(unchecked((int)wParam));
            handled = true;
            return 0;
        }

        return 0;
    }

    private nint OnMouseMessage(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && wParam == WmXButtonDown && _mouseCallback is not null)
        {
            var data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            if ((data.Flags & 0x00000001) != 0)
            {
                return CallNextHookEx(0, code, wParam, lParam);
            }

            var button = (MouseSideButton)((data.MouseData >> 16) & 0xFFFF);
            if (button is MouseSideButton.XButton1 or MouseSideButton.XButton2)
            {
                _mouseCallback(button, GetCurrentModifiers());
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static HotkeyModifiers GetCurrentModifiers()
    {
        var modifiers = HotkeyModifiers.None;
        if ((GetAsyncKeyState(VkControl) & 0x8000) != 0)
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if ((GetAsyncKeyState(VkMenu) & 0x8000) != 0)
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if ((GetAsyncKeyState(VkShift) & 0x8000) != 0)
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if ((GetAsyncKeyState(VkLWin) & 0x8000) != 0 ||
            (GetAsyncKeyState(VkRWin) & 0x8000) != 0)
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return modifiers;
    }

    private static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        var result = 0u;
        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            result |= 0x0001;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            result |= 0x0002;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            result |= 0x0004;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            result |= 0x0008;
        }

        return result;
    }

}
