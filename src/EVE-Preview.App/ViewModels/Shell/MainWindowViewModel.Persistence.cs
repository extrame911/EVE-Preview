using CommunityToolkit.Mvvm.Input;
using EvePreview.App.Services;
using EvePreview.Core;

namespace EvePreview.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task DiscardUnsavedChangesAsync()
    {
        if (_runtime is not IPreviewRuntimeSettingsRecovery recovery)
        {
            SystemSettings.ProfileStatusText = LocalizationManager.Translate("配置服务不可用。");
            return;
        }

        try
        {
            Interlocked.Increment(ref _settingsApplyVersion);
            var restored = await recovery.RestoreLastSavedSettingsAsync(_settings);
            _settings.CopyFrom(restored);
            ReloadSettingsViews();
            CanDiscardUnsavedChanges = false;
            SystemSettings.ProfileStatusText = LocalizationManager.Translate(
                "已取消未保存修改，并恢复到最后一次成功保存的配置。");
        }
        catch (Exception exception)
        {
            SystemSettings.ProfileStatusText = LocalizationManager.IsEnglish
                ? $"Unable to discard unsaved changes: {exception.Message}"
                : $"取消未保存修改失败：{exception.Message}";
        }
    }

    private void ReloadSettingsViews()
    {
        _applyingViewModelState = true;
        try
        {
            LocalizationManager.SetLanguage(_settings.General.Language);
            PreviewToolTipPalette.Apply(_settings.Overlay.TextColor);
            ThumbnailSettings.LoadFrom(_settings);
            Clients.LoadFrom(_settings);
            CycleGroups.LoadFrom(_settings);
            Hotkeys.LoadFrom(_settings);
            Hotkeys.DuplicateText = string.Empty;
            SystemSettings.LoadFrom(_settings);
            Overview.RenderModeText = SystemSettings.RenderingModeText;
            Hotkeys.ConflictText = ValidateHotkeyText();
            if (_runtime is IPreviewRuntimeStatusSource statusSource)
            {
                ApplyRuntimeSnapshot(statusSource.CurrentSnapshot);
            }
        }
        finally
        {
            _applyingViewModelState = false;
        }
    }
}
