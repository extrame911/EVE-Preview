using System.Windows;
using System.Windows.Media;
using EvePreview.App.Services;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class PreviewPixelSizeTests(WpfApplicationFixture wpf)
{
    [Theory]
    [InlineData(320, 1.0, 320)]
    [InlineData(320, 1.25, 256)]
    [InlineData(320, 2.0, 160)]
    [InlineData(180, 1.5, 120)]
    public void Physical_pixels_convert_to_dip_at_the_target_monitor_scale(
        int pixels,
        double dpiScale,
        double expectedDip)
    {
        Assert.Equal(expectedDip, PreviewPixelSize.ToDip(pixels, dpiScale));
    }

    [Fact]
    public void Default_preview_size_matches_physical_pixels_at_the_current_monitor_dpi()
    {
        wpf.Invoke(() =>
        {
            PreviewWindow? preview = null;
            try
            {
                var settings = new AppSettings();
                preview = new PreviewWindow(
                    new ProcessSnapshot(1, "exefile", 101, "EVE - Alpha", DateTime.Now),
                    "EVE - Alpha",
                    settings,
                    new PreviewPlacement(-10000, -10000, 320, 180),
                    (_, _) => { },
                    (_, _) => { },
                    useDefaultPhysicalSize: true);
                preview.Show();

                var dpi = VisualTreeHelper.GetDpi(preview);
                var primaryScale = PreviewMonitorDpi.GetPrimaryScale();
                Assert.Equal(dpi.DpiScaleX, primaryScale.X, 3);
                Assert.Equal(dpi.DpiScaleY, primaryScale.Y, 3);
                var physicalSize = TestWindowGeometry.GetPhysicalSize(preview);
                Assert.Equal(320, physicalSize.Width);
                Assert.Equal(180, physicalSize.Height);

                var resized = settings.DeepCopy();
                resized.Preview.DefaultWidth = 640;
                resized.Preview.DefaultHeight = 360;
                preview.ApplyDefaultPhysicalSize(resized);
                physicalSize = TestWindowGeometry.GetPhysicalSize(preview);
                Assert.Equal(640, physicalSize.Width);
                Assert.Equal(360, physicalSize.Height);
            }
            finally
            {
                preview?.Dispose();
            }
        });
    }

    [Fact]
    public void Out_of_bounds_margin_uses_physical_pixels_at_the_current_monitor_dpi()
    {
        wpf.Invoke(() =>
        {
            PreviewWindow? preview = null;
            try
            {
                var settings = new AppSettings();
                settings.Display.HideWhenOutOfBounds = true;
                settings.Display.ExpansionThreshold = 100;
                preview = new PreviewWindow(
                    new ProcessSnapshot(1, "exefile", 101, "EVE - Alpha", DateTime.Now),
                    "EVE - Alpha",
                    settings,
                    new PreviewPlacement(0, 0, 320, 180),
                    (_, _) => { },
                    (_, _) => { });
                preview.Show();
                preview.ApplyPlacement(new PreviewPlacement(5000, 5000, 320, 180));

                var dpi = VisualTreeHelper.GetDpi(preview);
                var screenRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
                var expectedMargin = Math.Round(PreviewPixelSize.ToDip(100, dpi.DpiScaleX));
                Assert.Equal(screenRight - expectedMargin, preview.Left, 3);
            }
            finally
            {
                preview?.Dispose();
            }
        });
    }
}
