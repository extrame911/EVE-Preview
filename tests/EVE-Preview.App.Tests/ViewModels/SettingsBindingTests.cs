using EvePreview.App.ViewModels;
using EvePreview.Core;

namespace EvePreview.App.Tests;

public sealed class SettingsBindingTests
{
    [Fact]
    public void Thumbnail_edits_are_projected_into_the_live_core_settings()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.ThumbnailSettings.OpacityPercent = 75;
        viewModel.ThumbnailSettings.DefaultWidth = 400;
        viewModel.ThumbnailSettings.GridSizeX = 20;
        viewModel.ThumbnailSettings.SnapToGrid = true;
        viewModel.ThumbnailSettings.ZoomDelayMilliseconds = 250;
        viewModel.ThumbnailSettings.ShowCharacterNameOnly = false;

        Assert.Equal(0.75, settings.Display.Opacity);
        Assert.Equal(400, settings.Preview.DefaultWidth);
        Assert.Equal(20, settings.Preview.GridSizeX);
        Assert.Equal(250, settings.Zoom.DelayMs);
        Assert.False(settings.Overlay.ShowCharacterNameOnly);
        Assert.Equal(SnapMode.Grid, settings.Preview.SnapMode);
    }

    [Fact]
    public void System_edits_are_normalized_into_process_and_rendering_settings()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.SystemSettings.ExecutableNames = @"C:\Games\EVE\ExeFile.exe, other";
        viewModel.SystemSettings.PollingIntervalMilliseconds = 750;
        viewModel.SystemSettings.UseCompatibilityMode = true;

        Assert.Equal(["exefile", "other"], settings.Monitoring.ProcessNames);
        Assert.Equal(750, settings.Monitoring.PollIntervalMs);
        Assert.Equal(RenderingMode.Gdi, settings.Compatibility.RenderingMode);
    }

    [Fact]
    public void Hotkey_edits_are_projected_and_validated_after_normalization()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.Hotkeys.MinimizeAllHotkey = "shift+ctrl+f4";
        viewModel.Hotkeys.RefreshMinimizedHotkey = "Ctrl+F5";

        Assert.Equal("Ctrl+Shift+F4", settings.HotKeys.MinimizeAll);
        Assert.Equal("Ctrl+F5", settings.HotKeys.RefreshMinimized);
        Assert.Equal(string.Empty, viewModel.Hotkeys.ConflictText);
    }

    [Fact]
    public void Cycle_group_hotkeys_and_order_are_projected()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);
        var clientA = new CycleGroupClientViewModel("EVE - A");
        var clientB = new CycleGroupClientViewModel("EVE - B");
        viewModel.CycleGroups.Groups[0].Clients.Add(clientA);
        viewModel.CycleGroups.Groups[0].Clients.Add(clientB);
        viewModel.CycleGroups.Groups[0].ForwardHotkey = "Ctrl+F6";
        viewModel.CycleGroups.Groups[0].MoveDownCommand.Execute(clientA);

        Assert.Equal("Ctrl+F6", settings.CycleGroups[0].ForwardHotKey);
        Assert.Equal(["EVE - B", "EVE - A"], settings.CycleGroups[0].OrderedClientTitles);
    }

    [Fact]
    public void Highlight_color_is_projected_into_overlay_options()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.ThumbnailSettings.OverlayColor = "#112233";
        viewModel.ThumbnailSettings.HighlightColor = "#445566";

        Assert.Equal("#112233", settings.Overlay.TextColor);
        Assert.Equal("#445566", settings.Overlay.HighlightColor);
    }

    [Fact]
    public void Hide_delays_are_projected_into_display_options()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.ThumbnailSettings.HideDelayMilliseconds = 1200;
        viewModel.ThumbnailSettings.HideActiveDelayMilliseconds = 450;

        Assert.Equal(1200, settings.Display.HideDelayMs);
        Assert.Equal(450, settings.Display.HideActiveDelayMs);
    }

    [Fact]
    public void Dynamic_cycle_group_count_is_projected_and_remains_valid()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.CycleGroups.AddGroupCommand.Execute(null);
        Assert.Equal(6, viewModel.CycleGroups.Groups.Count);
        Assert.Equal(6, settings.CycleGroups.Count);

        viewModel.CycleGroups.RemoveGroupCommand.Execute(viewModel.CycleGroups.Groups[5]);
        Assert.Equal(5, settings.CycleGroups.Count);

        while (viewModel.CycleGroups.Groups.Count > 1)
        {
            viewModel.CycleGroups.RemoveGroupCommand.Execute(viewModel.CycleGroups.Groups[^1]);
        }

        Assert.Single(settings.CycleGroups);
        Assert.True(settings.Validate().IsValid);
    }

    [Fact]
    public void Invalid_cross_field_sizes_are_clamped_to_a_savable_model()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.ThumbnailSettings.MinimumWidth = 800;

        Assert.True(viewModel.ThumbnailSettings.DefaultWidth >= viewModel.ThumbnailSettings.MinimumWidth);
        Assert.True(settings.Validate().IsValid);
        Assert.True(settings.Preview.DefaultWidth >= settings.Preview.MinWidth);
    }

    [Theory]
    [InlineData("中上", OverlayPosition.TopCenter)]
    [InlineData("中下", OverlayPosition.BottomCenter)]
    [InlineData("中间", OverlayPosition.Center)]
    public void Overlay_position_parser_supports_centered_positions(string text, OverlayPosition expected)
    {
        Assert.Equal(expected, ThumbnailSettingsViewModel.ParseOverlayPosition(text));
    }

    [Fact]
    public void All_four_overlay_corners_survive_an_unrelated_setting_change()
    {
        var settings = new AppSettings();
        settings.Overlay.Position = OverlayPosition.TopRight;
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.SystemSettings.PollingIntervalMilliseconds = 600;

        Assert.Equal(OverlayPosition.TopRight, settings.Overlay.Position);
        Assert.Equal("右上", viewModel.ThumbnailSettings.OverlayPosition);
    }

    [Fact]
    public void Invalid_hotkey_text_does_not_corrupt_the_last_valid_binding()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.Hotkeys.MinimizeAllHotkey = "Ctrl+";

        Assert.Equal("Ctrl+Shift+M", settings.HotKeys.MinimizeAll);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Hotkeys.ConflictText));
    }

    [Fact]
    public void Duplicate_group_hotkey_is_cleared_and_reports_its_global_usage()
    {
        var settings = new AppSettings();
        var viewModel = new MainWindowViewModel(null, settings);

        viewModel.CycleGroups.Groups[0].ForwardHotkey = "Ctrl+Shift+M";

        Assert.Equal(string.Empty, viewModel.CycleGroups.Groups[0].ForwardHotkey);
        Assert.Equal(string.Empty, settings.CycleGroups[0].ForwardHotKey);
        Assert.Contains("重复", viewModel.Hotkeys.DuplicateText);
        Assert.Contains("最小化全部", viewModel.Hotkeys.DuplicateText);
        Assert.True(settings.Validate().IsValid);
    }
}
