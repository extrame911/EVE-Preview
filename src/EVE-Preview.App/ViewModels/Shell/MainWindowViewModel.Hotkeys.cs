using EvePreview.App.Services;
using EvePreview.Core;

namespace EvePreview.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool HandleHotkeyEdit(
        object source,
        string propertyName,
        string value,
        Action<string> setValue)
    {
        if (_applyingViewModelState)
        {
            return false;
        }

        Hotkeys.DuplicateText = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !HotKeyParser.TryParse(value, out var hotkey))
        {
            return false;
        }

        if (hotkey.MouseButton != HotKeyMouseButton.None)
        {
            setValue(string.Empty);
            Hotkeys.DuplicateText = LocalizationManager.IsEnglish
                ? "Mouse buttons cannot be used as hotkeys; the input was cleared."
                : "热键不能设置为鼠标按键，已清空输入。";
            return true;
        }

        if (source is not ClientConfigViewModel and not CycleGroupViewModel)
        {
            return false;
        }

        foreach (var binding in GetHotkeyBindings())
        {
            if (ReferenceEquals(binding.Source, source) && binding.PropertyName == propertyName)
            {
                continue;
            }

            if (HotKeyParser.TryParse(binding.Value, out var assigned) &&
                string.Equals(assigned.Canonical, hotkey.Canonical, StringComparison.OrdinalIgnoreCase))
            {
                setValue(string.Empty);
                Hotkeys.DuplicateText = LocalizationManager.IsEnglish
                    ? $"Hotkey duplicate: '{hotkey.Canonical}' is already used by {binding.Label}."
                    : $"热键重复：'{hotkey.Canonical}' 已用于 {binding.Label}。";
                return true;
            }
        }

        return false;
    }

    private IEnumerable<HotkeyBinding> GetHotkeyBindings()
    {
        yield return new HotkeyBinding(Hotkeys, nameof(HotkeysViewModel.MinimizeAllHotkey),
            LocalizationManager.IsEnglish ? "Minimize All" : "最小化全部", Hotkeys.MinimizeAllHotkey);
        yield return new HotkeyBinding(Hotkeys, nameof(HotkeysViewModel.RefreshMinimizedHotkey),
            LocalizationManager.IsEnglish ? "Restore Minimized Clients" : "刷新最小化客户端", Hotkeys.RefreshMinimizedHotkey);
        yield return new HotkeyBinding(Hotkeys, nameof(HotkeysViewModel.ReturnToPreviousHotkey),
            LocalizationManager.IsEnglish ? "Return to Previous Application" : "返回此前外部应用", Hotkeys.ReturnToPreviousHotkey);

        for (var index = 0; index < CycleGroups.Groups.Count; index++)
        {
            var group = CycleGroups.Groups[index];
            var prefix = LocalizationManager.IsEnglish ? $"Cycle List {index + 1}" : $"循环组 {index + 1}";
            yield return new HotkeyBinding(group, nameof(CycleGroupViewModel.ForwardHotkey),
                LocalizationManager.IsEnglish ? $"{prefix} Forward" : $"{prefix} 前进", group.ForwardHotkey);
            yield return new HotkeyBinding(group, nameof(CycleGroupViewModel.BackwardHotkey),
                LocalizationManager.IsEnglish ? $"{prefix} Backward" : $"{prefix} 后退", group.BackwardHotkey);
        }

        foreach (var client in Clients.Clients)
        {
            yield return new HotkeyBinding(client, nameof(ClientConfigViewModel.HotKey),
                LocalizationManager.IsEnglish ? $"Client {client.DisplayName}" : $"客户端 {client.DisplayName}", client.HotKey);
        }
    }

    private readonly record struct HotkeyBinding(
        object Source,
        string PropertyName,
        string Label,
        string Value);
}
