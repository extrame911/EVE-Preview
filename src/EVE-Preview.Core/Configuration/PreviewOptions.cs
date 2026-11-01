namespace EvePreview.Core;

public sealed class PreviewOptions
{
    public int DefaultWidth { get; set; } = 320;
    public int DefaultHeight { get; set; } = 180;
    public int MinWidth { get; set; } = 8;
    public int MinHeight { get; set; } = 8;
    public int MaxWidth { get; set; } = 1920;
    public int MaxHeight { get; set; } = 1080;
    public SnapMode SnapMode { get; set; } = SnapMode.Grid;
    public int GridSizeX { get; set; } = 32;
    public int GridSizeY { get; set; } = 32;
}

public sealed class DisplayOptions
{
    public double Opacity { get; set; } = 0.8;
    public bool Topmost { get; set; } = true;
    public bool LockPosition { get; set; }
    public bool HideOnFocusLoss { get; set; }
    public int HideDelayMs { get; set; }
    public bool HideActiveClient { get; set; }
    public int HideActiveDelayMs { get; set; }

    public bool HideWhenOutOfBounds { get; set; } = true;
    public int ExpansionThreshold { get; set; } = 128;
}

public sealed class ZoomOptions
{
    public bool Enabled { get; set; }
    public double Factor { get; set; } = 2;
    public ZoomAnchor Anchor { get; set; } = ZoomAnchor.TopRight;
    public int DelayMs { get; set; } = 100;
}

public sealed class OverlayOptions
{
    public bool ShowName { get; set; } = true;
    public bool ShowCharacterNameOnly { get; set; } = true;
    // Retained for configuration compatibility; rendering uses the application's shared typography.
    public string FontFamily { get; set; } = "Bahnschrift SemiCondensed, Microsoft YaHei UI";
    public int FontSize { get; set; } = 14;
    public string TextColor { get; set; } = "#FF8040";
    public OverlayPosition Position { get; set; } = OverlayPosition.TopCenter;
    public bool HighlightActive { get; set; } = true;
    public string HighlightColor { get; set; } = "#00FFFF";
    public int HighlightThickness { get; set; } = 1;
    public bool PrivacyMask { get; set; }
}
