using EvePreview.App.Services;
using EvePreview.App.ViewModels;
using EvePreview.Core;

namespace EvePreview.App.Tests;

public sealed class ThumbnailSettingsViewModelTests
{
    [Fact]
    public void Load_from_applies_new_limits_before_restoring_default_size()
    {
        var viewModel = new ThumbnailSettingsViewModel
        {
            MinimumWidth = 8,
            MinimumHeight = 8,
            MaximumWidth = 200,
            MaximumHeight = 100,
            DefaultWidth = 200,
            DefaultHeight = 100
        };
        var settings = new AppSettings();
        settings.Preview.MinWidth = 8;
        settings.Preview.MinHeight = 8;
        settings.Preview.MaxWidth = 1920;
        settings.Preview.MaxHeight = 1080;
        settings.Preview.DefaultWidth = 320;
        settings.Preview.DefaultHeight = 180;

        viewModel.LoadFrom(settings);

        Assert.Equal(320, viewModel.DefaultWidth);
        Assert.Equal(180, viewModel.DefaultHeight);
        Assert.Equal(1920, viewModel.MaximumWidth);
        Assert.Equal(1080, viewModel.MaximumHeight);
    }

    [Fact]
    public void Opacity_change_notifies_shell_with_setting_name_and_value()
    {
        var viewModel = new ThumbnailSettingsViewModel();
        SettingChangedEventArgs? change = null;
        viewModel.SettingChanged += (_, args) => change = args;

        viewModel.OpacityPercent = 75;

        Assert.Equal(75, viewModel.OpacityPercent);
        Assert.NotNull(change);
        Assert.Equal(ThumbnailSettingsViewModel.OpacitySettingName, change.SettingName);
        Assert.Equal(75d, change.Value);
        Assert.Equal(string.Empty, viewModel.OpacityError);
    }

    [Fact]
    public void Unchanged_opacity_does_not_notify()
    {
        var viewModel = new ThumbnailSettingsViewModel();
        var notifications = 0;
        viewModel.SettingChanged += (_, _) => notifications++;

        viewModel.OpacityPercent = viewModel.OpacityPercent;

        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData(19, 20)]
    [InlineData(101, 100)]
    public void Out_of_range_opacity_is_clamped_to_nearest_valid_value(double input, double expected)
    {
        var viewModel = new ThumbnailSettingsViewModel();
        var notifications = 0;
        viewModel.SettingChanged += (_, _) => notifications++;

        viewModel.OpacityPercent = input;

        Assert.Equal(expected, viewModel.OpacityPercent);
        Assert.Equal(string.Empty, viewModel.OpacityError);
        Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(5000, 1920)]
    public void Out_of_range_default_width_is_clamped(int input, int expected)
    {
        var viewModel = new ThumbnailSettingsViewModel();

        viewModel.DefaultWidth = input;

        Assert.Equal(expected, viewModel.DefaultWidth);
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(5000, 1080)]
    public void Out_of_range_default_height_is_clamped(int input, int expected)
    {
        var viewModel = new ThumbnailSettingsViewModel();

        viewModel.DefaultHeight = input;

        Assert.Equal(expected, viewModel.DefaultHeight);
    }

    [Fact]
    public void Valid_opacity_clears_previous_inline_error()
    {
        var viewModel = new ThumbnailSettingsViewModel();
        viewModel.OpacityPercent = 10;

        viewModel.OpacityPercent = 80;

        Assert.Equal(80, viewModel.OpacityPercent);
        Assert.Equal(string.Empty, viewModel.OpacityError);
    }

    [Fact]
    public void Size_limits_text_tracks_limit_properties()
    {
        var viewModel = new ThumbnailSettingsViewModel();
        var expected = $"尺寸范围：最小 {viewModel.MinimumWidth} × {viewModel.MinimumHeight} 像素，" +
                       $"最大 {viewModel.MaximumWidth} × {viewModel.MaximumHeight} 像素";
        Assert.Equal(expected, viewModel.SizeLimitsText);

        var changed = false;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ThumbnailSettingsViewModel.SizeLimitsText))
            {
                changed = true;
            }
        };

        viewModel.MinimumWidth = 200;

        Assert.True(changed);
        Assert.Contains("最小 200", viewModel.SizeLimitsText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(8, 8, true)]
    [InlineData(15, 15, true)]
    [InlineData(16, 15, false)]
    [InlineData(15, 16, false)]
    [InlineData(0, 0, false)]
    public void Compact_surface_is_used_only_when_both_dimensions_are_below_threshold(
        double width,
        double height,
        bool expected)
    {
        Assert.Equal(expected, CompactPreviewSurface.ShouldUse(width, height));
    }

    [Theory]
    [InlineData("#000000", "#D8D8D8")]
    [InlineData("#FFFFFF", "#303030")]
    public void Preview_text_outline_adapts_to_text_luminance(string text, string expectedOutline)
    {
        var (_, outline) = PreviewTextPalette.Resolve(text);
        var expected = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(expectedOutline);
        Assert.Equal(expected, outline);
    }

    [Fact]
    public void Enabling_grid_snap_disables_edge_snap()
    {
        var viewModel = new ThumbnailSettingsViewModel { SnapToEdges = true };

        viewModel.SnapToGrid = true;

        Assert.True(viewModel.SnapToGrid);
        Assert.False(viewModel.SnapToEdges);
    }

    [Fact]
    public void Enabling_edge_snap_disables_grid_snap()
    {
        var viewModel = new ThumbnailSettingsViewModel { SnapToGrid = true };

        viewModel.SnapToEdges = true;

        Assert.True(viewModel.SnapToEdges);
        Assert.False(viewModel.SnapToGrid);
    }
}
