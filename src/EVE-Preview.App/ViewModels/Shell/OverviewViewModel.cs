using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EvePreview.App.Services;

namespace EvePreview.App.ViewModels;

public sealed class ClientSummaryViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string OriginalWindowTitle { get; init; } = string.Empty;
    public string CharacterName { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
    public string ActivityText { get; init; } = "非活动";
    public string VisibilityText { get; init; } = "预览可见";
    public string CycleStatusText { get; init; } = "参与循环";
}

public sealed partial class OverviewViewModel : ObservableObject
{
    public ObservableCollection<ClientSummaryViewModel> Clients { get; } = [];

    [ObservableProperty]
    private PreviewServiceState _serviceState = PreviewServiceState.Stopped;

    [ObservableProperty]
    private int _clientCount;

    [ObservableProperty]
    private string _activeClientText = "未检测到客户端";

    [ObservableProperty]
    private string _layoutStatusText = string.Empty;

    [ObservableProperty]
    private string _renderModeText = "DWM 实时预览";

    partial void OnServiceStateChanged(PreviewServiceState value)
    {
        OnPropertyChanged(nameof(ServiceStatusText));
        OnPropertyChanged(nameof(TogglePreviewText));
    }

    public string ServiceStatusText => LocalizationManager.Translate(ServiceState switch
    {
        PreviewServiceState.Stopped => "服务已停止",
        PreviewServiceState.Starting => "服务正在启动",
        PreviewServiceState.Running => "预览运行中",
        PreviewServiceState.Paused => "预览已暂停",
        PreviewServiceState.Stopping => "服务正在停止",
        PreviewServiceState.Faulted => "服务需要处理",
        _ => throw new ArgumentOutOfRangeException()
    });

    public string TogglePreviewText => LocalizationManager.Translate(
        ServiceState == PreviewServiceState.Running
            ? "暂停预览"
            : ServiceState == PreviewServiceState.Paused ? "恢复预览" : "启动预览");
}
