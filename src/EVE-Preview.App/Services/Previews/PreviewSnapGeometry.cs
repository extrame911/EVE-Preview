using EvePreview.Core;

namespace EvePreview.App.Services;

public static class PreviewSnapGeometry
{
    public static PixelRectangle Snap(
        PixelRectangle bounds,
        PixelRectangle workArea,
        SnapMode mode,
        int gridSizeX,
        int gridSizeY,
        int minWidth,
        int minHeight,
        int maxWidth,
        int maxHeight,
        int edgeThresholdPixels)
    {
        if (mode == SnapMode.Grid)
        {
            var gridX = Math.Max(1, gridSizeX);
            var gridY = Math.Max(1, gridSizeY);
            return new PixelRectangle(
                SnapCoordinate(bounds.X, workArea.X, gridX),
                SnapCoordinate(bounds.Y, workArea.Y, gridY),
                Math.Clamp(SnapSize(bounds.Width, gridX), minWidth, maxWidth),
                Math.Clamp(SnapSize(bounds.Height, gridY), minHeight, maxHeight));
        }

        if (mode != SnapMode.Edge)
        {
            return bounds;
        }

        var x = bounds.X;
        var y = bounds.Y;
        var threshold = Math.Max(0, edgeThresholdPixels);
        if (Math.Abs(x - workArea.X) < threshold)
        {
            x = workArea.X;
        }
        else if (Math.Abs(x + bounds.Width - (workArea.X + workArea.Width)) < threshold)
        {
            x = workArea.X + workArea.Width - bounds.Width;
        }

        if (Math.Abs(y - workArea.Y) < threshold)
        {
            y = workArea.Y;
        }
        else if (Math.Abs(y + bounds.Height - (workArea.Y + workArea.Height)) < threshold)
        {
            y = workArea.Y + workArea.Height - bounds.Height;
        }

        return new PixelRectangle(x, y, bounds.Width, bounds.Height);
    }

    private static int SnapCoordinate(int value, int origin, int gridSize) =>
        origin + (int)Math.Round((value - origin) / (double)gridSize, MidpointRounding.AwayFromZero) * gridSize;

    private static int SnapSize(int value, int gridSize) =>
        Math.Max(gridSize, (int)Math.Round(value / (double)gridSize, MidpointRounding.AwayFromZero) * gridSize);
}
