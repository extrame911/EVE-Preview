using System.Windows.Threading;
using EvePreview.Core;
using EvePreview.Windows;
using Application = System.Windows.Application;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime : IPreviewRuntime, IPreviewRuntimeController, IClientWindowLayoutController, IPreviewRuntimeStatusSource, IPreviewRuntimeSettingsRecovery, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly JsonSettingsStore _store;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly ProcessSnapshotMonitor _processMonitor = new();
    private readonly ClientSnapshotDiffer _differ = new();
    private readonly Dictionary<ClientIdentity, PreviewWindow> _previews = [];
    private readonly List<IHotkeyRegistration> _registrations = [];
    private readonly PreviewRuntimeSnapshot _emptySnapshot = new([], null, null, 0, 0);
    private AppSettings _settings;
    private AppSettings _lastPersistedSettings;
    private IWindowOperationQueue? _windowQueue;
    private GlobalHotkeyService? _hotkeys;
    private CancellationTokenSource? _lifetime;
    private Task? _pollTask;
    private ClientIdentity? _activeIdentity;
    private ClientIdentity? _focusedIdentity;
    private nint _previousForeignWindow;
    private int _previousForeignWindowProcessId;
    private bool _paused;
    private bool _started;
    private bool _disposed;
    private bool _uiResourcesDisposed;
    private int _saveVersion;
    private string? _lastError;
    private string? _lastSaveError;
    private RenderingMode _appliedRenderingMode;
    private int _appliedDefaultWidth;
    private int _appliedDefaultHeight;
    private bool _appliedHideWhenOutOfBounds;
    private int _appliedExpansionThreshold;
    private readonly Dictionary<string, string> _renamedClientTitles = new(StringComparer.OrdinalIgnoreCase);
    private PreviewRuntimeSnapshot _currentSnapshot;
    public ClientPreviewRuntime(
        AppSettings settings,
        JsonSettingsStore store,
        IWindowOperationQueue? windowQueue = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _lastPersistedSettings = settings.DeepCopy();
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _windowQueue = windowQueue;
        _dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("The preview runtime must be created on a WPF application thread.");
        _currentSnapshot = _emptySnapshot;
        _appliedRenderingMode = _settings.Compatibility.RenderingMode;
        _appliedDefaultWidth = _settings.Preview.DefaultWidth;
        _appliedDefaultHeight = _settings.Preview.DefaultHeight;
        _appliedHideWhenOutOfBounds = _settings.Display.HideWhenOutOfBounds;
        _appliedExpansionThreshold = _settings.Display.ExpansionThreshold;
    }

    public PreviewRuntimeSnapshot CurrentSnapshot => _currentSnapshot;

    public event EventHandler<PreviewRuntimeSnapshot>? SnapshotChanged;

    public AppSettings Settings => _settings;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_started)
            {
                if (_paused)
                {
                    await ResumeCoreAsync(cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            await _dispatcher.InvokeAsync(
                () =>
                {
                    _paused = false;
                    _uiResourcesDisposed = false;
                    _lifetime = new CancellationTokenSource();
                    try
                    {
                        EnsureUiServices();
                        RebuildHotkeys();
                        _started = true;
                    }
                    catch
                    {
                        DisposeUiResources();
                        _lifetime.Dispose();
                        _lifetime = null;
                        throw;
                    }
                },
                DispatcherPriority.Normal,
                cancellationToken).Task.ConfigureAwait(false);

            _pollTask = PollAsync(_lifetime!.Token);
            await PollOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_started || _paused)
            {
                return;
            }

            await _dispatcher.InvokeAsync(
                () =>
                {
                    _paused = true;
                    foreach (var preview in _previews.Values)
                    {
                        preview.SetPaused(true);
                    }

                    PublishSnapshot(null);
                },
                DispatcherPriority.Normal,
                cancellationToken).Task.ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_started)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_started)
            {
                return;
            }

            await ResumeCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task ResumeCoreAsync(CancellationToken cancellationToken)
    {
        await _dispatcher.InvokeAsync(
            () =>
            {
                _paused = false;
                foreach (var preview in _previews.Values)
                {
                    preview.SetPaused(false);
                }

                PublishSnapshot(null);
            },
            DispatcherPriority.Normal,
            cancellationToken).Task.ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            Interlocked.Increment(ref _saveVersion);
            _lifetime?.Cancel();
            var pollTask = _pollTask;
            if (pollTask is not null)
            {
                try
                {
                    await pollTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
                {
                }
            }

            await _dispatcher.InvokeAsync(
                DisposeUiResources,
                DispatcherPriority.Normal,
                CancellationToken.None).Task.ConfigureAwait(false);
            await SaveSynchronouslyAsync(CancellationToken.None).ConfigureAwait(false);
            _lifetime?.Dispose();
            _lifetime = null;
            _pollTask = null;
            PublishSnapshot(null);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public Task MinimizeAllAsync(CancellationToken cancellationToken = default) =>
        ExecuteForAllAsync(WindowOperationKind.Minimize, cancellationToken);

    public Task RestoreAllAsync(CancellationToken cancellationToken = default) =>
        ExecuteForAllAsync(WindowOperationKind.Restore, cancellationToken);

    private void EnsureUiServices()
    {
        _windowQueue ??= new WindowOperationQueue();
        if (_hotkeys is not null)
        {
            return;
        }

        try
        {
            _hotkeys = new GlobalHotkeyService();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowError($"全局热键不可用：{exception.Message}");
        }
    }
}
