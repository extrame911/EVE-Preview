using System.Windows;
using EvePreview.Core;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    public void ApplySettings(AppSettings settings, bool active)
    {
        var previousHideWhenOutOfBounds = _settings.Display.HideWhenOutOfBounds;
        var previousExpansionThreshold = _settings.Display.ExpansionThreshold;
        _settings = settings;
        _active = active;
        if ((!previousHideWhenOutOfBounds && settings.Display.HideWhenOutOfBounds) ||
            previousExpansionThreshold != settings.Display.ExpansionThreshold)
        {
            ApplyPlacement(_placement);
        }

        SetPreviewTooltip();
        if (!settings.Zoom.Enabled || settings.Display.LockPosition)
        {
            RestoreZoom();
        }

        Topmost = settings.Display.Topmost;
        Opacity = 1;
        _gdiImage.Opacity = Math.Clamp(settings.Display.Opacity, 0.1, 1.0);

        var privacy = settings.Overlay.PrivacyMask || FindClientSettings()?.PrivacyMask == true;
        ApplyPrivacyMode(privacy);
        _privacyOverlay.Visibility = privacy ? Visibility.Visible : Visibility.Collapsed;

        if (_gdiTimer is not null)
        {
            _gdiTimer.Interval = TimeSpan.FromSeconds(
                1d / Math.Clamp(settings.Compatibility.GdiFramesPerSecond, 1, 30));
        }

        RefreshVisibilityTimers();
        UpdateCompactState();
        UpdateThumbnailBounds();
        UpdateLayoutForDisplay();
        SyncOverlay();
    }

    public void SetActive(bool active) => ApplySettings(_settings, active);

    public void UpdateWindowTitle(string title)
    {
        _client = _client with { MainWindowTitle = title };
        if (!HasAlias)
        {
            DisplayName = title;
            Title = title;
        }

        SetPreviewTooltip();
        UpdateLayoutForDisplay();
        SyncOverlay();
    }

    public void UpdateDisplayName(string displayName, string? alias = null)
    {
        _alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        DisplayName = displayName;
        Title = displayName;
        SetPreviewTooltip();
        SyncOverlay();
    }
}
