using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class GridSnapperTests
{
    [Fact]
    public void SnapRoundsPositionAndSizeToNearest16PixelGridLine()
    {
        var bounds = new PixelRectangle(17, 31, 33, 47);

        var snapped = GridSnapper.Snap(bounds);

        Assert.Equal(new PixelRectangle(16, 32, 32, 48), snapped);
    }

    [Fact]
    public void SnapCanUseConfiguredGridSizePerAxis()
    {
        var bounds = new PixelRectangle(19, 23, 41, 37);

        var snapped = GridSnapper.Snap(bounds, gridSizeX: 10, gridSizeY: 5);

        Assert.Equal(new PixelRectangle(20, 25, 40, 35), snapped);
    }

    [Fact]
    public void SnapSupportsFractionalGridSizesForPhysicalPixelConversion()
    {
        var snapped = GridSnapper.Snap(
            new PreviewPlacement(50, 50, 100, 100),
            25.6,
            25.6);

        Assert.Equal(51.2, snapped.Left, 3);
        Assert.Equal(51.2, snapped.Top, 3);
        Assert.Equal(102.4, snapped.Width, 3);
        Assert.Equal(102.4, snapped.Height, 3);
    }

    [Fact]
    public void SnapNeverCollapsesWidthOrHeightToZero()
    {
        Assert.Equal(
            new PixelRectangle(0, 0, 512, 512),
            GridSnapper.Snap(new PixelRectangle(0, 0, 320, 180), 512, 512));
        Assert.Equal(
            new PixelRectangle(0, 0, 16, 16),
            GridSnapper.Snap(new PixelRectangle(0, 0, 4, 4), 16, 16));
    }
}
