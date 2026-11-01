using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvePreview.App.Services;
using EvePreview.Core;
using Microsoft.Win32;

namespace EvePreview.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void OnViewModelSettingChanged()
    {
        if (_applyingViewModelState)
        {
            return;
        }

        ApplySettingsToModel();
        if (_runtime is IPreviewRuntimeController)
        {
            ScheduleSettingsApply();
        }
    }

    private void ScheduleSettingsApply()
    {
        var version = Interlocked.Increment(ref _settingsApplyVersion);
        _ = ApplySettingsAfterDelayAsync(version);
    }

    private async Task ApplySettingsAfterDelayAsync(int version)
    {
        try
        {
            await Task.Delay(150);
            if (version != Volatile.Read(ref _settingsApplyVersion) ||
                _runtime is not IPreviewRuntimeController controller)
            {
                return;
            }

            await controller.ApplySettingsAsync(_settings);
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
        }
    }

    private void ApplySettingsToModel()
    {
        if (_applyingSettingsToModel)
        {
            return;
        }

        _applyingSettingsToModel = true;
        try
        {
            ApplySettingsToModelCore();
            Hotkeys.ConflictText = ValidateHotkeyText();
        }
        finally
        {
            _applyingSettingsToModel = false;
        }
    }

    private void ApplySettingsToModelCore()
    {
        _settings.General.Language = SystemSettings.Language == "English" ? "en-US" : "zh-CN";
        LocalizationManager.SetLanguage(_settings.General.Language);
        _settings.General.MinimizeToTray = SystemSettings.MinimizeToTray;
        _settings.General.CloseToTray = SystemSettings.CloseToTray;
        _settings.General.MinimizeInactiveClients = SystemSettings.MinimizeInactiveClients;
        _settings.General.UseWindowAnimations = SystemSettings.EnableWindowAnimations;
        _settings.Monitoring.ProcessNames = SystemSettings.ExecutableNames
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ProcessNameNormalizer.Normalize)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _settings.Monitoring.PollIntervalMs = SystemSettings.PollingIntervalMilliseconds;
        _settings.Compatibility.RenderingMode = SystemSettings.UseCompatibilityMode
            ? RenderingMode.Gdi
            : RenderingMode.Dwm;

        _settings.HotKeys.MinimizeAll = NormalizeHotKeyInput(Hotkeys.MinimizeAllHotkey, _settings.HotKeys.MinimizeAll);
        _settings.HotKeys.RefreshMinimized = NormalizeHotKeyInput(Hotkeys.RefreshMinimizedHotkey, _settings.HotKeys.RefreshMinimized);
        _settings.HotKeys.ReturnToPrevious = NormalizeHotKeyInput(Hotkeys.ReturnToPreviousHotkey, _settings.HotKeys.ReturnToPrevious);

        var minimumWidth = Math.Clamp(ThumbnailSettings.MinimumWidth, 8, 16384);
        var maximumWidth = Math.Clamp(ThumbnailSettings.MaximumWidth, minimumWidth, 16384);
        var defaultWidth = Math.Clamp(ThumbnailSettings.DefaultWidth, minimumWidth, maximumWidth);
        var minimumHeight = Math.Clamp(ThumbnailSettings.MinimumHeight, 8, 16384);
        var maximumHeight = Math.Clamp(ThumbnailSettings.MaximumHeight, minimumHeight, 16384);
        var defaultHeight = Math.Clamp(ThumbnailSettings.DefaultHeight, minimumHeight, maximumHeight);
        _settings.Preview.DefaultWidth = defaultWidth;
        _settings.Preview.DefaultHeight = defaultHeight;
        _settings.Preview.MinWidth = minimumWidth;
        _settings.Preview.MinHeight = minimumHeight;
        _settings.Preview.MaxWidth = maximumWidth;
        _settings.Preview.MaxHeight = maximumHeight;
        _settings.Preview.GridSizeX = Math.Clamp(ThumbnailSettings.GridSizeX, 1, 512);
        _settings.Preview.GridSizeY = Math.Clamp(ThumbnailSettings.GridSizeY, 1, 512);
        ThumbnailSettings.MinimumWidth = minimumWidth;
        ThumbnailSettings.MaximumWidth = maximumWidth;
        ThumbnailSettings.DefaultWidth = defaultWidth;
        ThumbnailSettings.MinimumHeight = minimumHeight;
        ThumbnailSettings.MaximumHeight = maximumHeight;
        ThumbnailSettings.DefaultHeight = defaultHeight;
        ThumbnailSettings.GridSizeX = _settings.Preview.GridSizeX;
        ThumbnailSettings.GridSizeY = _settings.Preview.GridSizeY;
        _settings.Preview.SnapMode = ThumbnailSettings.SnapToGrid
            ? SnapMode.Grid
            : ThumbnailSettings.SnapToEdges
                ? SnapMode.Edge
                : SnapMode.None;

        _settings.Display.Opacity = Math.Clamp(ThumbnailSettings.OpacityPercent / 100d, 0.2, 1.0);
        _settings.Display.Topmost = ThumbnailSettings.IsTopmost;
        _settings.Display.LockPosition = ThumbnailSettings.LockPosition;
        _settings.Display.HideOnFocusLoss = ThumbnailSettings.HideOnFocusLoss;
        _settings.Display.HideDelayMs = Math.Clamp(ThumbnailSettings.HideDelayMilliseconds, 0, 10000);
        ThumbnailSettings.HideDelayMilliseconds = _settings.Display.HideDelayMs;
        _settings.Display.HideActiveClient = ThumbnailSettings.HideActiveClient;
        _settings.Display.HideActiveDelayMs = Math.Clamp(ThumbnailSettings.HideActiveDelayMilliseconds, 0, 10000);
        ThumbnailSettings.HideActiveDelayMilliseconds = _settings.Display.HideActiveDelayMs;

        _settings.Display.HideWhenOutOfBounds = ThumbnailSettings.HideWhenOutOfBounds;
        _settings.Display.ExpansionThreshold = Math.Clamp(ThumbnailSettings.ExpansionThreshold, 0, 1000);
        ThumbnailSettings.ExpansionThreshold = _settings.Display.ExpansionThreshold;

        _settings.Zoom.Enabled = ThumbnailSettings.HoverZoomEnabled;
        _settings.Zoom.Factor = Math.Clamp(ThumbnailSettings.HoverZoomFactor, 1.0, 4.0);
        ThumbnailSettings.HoverZoomFactor = _settings.Zoom.Factor;
        _settings.Zoom.DelayMs = Math.Clamp(ThumbnailSettings.ZoomDelayMilliseconds, 0, 3000);
        ThumbnailSettings.ZoomDelayMilliseconds = _settings.Zoom.DelayMs;
        _settings.Zoom.Anchor = ThumbnailSettingsViewModel.ParseZoomAnchor(ThumbnailSettings.ZoomAnchor);

        _settings.Overlay.ShowName = ThumbnailSettings.ShowNames;
        _settings.Overlay.ShowCharacterNameOnly = ThumbnailSettings.ShowCharacterNameOnly;
        _settings.Overlay.FontSize = Math.Clamp(ThumbnailSettings.OverlayFontSize, 10, 32);
        ThumbnailSettings.OverlayFontSize = _settings.Overlay.FontSize;
        _settings.Overlay.TextColor = ThumbnailSettings.OverlayColor;
        PreviewToolTipPalette.Apply(_settings.Overlay.TextColor);
        _settings.Overlay.HighlightColor = ThumbnailSettings.HighlightColor;
        _settings.Overlay.Position = ThumbnailSettingsViewModel.ParseOverlayPosition(ThumbnailSettings.OverlayPosition);
        _settings.Overlay.HighlightActive = ThumbnailSettings.HighlightActiveClient;
        _settings.Overlay.PrivacyMask = ThumbnailSettings.PrivacyMask;
        Clients.SaveTo(_settings);
        CycleGroups.RefreshCandidates();

        for (var index = 0; index < CycleGroups.Groups.Count; index++)
        {
            if (index >= _settings.CycleGroups.Count)
            {
                _settings.CycleGroups.Add(new CycleGroupSettings());
            }
        }

        if (_settings.CycleGroups.Count > CycleGroups.Groups.Count)
        {
            _settings.CycleGroups.RemoveRange(
                CycleGroups.Groups.Count,
                _settings.CycleGroups.Count - CycleGroups.Groups.Count);
        }

        for (var index = 0; index < CycleGroups.Groups.Count && index < _settings.CycleGroups.Count; index++)
        {
            var source = CycleGroups.Groups[index];
            var target = _settings.CycleGroups[index];
            target.ForwardHotKey = NormalizeHotKeyInput(source.ForwardHotkey, target.ForwardHotKey);
            target.BackwardHotKey = NormalizeHotKeyInput(source.BackwardHotkey, target.BackwardHotKey);
            target.OrderedClientTitles = CycleGroups.GetOrderedTitles(index).ToList();
        }

        _settings.Normalize();
        ClearDuplicateHotKeys(_settings);
    }

    private static void ClearDuplicateHotKeys(AppSettings settings)
    {
        var bindings = new List<(string Value, Action<string> Set)>
        {
            (settings.HotKeys.MinimizeAll, value => settings.HotKeys.MinimizeAll = value),
            (settings.HotKeys.RefreshMinimized, value => settings.HotKeys.RefreshMinimized = value),
            (settings.HotKeys.ReturnToPrevious, value => settings.HotKeys.ReturnToPrevious = value)
        };
        foreach (var group in settings.CycleGroups)
        {
            bindings.Add((group.ForwardHotKey, value => group.ForwardHotKey = value));
            bindings.Add((group.BackwardHotKey, value => group.BackwardHotKey = value));
        }

        foreach (var client in settings.Clients)
        {
            bindings.Add((client.HotKey, value => client.HotKey = value));
        }

        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (value, set) in bindings)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!HotKeyParser.TryParse(value, out var hotkey) ||
                hotkey.MouseButton != HotKeyMouseButton.None ||
                hotkey.Modifiers == KeyModifiers.None ||
                !assigned.Add(hotkey.Canonical))
            {
                set(string.Empty);
            }
        }
    }

    private static string NormalizeHotKeyInput(string input, string previous)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return HotKeyParser.TryNormalize(input, out var normalized) ? normalized : previous;
    }

    private void ApplyViewModelState()
    {
        _applyingViewModelState = true;
        try
        {
            Overview.RenderModeText = SystemSettings.RenderingModeText;
            Hotkeys.ConflictText = ValidateHotkeyText();
        }
        finally
        {
            _applyingViewModelState = false;
        }
    }
}
