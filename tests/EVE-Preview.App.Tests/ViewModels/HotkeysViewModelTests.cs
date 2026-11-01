using EvePreview.App.ViewModels;

namespace EvePreview.App.Tests;

public sealed class HotkeysViewModelTests
{
    [Fact]
    public void Default_global_hotkeys_have_no_conflict()
    {
        var viewModel = new HotkeysViewModel();

        Assert.Equal("Ctrl+Shift+M", viewModel.MinimizeAllHotkey);
        Assert.Equal("Ctrl+Shift+R", viewModel.RefreshMinimizedHotkey);
        Assert.Equal(string.Empty, viewModel.ConflictText);
        Assert.Equal("选择热键输入框后录制；按 Esc 取消。", viewModel.RecorderStatusText);
    }

    [Fact]
    public void Duplicate_global_hotkey_shows_visible_conflict()
    {
        var viewModel = new HotkeysViewModel();

        viewModel.RefreshMinimizedHotkey = viewModel.MinimizeAllHotkey;

        Assert.Equal("热键冲突：最小化全部与刷新最小化客户端使用相同组合。", viewModel.ConflictText);
    }

    [Fact]
    public void Clearing_duplicate_hotkey_clears_conflict()
    {
        var viewModel = new HotkeysViewModel
        {
            RefreshMinimizedHotkey = "Ctrl+Shift+M"
        };

        viewModel.RefreshMinimizedHotkey = string.Empty;

        Assert.Equal(string.Empty, viewModel.ConflictText);
    }
}
