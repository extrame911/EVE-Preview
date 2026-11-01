using CommunityToolkit.Mvvm.ComponentModel;
using EvePreview.App.Services;
using EvePreview.Core;
using Anchors = EvePreview.Core.ZoomAnchor;
using OverlayPositions = EvePreview.Core.OverlayPosition;

namespace EvePreview.App.ViewModels;

public sealed partial class ThumbnailSettingsViewModel : ObservableObject
{
    public const string OpacitySettingName = "Thumbnails.OpacityPercent";

    public ThumbnailSettingsViewModel()
    {
    }

    public ThumbnailSettingsViewModel(AppSettings settings)
    {
        LoadFrom(settings);
    }

    [ObservableProperty]
    private int _defaultWidth = 320;

    [ObservableProperty]
    private int _defaultHeight = 180;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeLimitsText))]
    private int _minimumWidth = 8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeLimitsText))]
    private int _minimumHeight = 8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeLimitsText))]
    private int _maximumWidth = 1920;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeLimitsText))]
    private int _maximumHeight = 1080;

    [ObservableProperty]
    private bool _isTopmost = true;

    [ObservableProperty]
    private bool _lockPosition;

    [ObservableProperty]
    private bool _hideOnFocusLoss = true;

    [ObservableProperty]
    private int _hideDelayMilliseconds;

    [ObservableProperty]
    private bool _hideActiveClient;

    [ObservableProperty]
    private int _hideActiveDelayMilliseconds;

    [ObservableProperty]
    private bool _hoverZoomEnabled = true;

    [ObservableProperty]
    private double _hoverZoomFactor = 1.15;

    [ObservableProperty]
    private int _zoomDelayMilliseconds;

    [ObservableProperty]
    private string _zoomAnchor = "中心";

    [ObservableProperty]
    private bool _showNames = true;

    [ObservableProperty]
    private bool _showCharacterNameOnly = true;

    [ObservableProperty]
    private int _overlayFontSize = 12;

    [ObservableProperty]
    private string _overlayColor = "#F3F6F8";

    [ObservableProperty]
    private string _highlightColor = "#4CC2FF";

    [ObservableProperty]
    private string _overlayPosition = "顶部";

    [ObservableProperty]
    private bool _highlightActiveClient = true;

    [ObservableProperty]
    private bool _privacyMask;

    [ObservableProperty]
    private int _gridSizeX = 32;

    [ObservableProperty]
    private int _gridSizeY = 32;

    private double _opacityPercent = 90;

    [ObservableProperty]
    private string _opacityError = string.Empty;

    [ObservableProperty]
    private bool _snapToEdges = true;

    [ObservableProperty]
    private bool _snapToGrid;

    [ObservableProperty]
    private bool _hideWhenOutOfBounds;

    [ObservableProperty]
    private int _expansionThreshold = 24;

    public string SizeLimitsText => LocalizationManager.IsEnglish
        ? $"Size range: minimum {MinimumWidth} × {MinimumHeight} px, maximum {MaximumWidth} × {MaximumHeight} px"
        : $"尺寸范围：最小 {MinimumWidth} × {MinimumHeight} 像素，最大 {MaximumWidth} × {MaximumHeight} 像素";

    partial void OnDefaultWidthChanged(int value)
    {
        var clamped = Math.Clamp(value, Math.Min(MinimumWidth, MaximumWidth), MaximumWidth);
        if (clamped != value)
        {
            DefaultWidth = clamped;
        }
    }

    partial void OnDefaultHeightChanged(int value)
    {
        var clamped = Math.Clamp(value, Math.Min(MinimumHeight, MaximumHeight), MaximumHeight);
        if (clamped != value)
        {
            DefaultHeight = clamped;
        }
    }

    partial void OnSnapToEdgesChanged(bool value)
    {
        if (value && SnapToGrid)
        {
            SnapToGrid = false;
        }

        SettingChanged?.Invoke(this, new SettingChangedEventArgs("Thumbnails.SnapToEdges", value));
    }

    partial void OnSnapToGridChanged(bool value)
    {
        if (value && SnapToEdges)
        {
            SnapToEdges = false;
        }

        SettingChanged?.Invoke(this, new SettingChangedEventArgs("Thumbnails.SnapToGrid", value));
    }

    public event EventHandler<SettingChangedEventArgs>? SettingChanged;

    public double OpacityPercent
    {
        get => _opacityPercent;
        set
        {
            var clamped = Math.Clamp(value, 20, 100);
            OpacityError = string.Empty;
            if (!SetProperty(ref _opacityPercent, clamped))
            {
                if (clamped != value)
                {
                    OnPropertyChanged(nameof(OpacityPercent));
                }

                return;
            }

            SettingChanged?.Invoke(this, new SettingChangedEventArgs(OpacitySettingName, clamped));
        }
    }

    public void LoadFrom(AppSettings settings)
    {
        MinimumWidth = settings.Preview.MinWidth;
        MinimumHeight = settings.Preview.MinHeight;
        MaximumWidth = settings.Preview.MaxWidth;
        MaximumHeight = settings.Preview.MaxHeight;
        DefaultWidth = settings.Preview.DefaultWidth;
        DefaultHeight = settings.Preview.DefaultHeight;
        IsTopmost = settings.Display.Topmost;
        LockPosition = settings.Display.LockPosition;
        HideOnFocusLoss = settings.Display.HideOnFocusLoss;
        HideDelayMilliseconds = settings.Display.HideDelayMs;
        HideActiveClient = settings.Display.HideActiveClient;
        HideActiveDelayMilliseconds = settings.Display.HideActiveDelayMs;

        HoverZoomEnabled = settings.Zoom.Enabled;
        HoverZoomFactor = settings.Zoom.Factor;
        ZoomDelayMilliseconds = settings.Zoom.DelayMs;
        ZoomAnchor = ZoomAnchorText(settings.Zoom.Anchor);
        ShowNames = settings.Overlay.ShowName;
        ShowCharacterNameOnly = settings.Overlay.ShowCharacterNameOnly;
        OverlayFontSize = settings.Overlay.FontSize;
        OverlayColor = settings.Overlay.TextColor;
        HighlightColor = settings.Overlay.HighlightColor;
        OverlayPosition = OverlayPositionText(settings.Overlay.Position);
        HighlightActiveClient = settings.Overlay.HighlightActive;
        PrivacyMask = settings.Overlay.PrivacyMask;
        GridSizeX = settings.Preview.GridSizeX;
        GridSizeY = settings.Preview.GridSizeY;
        HideWhenOutOfBounds = settings.Display.HideWhenOutOfBounds;
        ExpansionThreshold = settings.Display.ExpansionThreshold;
        OpacityPercent = Math.Round(settings.Display.Opacity * 100, 2);
        SnapToGrid = false;
        SnapToEdges = false;
        if (settings.Preview.SnapMode == SnapMode.Edge)
        {
            SnapToEdges = true;
        }
        else if (settings.Preview.SnapMode == SnapMode.Grid)
        {
            SnapToGrid = true;
        }
    }

    public static string ZoomAnchorText(Anchors anchor)
    {
        var source = anchor switch
        {
            Anchors.TopLeft => "左上",
            Anchors.Top => "上",
            Anchors.TopRight => "右上",
            Anchors.Left => "左",
            Anchors.Center => "中心",
            Anchors.Right => "右",
            Anchors.BottomLeft => "左下",
            Anchors.Bottom => "下",
            Anchors.BottomRight => "右下",
            _ => "中心"
        };
        return source;
    }

    public static string OverlayPositionText(OverlayPositions position)
    {
        var source = position switch
        {
            OverlayPositions.TopCenter => "中上",
            OverlayPositions.TopRight => "右上",
            OverlayPositions.BottomCenter => "中下",
            OverlayPositions.BottomRight => "右下",
            OverlayPositions.BottomLeft => "左下",
            OverlayPositions.Center => "中间",
            _ => "左上"
        };
        return source;
    }

    public static Anchors ParseZoomAnchor(string? text) => text switch
    {
        "左上" or "Top left" => Anchors.TopLeft,
        "上" or "Top" => Anchors.Top,
        "右上" or "Top right" => Anchors.TopRight,
        "左" or "Left" => Anchors.Left,
        "右" or "Right" => Anchors.Right,
        "左下" or "Bottom left" => Anchors.BottomLeft,
        "下" or "Bottom" => Anchors.Bottom,
        "右下" or "Bottom right" => Anchors.BottomRight,
        _ => Anchors.Center
    };

    public static OverlayPositions ParseOverlayPosition(string? text) => text switch
    {
        "中上" or "Top center" => OverlayPositions.TopCenter,
        "右上" or "Top right" => OverlayPositions.TopRight,
        "中下" or "Bottom center" => OverlayPositions.BottomCenter,
        "左下" or "Bottom left" => OverlayPositions.BottomLeft,
        "右下" or "Bottom right" => OverlayPositions.BottomRight,
        "中间" or "Center" => OverlayPositions.Center,
        _ => OverlayPositions.TopLeft
    };
}
