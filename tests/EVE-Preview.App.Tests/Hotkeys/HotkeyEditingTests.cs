using EvePreview.App.ViewModels;
using EvePreview.Core;

namespace EvePreview.App.Tests;

public sealed class HotkeyEditingTests
{
    [Fact]
    public void Client_hotkey_duplicating_a_cycle_group_is_cleared_with_its_usage_location()
    {
        var settings = CreateSettings();
        settings.CycleGroups[0].ForwardHotKey = "Ctrl+F8";
        var viewModel = new MainWindowViewModel(settings: settings);

        viewModel.Clients.Clients[0].HotKey = "ctrl+f8";

        Assert.Equal(string.Empty, viewModel.Clients.Clients[0].HotKey);
        Assert.Equal(string.Empty, viewModel.Settings.Clients[0].HotKey);
        Assert.Contains("热键重复", viewModel.Hotkeys.DuplicateText);
        Assert.Contains("Ctrl+F8", viewModel.Hotkeys.DuplicateText);
        Assert.Contains("循环组 1 前进", viewModel.Hotkeys.DuplicateText);
    }

    [Fact]
    public void Cycle_group_hotkey_duplicating_a_client_is_cleared_with_its_usage_location()
    {
        var settings = CreateSettings();
        settings.Clients[0].HotKey = "Ctrl+F8";
        var viewModel = new MainWindowViewModel(settings: settings);

        viewModel.CycleGroups.Groups[0].ForwardHotkey = "ctrl+f8";

        Assert.Equal(string.Empty, viewModel.CycleGroups.Groups[0].ForwardHotkey);
        Assert.Equal(string.Empty, viewModel.Settings.CycleGroups[0].ForwardHotKey);
        Assert.Contains("热键重复", viewModel.Hotkeys.DuplicateText);
        Assert.Contains("客户端 EVE - Alpha", viewModel.Hotkeys.DuplicateText);
    }

    [Theory]
    [InlineData("Client")]
    [InlineData("CycleForward")]
    [InlineData("CycleBackward")]
    [InlineData("MinimizeAll")]
    [InlineData("RefreshMinimized")]
    [InlineData("ReturnToPrevious")]
    public void Mouse_hotkeys_are_cleared_at_every_editing_entry(string target)
    {
        var viewModel = new MainWindowViewModel(settings: CreateSettings());

        SetHotkey(viewModel, target, "Ctrl+XButton1");

        Assert.Equal(string.Empty, GetHotkey(viewModel, target));
        Assert.Contains("鼠标", viewModel.Hotkeys.DuplicateText);
        Assert.Equal(string.Empty, viewModel.Hotkeys.ConflictText);
    }

    private static AppSettings CreateSettings()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - Alpha" });
        settings.Normalize();
        return settings;
    }

    private static void SetHotkey(MainWindowViewModel viewModel, string target, string value)
    {
        switch (target)
        {
            case "Client": viewModel.Clients.Clients[0].HotKey = value; break;
            case "CycleForward": viewModel.CycleGroups.Groups[0].ForwardHotkey = value; break;
            case "CycleBackward": viewModel.CycleGroups.Groups[0].BackwardHotkey = value; break;
            case "MinimizeAll": viewModel.Hotkeys.MinimizeAllHotkey = value; break;
            case "RefreshMinimized": viewModel.Hotkeys.RefreshMinimizedHotkey = value; break;
            case "ReturnToPrevious": viewModel.Hotkeys.ReturnToPreviousHotkey = value; break;
        }
    }

    private static string GetHotkey(MainWindowViewModel viewModel, string target) => target switch
    {
        "Client" => viewModel.Clients.Clients[0].HotKey,
        "CycleForward" => viewModel.CycleGroups.Groups[0].ForwardHotkey,
        "CycleBackward" => viewModel.CycleGroups.Groups[0].BackwardHotkey,
        "MinimizeAll" => viewModel.Hotkeys.MinimizeAllHotkey,
        "RefreshMinimized" => viewModel.Hotkeys.RefreshMinimizedHotkey,
        "ReturnToPrevious" => viewModel.Hotkeys.ReturnToPreviousHotkey,
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };
}
