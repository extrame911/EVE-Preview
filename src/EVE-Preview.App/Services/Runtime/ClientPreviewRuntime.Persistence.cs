using System.Windows.Threading;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime
{
    private string? SaveOnShutdown()
    {
        var gateTaken = false;
        try
        {
            gateTaken = _saveGate.Wait(TimeSpan.FromSeconds(2));
            if (!gateTaken)
            {
                _lastError = "退出时保存配置失败：等待上一次保存超时。";
                return _lastError;
            }

            var settings = _settings.DeepCopy();
            _store.Save(settings);
            MarkSaveSucceeded(settings);
            return null;
        }
        catch (Exception exception)
        {
            _lastSaveError = $"退出时保存配置失败：{exception.Message}";
            _lastError = _lastSaveError;
            return _lastError;
        }
        finally
        {
            if (gateTaken)
            {
                _saveGate.Release();
            }
        }
    }

    private void ScheduleSave()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(ScheduleSave, DispatcherPriority.Background);
            return;
        }

        var version = Interlocked.Increment(ref _saveVersion);
        var settings = _settings.DeepCopy();
        _ = SaveAfterDelayAsync(settings, version);
    }

    private async Task SaveAfterDelayAsync(AppSettings settings, int version)
    {
        try
        {
            await Task.Delay(500).ConfigureAwait(false);
            await _saveGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (version != Volatile.Read(ref _saveVersion))
                {
                    return;
                }

                await Task.Run(() => _store.Save(settings)).ConfigureAwait(false);
                MarkSaveSucceeded(settings);
            }
            finally
            {
                _saveGate.Release();
            }

            await _dispatcher.InvokeAsync(
                () => PublishSnapshot(null),
                DispatcherPriority.Background).Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _lastSaveError = $"保存配置失败：{exception.Message}";
            _lastError = _lastSaveError;
            await _dispatcher.InvokeAsync(
                () => PublishSnapshot(null),
                DispatcherPriority.Background).Task.ConfigureAwait(false);
        }
    }

    private async Task SaveSynchronouslyAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.DeepCopy();
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() => _store.Save(settings), cancellationToken).ConfigureAwait(false);
            MarkSaveSucceeded(settings);
        }
        catch (Exception exception)
        {
            _lastSaveError = $"退出时保存配置失败：{exception.Message}";
            await ReportErrorAsync(_lastSaveError, CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            _saveGate.Release();
        }
    }
    public async Task<AppSettings> RestoreLastSavedSettingsAsync(
        AppSettings target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ThrowIfDisposed();
        Interlocked.Increment(ref _saveVersion);
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var restored = _lastPersistedSettings.DeepCopy();
            await _dispatcher.InvokeAsync(
                () =>
                {
                    Interlocked.Increment(ref _saveVersion);
                    target.CopyFrom(restored);
                    ApplySettingsCore(target, updatePlacementsForDefaultSizeChange: false);
                    if (_lastSaveError is not null &&
                        string.Equals(_lastError, _lastSaveError, StringComparison.Ordinal))
                    {
                        _lastError = null;
                    }

                    _lastSaveError = null;
                    PublishSnapshot(null);
                },
                DispatcherPriority.Normal,
                cancellationToken);
            return restored.DeepCopy();
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void MarkSaveSucceeded(AppSettings settings)
    {
        _lastPersistedSettings = settings.DeepCopy();
        if (_lastSaveError is not null && string.Equals(_lastError, _lastSaveError, StringComparison.Ordinal))
        {
            _lastError = null;
        }

        _lastSaveError = null;
    }
}
