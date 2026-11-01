using CommunityToolkit.Mvvm.Input;
using EvePreview.App.Services;

namespace EvePreview.App.ViewModels;

public sealed partial class ClientsViewModel
{
    [RelayCommand]
    private async Task SaveSelectedLayoutAsync()
    {
        if (SelectedClient is null)
        {
            return;
        }

        var client = SelectedClient;
        var originalTitle = client.OriginalTitle;
        if (_layoutController is null)
        {
            LayoutStatusText = "预览服务尚未启动，无法保存布局。";
            return;
        }

        LayoutStatusText = "正在保存客户端窗口布局…";
        try
        {
            var saved = await _layoutController.SaveClientWindowLayoutAsync(originalTitle);
            if (saved is not null)
            {
                client.SetClientWindowBounds(saved.Value);
            }

            LayoutStatusText = saved is not null
                ? "已保存当前客户端窗口布局。"
                : "保存失败：客户端可能已离线或最小化。";
        }
        catch (Exception exception)
        {
            LayoutStatusText = $"保存失败：{exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplySelectedLayoutAsync()
    {
        if (SelectedClient is null)
        {
            return;
        }

        if (_layoutController is null)
        {
            LayoutStatusText = "预览服务尚未启动，无法应用布局。";
            return;
        }

        LayoutStatusText = "正在应用客户端窗口布局…";
        try
        {
            var applied = await _layoutController.ApplyClientWindowLayoutAsync(SelectedClient.OriginalTitle);
            LayoutStatusText = applied ? "已应用保存的客户端窗口布局。" : "应用失败：客户端离线、尚未保存布局或无响应。";
        }
        catch (Exception exception)
        {
            LayoutStatusText = $"应用失败：{exception.Message}";
        }
    }
}
