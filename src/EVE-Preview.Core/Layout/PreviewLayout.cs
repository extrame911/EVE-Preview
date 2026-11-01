namespace EvePreview.Core;

public enum PreviewLayoutState
{
    Normal,
    Hidden,
    Minimized
}

public sealed record PreviewLayout
{
    public PreviewLayoutState State { get; init; } = PreviewLayoutState.Normal;
    public PixelRectangle Bounds { get; init; } = new(0, 0, 320, 180);
    public bool HideWhenOutOfBounds { get; init; }
    public int ExpansionThreshold { get; init; } = 24;

    public ValidationResult Validate()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return new ValidationResult(["Bounds width and height must be greater than zero."]);
        }

        if (ExpansionThreshold < 0)
        {
            return new ValidationResult(["ExpansionThreshold must not be negative."]);
        }

        return ValidationResult.Success;
    }

    public PixelRectangle ClampToVisibleScreen(PixelRectangle screen)
    {
        if (!HideWhenOutOfBounds)
        {
            return Bounds;
        }

        var width = Math.Max(1, Bounds.Width);
        var height = Math.Max(1, Bounds.Height);
        var visibleEdgeX = Math.Clamp(ExpansionThreshold, 0, Math.Max(0, width - 1));
        var visibleEdgeY = Math.Clamp(ExpansionThreshold, 0, Math.Max(0, height - 1));
        var minimumX = screen.X - width + visibleEdgeX;
        var maximumX = screen.X + screen.Width - visibleEdgeX;
        var minimumY = screen.Y - height + visibleEdgeY;
        var maximumY = screen.Y + screen.Height - visibleEdgeY;
        return new PixelRectangle(
            Math.Clamp(Bounds.X, minimumX, maximumX),
            Math.Clamp(Bounds.Y, minimumY, maximumY),
            width,
            height);
    }
}
