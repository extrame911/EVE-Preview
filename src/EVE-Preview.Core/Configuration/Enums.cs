namespace EvePreview.Core;

public enum SnapMode
{
    None,
    Edge,
    Grid
}

public enum ZoomAnchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

public enum OverlayPosition
{
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3,
    TopCenter = 4,
    BottomCenter = 5,
    Center = 6
}

public enum RenderingMode
{
    Dwm,
    Gdi
}
