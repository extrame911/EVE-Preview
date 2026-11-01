using System.Windows.Threading;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime
{
    private void RebuildHotkeys()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        if (_hotkeys is null)
        {
            return;
        }

        var errors = new List<string>();
        RegisterHotkey(_settings.HotKeys.MinimizeAll, "最小化全部", token => RunOnUiAsync(token, () => MinimizeAllAsync(token)), errors);
        RegisterHotkey(_settings.HotKeys.RefreshMinimized, "刷新最小化客户端", token => RunOnUiAsync(token, () => RestoreAllAsync(token)), errors);
        RegisterHotkey(_settings.HotKeys.ReturnToPrevious, "返回此前外部应用", token => RunOnUiAsync(token, () => ReturnToPreviousWindowAsync()), errors);
        for (var index = 0; index < _settings.CycleGroups.Count; index++)
        {
            var groupIndex = index;
            RegisterHotkey(
                _settings.CycleGroups[index].ForwardHotKey,
                $"循环组 {index + 1} 前进",
                token => RunOnUiAsync(token, () => CycleAsync(groupIndex, CycleDirection.Forward)),
                errors);
            RegisterHotkey(
                _settings.CycleGroups[index].BackwardHotKey,
                $"循环组 {index + 1} 后退",
                token => RunOnUiAsync(token, () => CycleAsync(groupIndex, CycleDirection.Backward)),
                errors);
        }

        foreach (var client in _settings.Clients.Where(client => !string.IsNullOrWhiteSpace(client.HotKey)))
        {
            var preview = FindPreview(client.OriginalTitle);
            if (preview is null)
            {
                continue;
            }

            var identity = preview.Identity;
            RegisterHotkey(
                client.HotKey,
                $"客户端 {client.OriginalTitle}",
                token => RunOnUiAsync(token, () => ActivateClientCoreAsync(identity, token)),
                errors);
        }

        if (errors.Count > 0)
        {
            PublishSnapshot(string.Join("；", errors));
        }

        PublishSnapshot(null);
    }

    private void RegisterHotkey(
        string? text,
        string label,
        Func<CancellationToken, Task> action,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!HotkeyMapper.TryMap(text, out var hotkey))
        {
            errors.Add($"{label} 的热键 '{text}' 无效");
            return;
        }

        var result = _hotkeys!.Register(hotkey, action);
        if (!result.Succeeded)
        {
            errors.Add($"{label} 的热键注册失败：{result.Error}");
            return;
        }

        if (result.Registration is not null)
        {
            _registrations.Add(result.Registration);
        }
    }

    private async Task RunOnUiAsync(CancellationToken cancellationToken, Func<Task> action)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_paused)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            await action();
            return;
        }

        await _dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken)
            .Task.Unwrap().ConfigureAwait(false);
    }

}
