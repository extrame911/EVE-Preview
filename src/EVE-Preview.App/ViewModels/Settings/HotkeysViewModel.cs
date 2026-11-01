using CommunityToolkit.Mvvm.ComponentModel;
using EvePreview.Core;

namespace EvePreview.App.ViewModels;

public sealed partial class HotkeysViewModel : ObservableObject
{
    public HotkeysViewModel()
    {
    }

    public HotkeysViewModel(AppSettings settings)
    {
        LoadFrom(settings);
    }

    [ObservableProperty]
    private string _minimizeAllHotkey = "Ctrl+Shift+M";

    [ObservableProperty]
    private string _refreshMinimizedHotkey = "Ctrl+Shift+R";

    [ObservableProperty]
    private string _returnToPreviousHotkey = "Ctrl+Shift+Back";

    [ObservableProperty]
    private string _conflictText = string.Empty;

    [ObservableProperty]
    private string _duplicateText = string.Empty;

    [ObservableProperty]
    private string _recorderStatusText = "选择热键输入框后录制；按 Esc 取消。";

    partial void OnMinimizeAllHotkeyChanged(string value) => ValidateGlobalHotkeys();
    partial void OnRefreshMinimizedHotkeyChanged(string value) => ValidateGlobalHotkeys();

    private void ValidateGlobalHotkeys()
    {
        ConflictText = !string.IsNullOrWhiteSpace(MinimizeAllHotkey) &&
                       string.Equals(MinimizeAllHotkey, RefreshMinimizedHotkey, StringComparison.OrdinalIgnoreCase)
            ? "热键冲突：最小化全部与刷新最小化客户端使用相同组合。"
            : string.Empty;
    }

    public void LoadFrom(AppSettings settings)
    {
        MinimizeAllHotkey = settings.HotKeys.MinimizeAll;
        RefreshMinimizedHotkey = settings.HotKeys.RefreshMinimized;
        ReturnToPreviousHotkey = settings.HotKeys.ReturnToPrevious;
        ValidateGlobalHotkeys();
    }
}
