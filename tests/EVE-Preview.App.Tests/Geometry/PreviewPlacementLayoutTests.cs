using System.Windows;
using EvePreview.App.Services;

namespace EvePreview.App.Tests;

public sealed class PreviewPlacementLayoutTests
{
    [Fact]
    public void Default_layout_converts_physical_dimensions_to_dip_before_spacing()
    {
        var placement = PreviewPlacementLayout.CreateDefault(
            new Rect(0, 0, 1920, 1080),
            index: 0,
            widthPixels: 320,
            heightPixels: 180,
            dpiScaleX: 2,
            dpiScaleY: 2);

        Assert.Equal(160, placement.Width);
        Assert.Equal(90, placement.Height);
        Assert.Equal(1920 - 160 - 12, placement.Left);
        Assert.Equal(24, placement.Top);
    }

    [Fact]
    public void Default_layout_uses_converted_height_when_wrapping_columns()
    {
        var placement = PreviewPlacementLayout.CreateDefault(
            new Rect(0, 0, 1920, 400),
            index: 3,
            widthPixels: 320,
            heightPixels: 180,
            dpiScaleX: 2,
            dpiScaleY: 2);

        Assert.Equal(1920 - 160 - 12 - (160 + 12), placement.Left);
        Assert.Equal(24, placement.Top);
    }

    [Fact]
    public void Default_layout_reuses_the_lowest_free_slot_after_a_client_exits()
    {
        Assert.Equal(1, PreviewPlacementLayout.FindFirstFreeSlot(new HashSet<int> { 0, 2 }));
        Assert.Equal(0, PreviewPlacementLayout.FindFirstFreeSlot(new HashSet<int>()));
    }
}
