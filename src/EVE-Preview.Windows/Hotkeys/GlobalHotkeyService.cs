using System.Threading.Channels;

namespace EvePreview.Windows;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private readonly IHotkeyNative _native;
    private readonly Dictionary<int, HotkeyBinding> _bindings = [];
    private readonly Channel<Func<CancellationToken, Task>> _actions = Channel.CreateUnbounded<Func<CancellationToken, Task>>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _consumer;
    private int _nextId = 1;
    private bool _disposed;

    public GlobalHotkeyService() : this(new Win32HotkeyNative())
    {
    }

    internal GlobalHotkeyService(IHotkeyNative native)
    {
        _native = native;
        _consumer = Task.Factory.StartNew(
            ConsumeActionsAsync,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default).Unwrap();
        try
        {
            _native.Start(OnKeyboard, OnMouse);
        }
        catch
        {
            _lifetime.Cancel();
            _actions.Writer.TryComplete();
            _consumer.Wait(TimeSpan.FromSeconds(1));
            _native.Dispose();
            _lifetime.Dispose();
            throw;
        }
    }

    public HotkeyRegistrationResult Register(Hotkey hotkey, Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(action);
        int id;
        lock (_bindings)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_bindings.Values.Any(binding => binding.Hotkey == hotkey))
            {
                return new HotkeyRegistrationResult(false, "The hotkey is already registered.", null);
            }

            id = _nextId;
        }

        // Never hold _bindings while calling the native layer: WM_HOTKEY can arrive on the
        // dedicated hotkey thread and would otherwise deadlock on the same lock.
        var result = _native.Register(id, hotkey);
        if (!result.Succeeded)
        {
            return new HotkeyRegistrationResult(false, result.Error, null);
        }

        var shuttingDown = false;
        lock (_bindings)
        {
            if (_disposed)
            {
                shuttingDown = true;
            }
            else if (_bindings.Values.Any(binding => binding.Hotkey == hotkey))
            {
                // Another caller registered the same hotkey while the native call was in flight.
            }
            else
            {
                var registration = new HotkeyRegistration(hotkey, id, Unregister);
                _bindings.Add(id, new HotkeyBinding(hotkey, action, registration));
                _nextId = Math.Max(_nextId, id + 1);
                return new HotkeyRegistrationResult(true, null, registration);
            }
        }

        _native.Unregister(id, hotkey);
        return shuttingDown
            ? new HotkeyRegistrationResult(false, "The hotkey service is shutting down.", null)
            : new HotkeyRegistrationResult(false, "The hotkey is already registered.", null);
    }

    public void Dispose()
    {
        HotkeyBinding[] bindings;
        lock (_bindings)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            bindings = _bindings.Values.ToArray();
            _bindings.Clear();
        }

        _lifetime.Cancel();
        _actions.Writer.TryComplete();
        foreach (var binding in bindings)
        {
            _native.Unregister(binding.Registration.Id, binding.Hotkey);
            binding.Registration.MarkDisposed();
        }

        _native.Dispose();
        if (_consumer.Wait(TimeSpan.FromSeconds(1)))
        {
            _lifetime.Dispose();
        }
        else
        {
            // Never block WPF shutdown indefinitely if a consumer is waiting on the Dispatcher.
            _ = _consumer.ContinueWith(_ => _lifetime.Dispose(), TaskScheduler.Default);
        }
    }

    private void OnKeyboard(int registrationId)
    {
        lock (_bindings)
        {
            if (!_disposed && _bindings.TryGetValue(registrationId, out var binding))
            {
                _actions.Writer.TryWrite(binding.Action);
            }
        }
    }

    private void OnMouse(MouseSideButton button, HotkeyModifiers modifiers)
    {
        lock (_bindings)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var binding in _bindings.Values)
            {
                if (binding.Hotkey.MouseButton == button && binding.Hotkey.Modifiers == modifiers)
                {
                    _actions.Writer.TryWrite(binding.Action);
                }
            }
        }
    }

    private async Task ConsumeActionsAsync()
    {
        await foreach (var action in _actions.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await action(_lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                // One failing program action must not terminate hotkey dispatch.
            }
        }
    }

    private void Unregister(int registrationId)
    {
        HotkeyBinding? binding;
        bool disposed;
        lock (_bindings)
        {
            if (!_bindings.Remove(registrationId, out binding))
            {
                return;
            }

            disposed = _disposed;
        }

        if (!disposed)
        {
            _native.Unregister(registrationId, binding.Hotkey);
        }
    }

    private sealed record HotkeyBinding(
        Hotkey Hotkey,
        Func<CancellationToken, Task> Action,
        HotkeyRegistration Registration);
}

internal sealed class HotkeyRegistration : IHotkeyRegistration
{
    private readonly Action<int> _unregister;
    private int _disposed;

    internal HotkeyRegistration(Hotkey hotkey, int id, Action<int> unregister)
    {
        Hotkey = hotkey;
        Id = id;
        _unregister = unregister;
    }

    public Hotkey Hotkey { get; }
    internal int Id { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _unregister(Id);
        }
    }

    internal void MarkDisposed() => Interlocked.Exchange(ref _disposed, 1);
}
