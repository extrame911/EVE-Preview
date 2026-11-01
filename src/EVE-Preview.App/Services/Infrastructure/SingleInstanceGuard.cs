using System.Threading;

namespace EvePreview.App.Services;

/// <summary>
/// Named mutex plus activation event: a second launch focuses the already-running instance.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\EVE-Preview.SingleInstance";
    private const string ActivationEventName = @"Local\EVE-Preview.Activate";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activationEvent;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle? activationEvent)
    {
        _mutex = mutex;
        _activationEvent = activationEvent;
    }

    public void StartListening()
    {
        if (_listener is not null || _activationEvent is null)
        {
            return;
        }

        _listener = ListenAsync(_activationEvent, _cancellation.Token);
    }

    public static SingleInstanceGuard? TryAcquire(out bool alreadyRunning)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            alreadyRunning = true;
            return null;
        }

        alreadyRunning = false;
        var activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName);
        return new SingleInstanceGuard(mutex, activationEvent);
    }

    public static void SignalExistingInstance()
    {
        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
            activationEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public event EventHandler? ActivationRequested;

    private async Task ListenAsync(EventWaitHandle activationEvent, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Run(() => activationEvent.WaitOne(), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            ActivationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _activationEvent?.Set();
        _activationEvent?.Dispose();
        _listener?.Wait(TimeSpan.FromSeconds(1));
        _cancellation.Dispose();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
    }
}
