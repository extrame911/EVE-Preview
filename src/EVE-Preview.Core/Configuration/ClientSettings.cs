namespace EvePreview.Core;

public sealed class ClientSettings
{
    public string OriginalTitle { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string HotKey { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public ZoomAnchor? ZoomAnchor { get; set; }
    public string HighlightColor { get; set; } = string.Empty;
    public bool PrivacyMask { get; set; }
    public bool ExcludeFromCycle { get; set; }
    public bool DisablePreview { get; set; }
    public PreviewPlacement? Placement { get; set; }
    public PixelRectangle? ClientWindowBounds { get; set; }
}

public sealed record PreviewPlacement(double Left, double Top, double Width, double Height)
{
    public ValidationResult Validate()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top) ||
            !double.IsFinite(Width) || !double.IsFinite(Height) ||
            Width < 1 || Height < 1)
        {
            return new ValidationResult(["Preview placement must have finite coordinates and a positive size."]);
        }

        return ValidationResult.Success;
    }
}
