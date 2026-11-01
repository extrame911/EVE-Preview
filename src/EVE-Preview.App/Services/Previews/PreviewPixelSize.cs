namespace EvePreview.App.Services;

public static class PreviewPixelSize
{
    public static double ToDip(int pixels, double dpiScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pixels);
        if (!double.IsFinite(dpiScale) || dpiScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale));
        }

        return pixels / dpiScale;
    }

    public static double ToPixels(double dip, double dpiScale)
    {
        if (!double.IsFinite(dip))
        {
            throw new ArgumentOutOfRangeException(nameof(dip));
        }

        if (!double.IsFinite(dpiScale) || dpiScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale));
        }

        return dip * dpiScale;
    }
}
