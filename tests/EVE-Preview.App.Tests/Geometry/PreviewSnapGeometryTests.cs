using EvePreview.App.Services;
using EvePreview.Core;

namespace EvePreview.App.Tests;

public sealed class PreviewSnapGeometryTests
{
    [Fact]
    public void Monitor_lookup_returns_a_positive_work_area_and_scale_for_the_primary_point()
    {
        var monitor = MonitorGeometry.FromPoint(0, 0);

        Assert.NotNull(monitor);
        Assert.True(monitor.WorkArea.Width > 0);
        Assert.True(monitor.WorkArea.Height > 0);
        Assert.InRange(monitor.DpiScaleX, 0.5, 4);
        Assert.InRange(monitor.DpiScaleY, 0.5, 4);
    }

    [Fact]
    public void Grid_snap_uses_the_selected_monitor_work_area_origin()
    {
        var snapped = PreviewSnapGeometry.Snap(
            new PixelRectangle(1930, 40, 300, 200),
            new PixelRectangle(1920, 0, 1920, 1080),
            SnapMode.Grid,
            gridSizeX: 32,
            gridSizeY: 32,
            minWidth: 8,
            minHeight: 8,
            maxWidth: 1920,
            maxHeight: 1080,
            edgeThresholdPixels: 16);

        Assert.Equal(new PixelRectangle(1920, 32, 288, 192), snapped);
    }

    [Fact]
    public void Grid_snap_supports_monitors_with_negative_origins()
    {
        var snapped = PreviewSnapGeometry.Snap(
            new PixelRectangle(-1900, 40, 300, 200),
            new PixelRectangle(-1920, 0, 1920, 1080),
            SnapMode.Grid,
            gridSizeX: 32,
            gridSizeY: 32,
            minWidth: 8,
            minHeight: 8,
            maxWidth: 1920,
            maxHeight: 1080,
            edgeThresholdPixels: 16);

        Assert.Equal(new PixelRectangle(-1888, 32, 288, 192), snapped);
    }

    [Fact]
    public void Edge_snap_uses_the_selected_monitor_edges()
    {
        var snapped = PreviewSnapGeometry.Snap(
            new PixelRectangle(3532, 10, 300, 200),
            new PixelRectangle(1920, 0, 1920, 1080),
            SnapMode.Edge,
            gridSizeX: 32,
            gridSizeY: 32,
            minWidth: 8,
            minHeight: 8,
            maxWidth: 1920,
            maxHeight: 1080,
            edgeThresholdPixels: 16);

        Assert.Equal(new PixelRectangle(3540, 0, 300, 200), snapped);
    }

    [Fact]
    public void Edge_snap_leaves_bounds_unchanged_outside_the_threshold()
    {
        var bounds = new PixelRectangle(2100, 300, 300, 200);

        var snapped = PreviewSnapGeometry.Snap(
            bounds,
            new PixelRectangle(1920, 0, 1920, 1080),
            SnapMode.Edge,
            gridSizeX: 32,
            gridSizeY: 32,
            minWidth: 8,
            minHeight: 8,
            maxWidth: 1920,
            maxHeight: 1080,
            edgeThresholdPixels: 16);

        Assert.Equal(bounds, snapped);
    }
}
