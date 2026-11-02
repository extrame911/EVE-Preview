using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace EvePreview.App.Tests;

[CollectionDefinition("WPF Application", DisableParallelization = true)]
public sealed class WpfApplicationCollection : ICollectionFixture<WpfApplicationFixture>;

public sealed class WpfApplicationFixture : IDisposable
{
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private Exception? _startupError;

    public WpfApplicationFixture()
    {
        _thread = new Thread(() =>
        {
            try
            {
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                Dispatcher = Dispatcher.CurrentDispatcher;
                _ready.Set();
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                _startupError = exception;
                _ready.Set();
            }
        })
        {
            IsBackground = true,
            Name = "WPF test application"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("The shared WPF test application did not start.");
        }
        if (_startupError is not null)
        {
            throw _startupError;
        }
    }

    private Dispatcher Dispatcher { get; set; } = null!;

    public bool IsOnDispatcher => Dispatcher.CheckAccess();

    public void Invoke(Action action)
    {
        var operation = Dispatcher.InvokeAsync(action);
        if (!operation.Task.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("The WPF test action did not finish.");
        }
        operation.Task.GetAwaiter().GetResult();
    }

    public void Invoke(Func<Task> action)
    {
        var task = Dispatcher.InvokeAsync(action).Task.Unwrap();
        if (!task.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("The asynchronous WPF test action did not finish.");
        }
        task.GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (Dispatcher.HasShutdownStarted)
        {
            return;
        }
        Dispatcher.InvokeShutdown();
        if (!_thread.Join(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("The shared WPF test application did not stop.");
        }
        _ready.Dispose();
    }
}
