using EvePreview.App.ViewModels;
using EvePreview.Core;

namespace EvePreview.App.Tests;

public sealed class SystemSettingsViewModelTests
{
    [Fact]
    public void Compatibility_mode_notifies_and_updates_rendering_description()
    {
        var viewModel = new SystemSettingsViewModel();
        SettingChangedEventArgs? change = null;
        viewModel.SettingChanged += (_, args) => change = args;

        viewModel.UseCompatibilityMode = true;

        Assert.True(viewModel.UseCompatibilityMode);
        Assert.Equal("GDI 兼容模式（约 1 FPS）", viewModel.RenderingModeText);
        Assert.NotNull(change);
        Assert.Equal(SystemSettingsViewModel.CompatibilityModeSettingName, change.SettingName);
        Assert.Equal(true, change.Value);
    }

    [Fact]
    public void Auto_restore_client_window_defaults_on_and_loads_a_saved_off_value()
    {
        var viewModel = new SystemSettingsViewModel();
        Assert.True(viewModel.AutoRestoreClientWindowOnStartup);

        var settings = new AppSettings();
        settings.General.AutoRestoreClientWindowOnStartup = false;
        viewModel.LoadFrom(settings);

        Assert.False(viewModel.AutoRestoreClientWindowOnStartup);
    }

    [Fact]
    public void Defaults_use_dwm_and_safe_monitoring_interval()
    {
        var viewModel = new SystemSettingsViewModel();

        Assert.False(viewModel.UseCompatibilityMode);
        Assert.Equal("DWM 实时预览", viewModel.RenderingModeText);
        Assert.Equal(500, viewModel.PollingIntervalMilliseconds);
        Assert.Equal(string.Empty, viewModel.PollingIntervalError);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(2001)]
    public void Invalid_polling_interval_is_rejected_without_notification(int value)
    {
        var viewModel = new SystemSettingsViewModel();
        var notifications = 0;
        viewModel.SettingChanged += (_, _) => notifications++;

        viewModel.PollingIntervalMilliseconds = value;

        Assert.Equal(500, viewModel.PollingIntervalMilliseconds);
        Assert.Equal("轮询周期范围为 100–2000 毫秒。", viewModel.PollingIntervalError);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Valid_polling_interval_notifies_and_clears_error()
    {
        var viewModel = new SystemSettingsViewModel();
        viewModel.PollingIntervalMilliseconds = 50;
        SettingChangedEventArgs? change = null;
        viewModel.SettingChanged += (_, args) => change = args;

        viewModel.PollingIntervalMilliseconds = 250;

        Assert.Equal(250, viewModel.PollingIntervalMilliseconds);
        Assert.Equal(string.Empty, viewModel.PollingIntervalError);
        Assert.NotNull(change);
        Assert.Equal("Monitoring.PollingIntervalMilliseconds", change.SettingName);
        Assert.Equal(250, change.Value);
    }
}
