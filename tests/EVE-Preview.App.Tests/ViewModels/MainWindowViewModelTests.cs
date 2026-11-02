using EvePreview.App.Services;
using EvePreview.App.ViewModels;

namespace EvePreview.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Status_bar_exposes_the_build_date()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", viewModel.BuildDate);
        Assert.Equal(BuildInfo.BuildDate, viewModel.BuildDate);
    }

    [Fact]
    public void New_shell_opens_overview_page()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal(AppPage.Overview, viewModel.CurrentPage);
    }

    [Theory]
    [InlineData(AppPage.Thumbnails)]
    [InlineData(AppPage.Clients)]
    [InlineData(AppPage.CycleGroups)]
    [InlineData(AppPage.System)]
    public void Navigate_command_changes_page_and_view_model(AppPage page)
    {
        var viewModel = new MainWindowViewModel();
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName!);

        viewModel.NavigateCommand.Execute(page);

        Assert.Equal(page, viewModel.CurrentPage);
        Assert.Contains(nameof(MainWindowViewModel.CurrentPage), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.CurrentPageViewModel), changedProperties);
        Assert.IsType(page switch
        {
            AppPage.Thumbnails => typeof(ThumbnailSettingsViewModel),
            AppPage.Clients => typeof(ClientsViewModel),
            AppPage.CycleGroups => typeof(CycleGroupsViewModel),
            _ => typeof(SystemSettingsViewModel)
        }, viewModel.CurrentPageViewModel);
    }

    [Fact]
    public void Navigating_to_current_page_does_not_send_duplicate_notifications()
    {
        var viewModel = new MainWindowViewModel();
        var notificationCount = 0;
        viewModel.PropertyChanged += (_, _) => notificationCount++;

        viewModel.NavigateCommand.Execute(AppPage.Overview);

        Assert.Equal(0, notificationCount);
    }

    [Fact]
    public async Task Toggle_preview_starts_injected_runtime_and_updates_overview()
    {
        var runtime = new RecordingRuntime();
        var viewModel = new MainWindowViewModel(runtime);

        await viewModel.TogglePreviewCommand.ExecuteAsync(null);
        await viewModel.TogglePreviewCommand.ExecuteAsync(null);

        Assert.Equal(1, runtime.StartCalls);
        Assert.Equal(1, runtime.StopCalls);
        Assert.Equal(PreviewServiceState.Stopped, viewModel.Overview.ServiceState);
        Assert.Equal("启动预览", viewModel.Overview.TogglePreviewText);
    }

    [Fact]
    public async Task Runtime_failure_is_retained_in_status_instead_of_being_swallowed()
    {
        var runtime = new RecordingRuntime { StartException = new InvalidOperationException("预览服务不可用") };
        var viewModel = new MainWindowViewModel(runtime);

        await viewModel.TogglePreviewCommand.ExecuteAsync(null);

        Assert.Equal(PreviewServiceState.Faulted, viewModel.Overview.ServiceState);
        Assert.Equal("预览服务不可用", viewModel.LastError);
        Assert.Contains("预览服务不可用", viewModel.ServiceStatusText);
    }

    [Fact]
    public void Overview_state_change_raises_shell_status_binding_notification()
    {
        var viewModel = new MainWindowViewModel();
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName!);

        viewModel.Overview.ServiceState = PreviewServiceState.Running;

        Assert.Contains(nameof(MainWindowViewModel.ServiceStatusText), changedProperties);
        Assert.Equal("预览运行中", viewModel.ServiceStatusText);
    }

    [Fact]
    public void Compatibility_mode_change_updates_overview_render_mode()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.SystemSettings.UseCompatibilityMode = true;

        Assert.Equal("GDI 兼容模式（约 1 FPS）", viewModel.Overview.RenderModeText);
    }

    private sealed class RecordingRuntime : IPreviewRuntime
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public Exception? StartException { get; init; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return StartException is null ? Task.CompletedTask : Task.FromException(StartException);
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalls++;
            return Task.CompletedTask;
        }
    }
}
