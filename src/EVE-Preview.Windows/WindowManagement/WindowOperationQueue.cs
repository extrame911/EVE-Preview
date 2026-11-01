using System.Threading.Channels;

namespace EvePreview.Windows;

public sealed class WindowOperationQueue : IWindowOperationQueue
{
    private readonly IWindowApi _windowApi;
    private readonly ISystemAnimationSuppression _animationSuppression;
    private readonly Channel<QueuedOperation> _channel;
    private readonly Task _consumer;
    private int _disposed;

    public WindowOperationQueue() : this(new User32WindowApi(), new SystemAnimationSuppression())
    {
    }

    internal WindowOperationQueue(
        IWindowApi windowApi,
        ISystemAnimationSuppression? animationSuppression = null)
    {
        _windowApi = windowApi;
        _animationSuppression = animationSuppression ?? new NoAnimationSuppression();
        _channel = Channel.CreateUnbounded<QueuedOperation>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _consumer = Task.Factory.StartNew(
            ConsumeAsync,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default).Unwrap();
    }

    public nint GetForegroundWindow() => _windowApi.GetForegroundWindow();

    public bool IsMinimized(nint windowHandle) => _windowApi.IsMinimized(windowHandle);

    public int GetWindowProcessId(nint windowHandle) => _windowApi.GetWindowProcessId(windowHandle);

    public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds) =>
        _windowApi.TryGetWindowBounds(windowHandle, out bounds);

    public Task<WindowOperationResult> EnqueueAsync(
        WindowOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var completion = new TaskCompletionSource<WindowOperationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() =>
            completion.TrySetResult(WindowOperationResult.Failure(WindowOperationError.Canceled)));
        var operation = new QueuedOperation(request, completion, registration);
        if (!_channel.Writer.TryWrite(operation))
        {
            registration.Dispose();
            completion.TrySetResult(WindowOperationResult.Failure(WindowOperationError.QueueStopped));
        }

        return completion.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();
        try
        {
            await _consumer.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            // A blocked native call must not prevent application shutdown.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();
        try
        {
            _consumer.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The window-operation failure is reported through each request result.
        }
    }
    private async Task ConsumeAsync()
    {
        await foreach (var operation in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            using (operation.CancellationRegistration)
            {
                if (operation.Completion.Task.IsCompleted)
                {
                    continue;
                }

                var result = Execute(operation.Request);
                operation.Completion.TrySetResult(result);
            }
        }
    }

    private WindowOperationResult Execute(WindowOperationRequest request)
    {
        if (request.ExpectedProcessId is int expectedProcessId &&
            _windowApi.GetWindowProcessId(request.WindowHandle) != expectedProcessId)
        {
            return WindowOperationResult.Failure(
                WindowOperationError.InvalidWindow,
                message: "The window no longer belongs to the expected client process.");
        }

        try
        {
            using var animationSuppression = request.SuppressSystemAnimations
                ? _animationSuppression.Suppress()
                : null;
            return request.Kind switch
            {
                WindowOperationKind.Activate => _windowApi.Activate(request.WindowHandle),
                WindowOperationKind.Restore => _windowApi.Restore(request.WindowHandle),
                WindowOperationKind.Minimize => _windowApi.Minimize(request.WindowHandle),
                WindowOperationKind.Maximize => _windowApi.Maximize(request.WindowHandle),
                WindowOperationKind.MoveResize => _windowApi.MoveResize(
                    request.WindowHandle,
                    request.Bounds ?? default),
                _ => WindowOperationResult.Failure(
                    WindowOperationError.NativeFailure,
                    message: "Unknown window operation.")
            };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return WindowOperationResult.Failure(
                WindowOperationError.NativeFailure,
                exception.HResult,
                exception.Message);
        }
    }
}

internal sealed record QueuedOperation(
    WindowOperationRequest Request,
    TaskCompletionSource<WindowOperationResult> Completion,
    CancellationTokenRegistration CancellationRegistration);

internal sealed class NoAnimationSuppression : ISystemAnimationSuppression
{
    public IDisposable Suppress() => EmptyDisposable.Instance;

    private sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();
        public void Dispose()
        {
        }
    }
}
