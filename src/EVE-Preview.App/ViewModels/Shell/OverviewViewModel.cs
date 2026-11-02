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

    public bool IsPreviewOff => ServiceState is PreviewServiceState.Stopped or PreviewServiceState.Faulted;

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
        OnPropertyChanged(nameof(TogglePreviewToolTip));
        OnPropertyChanged(nameof(IsPreviewOff));
    }

    public string ServiceStatusText => LocalizationManager.Translate(ServiceState switch
    {
        PreviewServiceState.Stopped => "预览关闭",
        PreviewServiceState.Starting => "服务正在启动",
        PreviewServiceState.Running => "预览运行中",
        PreviewServiceState.Paused => "预览已暂停",
        PreviewServiceState.Stopping => "服务正在停止",
        PreviewServiceState.Faulted => "预览关闭",
        _ => throw new ArgumentOutOfRangeException()
    });

    public string TogglePreviewText => LocalizationManager.Translate(
        ServiceState == PreviewServiceState.Running ? "停止预览" : "启动预览");

    public string TogglePreviewToolTip => LocalizationManager.Translate(
        ServiceState == PreviewServiceState.Running ? "停止客户端预览" : "启动客户端预览");
}
