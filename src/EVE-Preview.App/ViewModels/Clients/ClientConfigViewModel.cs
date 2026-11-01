using CommunityToolkit.Mvvm.ComponentModel;
using EvePreview.App.Services;
using EvePreview.Core;

namespace EvePreview.App.ViewModels;

public sealed partial class ClientConfigViewModel : ObservableObject
{
    private PreviewPlacement? _placement;
    private bool _hasSavedPlacement;

    public ClientConfigViewModel(ClientSettings settings)
    {
        OriginalTitle = settings.OriginalTitle;
        _placement = settings.Placement;
        _hasSavedPlacement = settings.Placement is not null;
        _alias = settings.Alias;
        _hotKey = settings.HotKey;
        _highlightColor = settings.HighlightColor;
        _privacyMask = settings.PrivacyMask;
        _excludeFromCycle = settings.ExcludeFromCycle;
        _disablePreview = settings.DisablePreview;
        _placementText = FormatPlacement(settings.Placement, _hasSavedPlacement);
        _clientWindowLayoutText = FormatClientWindowBounds(settings.ClientWindowBounds);
    }

    public string OriginalTitle { get; private set; }

    public void RenameOriginalTitle(string originalTitle)
    {
        if (string.Equals(OriginalTitle, originalTitle, StringComparison.Ordinal))
        {
            return;
        }

        OriginalTitle = originalTitle;
        OnPropertyChanged(nameof(OriginalTitle));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(CharacterName));
        OnPropertyChanged(nameof(ServerName));
        OnPropertyChanged(nameof(AutomationName));
        OnPropertyChanged(nameof(OnlineStatusDetail));
    }

    [ObservableProperty]
    private string _alias = string.Empty;

    [ObservableProperty]
    private string _hotKey = string.Empty;

    [ObservableProperty]
    private string _highlightColor = string.Empty;

    [ObservableProperty]
    private bool _privacyMask;

    [ObservableProperty]
    private bool _excludeFromCycle;

    [ObservableProperty]
    private bool _disablePreview;

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private string _liveWindowTitle = string.Empty;

    [ObservableProperty]
    private string _placementText = "尚未保存独立布局";

    [ObservableProperty]
    private string _clientWindowLayoutText = "尚未保存客户端窗口布局";

    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? OriginalTitle : Alias;
    public string CharacterName
    {
        get
        {
            var title = EffectiveWindowTitle;
            return ClientWindowTitle.TrySplit(title, out _, out var characterName) &&
                   !string.IsNullOrWhiteSpace(characterName)
                ? characterName
                : "[未登录]";
        }
    }

    public string ServerName
    {
        get
        {
            var title = EffectiveWindowTitle;
            return ClientWindowTitle.TrySplit(title, out var gameTitle, out _)
                ? gameTitle
                : title;
        }
    }

    /// <summary>Name exposed to UI automation and screen readers for the client list item.</summary>
    public string AutomationName => $"{CharacterName} · {ServerName}";

    public string OnlineStatusText => LocalizationManager.Translate(IsOnline ? "在线" : "离线");
    public string OnlineStatusDetail => IsOnline
        ? LocalizationManager.IsEnglish
            ? $"Window: {LiveWindowTitle}"
            : $"窗口：{LiveWindowTitle}"
        : LocalizationManager.Translate("不会对其执行窗口操作");

    private string EffectiveWindowTitle =>
        string.IsNullOrWhiteSpace(LiveWindowTitle) ? OriginalTitle : LiveWindowTitle;

    partial void OnAliasChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(OnlineStatusDetail));
    }

    partial void OnIsOnlineChanged(bool value)
    {
        OnPropertyChanged(nameof(OnlineStatusText));
        OnPropertyChanged(nameof(OnlineStatusDetail));
    }

    partial void OnLiveWindowTitleChanged(string value)
    {
        OnPropertyChanged(nameof(CharacterName));
        OnPropertyChanged(nameof(ServerName));
        OnPropertyChanged(nameof(AutomationName));
        OnPropertyChanged(nameof(OnlineStatusDetail));
    }

    public void ApplyLiveStatus(PreviewClientStatus? status)
    {
        IsOnline = status is not null;
        LiveWindowTitle = status?.OriginalWindowTitle ?? string.Empty;
        if (status is not null)
        {
            _hasSavedPlacement = status.HasSavedPlacement;
            if (status.Placement is not null)
            {
                _placement = status.Placement;
            }
        }

        PlacementText = status?.Placement is { } placement
            ? FormatPlacement(placement, status.DpiScaleX, status.DpiScaleY, status.HasSavedPlacement)
            : FormatPlacement(_placement, _hasSavedPlacement);
    }

    public void ApplyTo(ClientSettings target)
    {
        target.OriginalTitle = OriginalTitle;
        target.Alias = Alias.Trim();
        target.HotKey = string.IsNullOrWhiteSpace(HotKey)
            ? string.Empty
            : HotKeyParser.TryNormalize(HotKey, out var normalized) ? normalized : target.HotKey;
        target.HighlightColor = HighlightColor.Trim();
        target.PrivacyMask = PrivacyMask;
        target.ExcludeFromCycle = ExcludeFromCycle;
        target.DisablePreview = DisablePreview;
    }

    private static string FormatPlacement(PreviewPlacement? placement, bool hasSavedPlacement) =>
        placement is null || !hasSavedPlacement
            ? LocalizationManager.Translate("尚未保存独立布局")
            : LocalizationManager.Translate("已保存独立布局；上线后显示像素位置与尺寸");

    private static string FormatPlacement(
        PreviewPlacement placement,
        double dpiScaleX,
        double dpiScaleY,
        bool hasSavedPlacement)
    {
        if (!hasSavedPlacement)
        {
            return LocalizationManager.IsEnglish
                ? $"Current position {PreviewPixelSize.ToPixels(placement.Left, dpiScaleX):0} × {PreviewPixelSize.ToPixels(placement.Top, dpiScaleY):0}, " +
                  $"size {PreviewPixelSize.ToPixels(placement.Width, dpiScaleX):0} × {PreviewPixelSize.ToPixels(placement.Height, dpiScaleY):0} px (unsaved)"
                : $"当前位置 {PreviewPixelSize.ToPixels(placement.Left, dpiScaleX):0} × {PreviewPixelSize.ToPixels(placement.Top, dpiScaleY):0}，" +
                  $"尺寸 {PreviewPixelSize.ToPixels(placement.Width, dpiScaleX):0} × {PreviewPixelSize.ToPixels(placement.Height, dpiScaleY):0} 像素（未保存）";
        }

        return LocalizationManager.IsEnglish
            ? $"Position {PreviewPixelSize.ToPixels(placement.Left, dpiScaleX):0} × {PreviewPixelSize.ToPixels(placement.Top, dpiScaleY):0}, " +
              $"size {PreviewPixelSize.ToPixels(placement.Width, dpiScaleX):0} × {PreviewPixelSize.ToPixels(placement.Height, dpiScaleY):0} px"
            : $"位置 {PreviewPixelSize.ToPixels(placement.Left, dpiScaleX):0} × {PreviewPixelSize.ToPixels(placement.Top, dpiScaleY):0}，" +
              $"尺寸 {PreviewPixelSize.ToPixels(placement.Width, dpiScaleX):0} × {PreviewPixelSize.ToPixels(placement.Height, dpiScaleY):0} 像素";
    }

    private static string FormatClientWindowBounds(PixelRectangle? bounds) =>
        bounds is null
            ? LocalizationManager.Translate("尚未保存客户端窗口布局")
            : LocalizationManager.IsEnglish
                ? $"Client window: ({bounds.Value.X}, {bounds.Value.Y}), {bounds.Value.Width} × {bounds.Value.Height} px"
                : $"客户端窗口：({bounds.Value.X}, {bounds.Value.Y})，{bounds.Value.Width} × {bounds.Value.Height} 像素";

    public void SetClientWindowBounds(PixelRectangle bounds) =>
        ClientWindowLayoutText = FormatClientWindowBounds(bounds);

    public void ClearSettings()
    {
        _placement = null;
        _hasSavedPlacement = false;
        Alias = string.Empty;
        HotKey = string.Empty;
        HighlightColor = string.Empty;
        PrivacyMask = false;
        ExcludeFromCycle = false;
        DisablePreview = false;
        PlacementText = FormatPlacement(null, false);
        ClientWindowLayoutText = FormatClientWindowBounds(null);
    }
}
