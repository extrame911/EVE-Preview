namespace EvePreview.Core;

public readonly record struct PixelRectangle(int X, int Y, int Width, int Height);

public static class GridSnapper
{
    public static PixelRectangle Snap(PixelRectangle bounds) => Snap(bounds, 16, 16);

    public static PixelRectangle Snap(PixelRectangle bounds, int gridSizeX, int gridSizeY)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gridSizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gridSizeY);
        return new PixelRectangle(
            SnapPosition(bounds.X, gridSizeX),
            SnapPosition(bounds.Y, gridSizeY),
            SnapSize(bounds.Width, gridSizeX),
            SnapSize(bounds.Height, gridSizeY));
    }

    public static PreviewPlacement Snap(PreviewPlacement bounds, double gridSizeX, double gridSizeY)
    {
        ValidateGridSize(gridSizeX, nameof(gridSizeX));
        ValidateGridSize(gridSizeY, nameof(gridSizeY));
        return new PreviewPlacement(
            SnapPosition(bounds.Left, gridSizeX),
            SnapPosition(bounds.Top, gridSizeY),
            SnapSize(bounds.Width, gridSizeX),
            SnapSize(bounds.Height, gridSizeY));
    }

    private static void ValidateGridSize(double gridSize, string parameterName)
    {
        if (!double.IsFinite(gridSize) || gridSize <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static double SnapPosition(double value, double gridSize) =>
        Math.Round(value / gridSize, MidpointRounding.AwayFromZero) * gridSize;

    private static double SnapSize(double value, double gridSize) =>
        Math.Max(gridSize, SnapPosition(value, gridSize));

    private static int SnapPosition(int value, int gridSize) =>
        (int)Math.Round(value / (double)gridSize, MidpointRounding.AwayFromZero) * gridSize;

    private static int SnapSize(int value, int gridSize) =>
        Math.Max(gridSize, SnapPosition(value, gridSize));
}
