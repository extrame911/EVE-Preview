using EvePreview.App.ViewModels;

namespace EvePreview.App.Tests;

public sealed class OverviewViewModelTests
{
    [Fact]
    public void Stopped_service_has_clear_empty_status()
    {
        var viewModel = new OverviewViewModel();

        Assert.Equal(PreviewServiceState.Stopped, viewModel.ServiceState);
        Assert.Equal("服务已停止", viewModel.ServiceStatusText);
        Assert.Equal("启动预览", viewModel.TogglePreviewText);
        Assert.Equal(0, viewModel.ClientCount);
        Assert.Equal("未检测到客户端", viewModel.ActiveClientText);
        Assert.Equal("DWM 实时预览", viewModel.RenderModeText);
    }

    [Theory]
    [InlineData(PreviewServiceState.Running, "预览运行中", "暂停预览")]
    [InlineData(PreviewServiceState.Paused, "预览已暂停", "恢复预览")]
    [InlineData(PreviewServiceState.Faulted, "服务需要处理", "启动预览")]
    public void Service_state_updates_status_and_primary_action(
        PreviewServiceState state,
        string status,
        string action)
    {
        var viewModel = new OverviewViewModel();
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName!);

        viewModel.ServiceState = state;

        Assert.Equal(status, viewModel.ServiceStatusText);
        Assert.Equal(action, viewModel.TogglePreviewText);
        Assert.Contains(nameof(OverviewViewModel.ServiceStatusText), changedProperties);
        Assert.Contains(nameof(OverviewViewModel.TogglePreviewText), changedProperties);
    }
}
