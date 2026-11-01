using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class PreviewLayoutTests
{
    [Fact]
    public void LayoutRepresentsNormalHiddenAndMinimizedStates()
    {
        var layout = new PreviewLayout();

        Assert.Equal(PreviewLayoutState.Normal, layout.State);
        Assert.Equal(PreviewLayoutState.Hidden, (layout with { State = PreviewLayoutState.Hidden }).State);
        Assert.Equal(PreviewLayoutState.Minimized, (layout with { State = PreviewLayoutState.Minimized }).State);
    }

    [Fact]
    public void ValidationRejectsNonPositivePixelSize()
    {
        var layout = new PreviewLayout { Bounds = new PixelRectangle(10, 20, 0, 180) };

        var result = layout.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Bounds", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationRejectsNegativeExpansionThreshold()
    {
        var layout = new PreviewLayout { ExpansionThreshold = -1 };

        var result = layout.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("ExpansionThreshold", StringComparison.Ordinal));
    }

    [Fact]
    public void ClampKeepsRequestedGrabEdgeVisibleWhenBoundsHidingIsEnabled()
    {
        var layout = new PreviewLayout
        {
            Bounds = new PixelRectangle(5000, 5000, 320, 180),
            HideWhenOutOfBounds = true,
            ExpansionThreshold = 24
        };

        var clamped = layout.ClampToVisibleScreen(new PixelRectangle(0, 0, 1920, 1080));

        Assert.Equal(new PixelRectangle(1920 - 24, 1080 - 24, 320, 180), clamped);
    }

    [Fact]
    public void ClampLeavesOffscreenPositionsUntouchedWhenBoundsHidingIsDisabled()
    {
        var layout = new PreviewLayout
        {
            Bounds = new PixelRectangle(5000, 5000, 320, 180),
            HideWhenOutOfBounds = false
        };

        Assert.Equal(layout.Bounds, layout.ClampToVisibleScreen(new PixelRectangle(0, 0, 1920, 1080)));
    }
}
