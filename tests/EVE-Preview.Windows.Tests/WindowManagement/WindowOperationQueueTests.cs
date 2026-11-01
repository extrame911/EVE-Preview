namespace EvePreview.Windows.Tests;

public sealed class WindowOperationQueueTests
{
    [Fact]
    public async Task EnqueueAsync_executes_rapid_operations_in_order_on_one_consumer()
    {
        var api = new FakeWindowApi();
        await using var queue = new WindowOperationQueue(api);

        var first = queue.EnqueueAsync(new WindowOperationRequest(WindowOperationKind.Activate, (nint)1));
        await api.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = queue.EnqueueAsync(new WindowOperationRequest(WindowOperationKind.Minimize, (nint)2));
        var third = queue.EnqueueAsync(new WindowOperationRequest(
            WindowOperationKind.MoveResize,
            (nint)3,
            new WindowBounds(10, 20, 300, 200)));
        api.ReleaseFirst.TrySetResult();

        var results = await Task.WhenAll(first, second, third).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(
            [WindowOperationKind.Activate, WindowOperationKind.Minimize, WindowOperationKind.MoveResize],
            api.ExecutedOperations);
        Assert.Single(api.ConsumerThreadIds.Distinct());
        Assert.Equal(1, api.MaximumConcurrency);
    }

    [Fact]
    public async Task EnqueueAsync_cancellation_completes_a_queued_operation()
    {
        var api = new FakeWindowApi();
        await using var queue = new WindowOperationQueue(api);
        var first = queue.EnqueueAsync(new WindowOperationRequest(WindowOperationKind.Activate, (nint)1));
        await api.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        var second = queue.EnqueueAsync(
            new WindowOperationRequest(WindowOperationKind.Minimize, (nint)2),
            cancellation.Token);

        try
        {
            await cancellation.CancelAsync();
            var result = await second.WaitAsync(TimeSpan.FromMilliseconds(200));
            Assert.Equal(WindowOperationError.Canceled, result.Error);
        }
        finally
        {
            api.ReleaseFirst.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task A_native_exception_fails_only_that_request_and_the_queue_continues()
    {
        var api = new FakeWindowApi { ThrowOnActivate = true };
        await using var queue = new WindowOperationQueue(api);

        var failed = await queue.EnqueueAsync(new WindowOperationRequest(
            WindowOperationKind.Activate,
            (nint)1,
            ExpectedProcessId: 42));
        var succeeded = await queue.EnqueueAsync(new WindowOperationRequest(
            WindowOperationKind.Minimize,
            (nint)2,
            ExpectedProcessId: 42));

        Assert.False(failed.Succeeded);
        Assert.Equal(WindowOperationError.NativeFailure, failed.Error);
        Assert.True(succeeded.Succeeded);
    }

    [Fact]
    public async Task Mismatched_process_identity_is_rejected_before_touching_the_window()
    {
        var api = new FakeWindowApi { WindowProcessId = 77 };
        await using var queue = new WindowOperationQueue(api);

        var result = await queue.EnqueueAsync(new WindowOperationRequest(
            WindowOperationKind.Minimize,
            (nint)123,
            ExpectedProcessId: 42));

        Assert.False(result.Succeeded);
        Assert.Equal(WindowOperationError.InvalidWindow, result.Error);
        Assert.Empty(api.ExecutedOperations);
    }

    [Fact]
    public async Task System_animation_suppression_is_scoped_to_the_operation()
    {
        var api = new FakeWindowApi();
        var animation = new FakeAnimationSuppression();
        await using var queue = new WindowOperationQueue(api, animation);

        var result = await queue.EnqueueAsync(new WindowOperationRequest(
            WindowOperationKind.Minimize,
            (nint)123,
            ExpectedProcessId: 42,
            SuppressSystemAnimations: true));

        Assert.True(result.Succeeded);
        Assert.Equal(1, animation.SuppressCalls);
        Assert.Equal(1, animation.DisposeCalls);
    }

    private sealed class FakeAnimationSuppression : ISystemAnimationSuppression
    {
        public int SuppressCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public IDisposable Suppress()
        {
            SuppressCalls++;
            return new CallbackDisposable(() => DisposeCalls++);
        }

        private sealed class CallbackDisposable(Action callback) : IDisposable
        {
            public void Dispose() => callback();
        }
    }

    private sealed class FakeWindowApi : IWindowApi
    {
        private int _activeOperations;

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<WindowOperationKind> ExecutedOperations { get; } = [];
        public List<int> ConsumerThreadIds { get; } = [];
        public int MaximumConcurrency { get; private set; }
        public bool ThrowOnActivate { get; init; }
        public int WindowProcessId { get; init; } = 42;

        public WindowOperationResult Activate(nint windowHandle)
        {
            if (ThrowOnActivate)
            {
                throw new InvalidOperationException("native activate failed");
            }

            return Execute(WindowOperationKind.Activate, block: true);
        }
        public nint GetForegroundWindow() => 0;
        public bool IsMinimized(nint windowHandle) => false;
        public int GetWindowProcessId(nint windowHandle) => WindowProcessId;
        public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds)
        {
            bounds = new WindowBounds(10, 20, 300, 200);
            return true;
        }
        public WindowOperationResult Restore(nint windowHandle) => Execute(WindowOperationKind.Restore);
        public WindowOperationResult Minimize(nint windowHandle) => Execute(WindowOperationKind.Minimize);
        public WindowOperationResult Maximize(nint windowHandle) => Execute(WindowOperationKind.Maximize);
        public WindowOperationResult MoveResize(nint windowHandle, WindowBounds bounds) => Execute(WindowOperationKind.MoveResize);

        private WindowOperationResult Execute(WindowOperationKind kind, bool block = false)
        {
            var active = Interlocked.Increment(ref _activeOperations);
            MaximumConcurrency = Math.Max(MaximumConcurrency, active);
            ConsumerThreadIds.Add(Environment.CurrentManagedThreadId);
            ExecutedOperations.Add(kind);
            if (block)
            {
                FirstStarted.TrySetResult();
                ReleaseFirst.Task.GetAwaiter().GetResult();
            }

            Interlocked.Decrement(ref _activeOperations);
            return WindowOperationResult.Success();
        }
    }
}
