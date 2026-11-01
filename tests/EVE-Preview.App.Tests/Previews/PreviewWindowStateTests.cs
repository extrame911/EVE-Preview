using System.Reflection;
using EvePreview.App.Services;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class PreviewWindowStateTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Grid_snap_that_changes_size_stops_default_size_tracking()
    {
        wpf.Invoke(() =>
        {
            PreviewWindow? preview = null;
            try
            {
                var settings = new AppSettings();
                settings.Preview.SnapMode = SnapMode.Grid;
                settings.Preview.GridSizeX = 32;
                settings.Preview.GridSizeY = 32;
                preview = new PreviewWindow(
                    new ProcessSnapshot(1, "exefile", 101, "EVE - Alpha", DateTime.Now),
                    "EVE - Alpha",
                    settings,
                    new PreviewPlacement(10, 10, 320, 180),
                    (_, _) => { },
                    (_, _) => { },
                    useDefaultPhysicalSize: true);

                typeof(PreviewWindow)
                    .GetMethod("ApplyDragSnap", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(preview, null);

                var usesDefaultSize = (bool)typeof(PreviewWindow)
                    .GetField("_useDefaultPhysicalSize", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(preview)!;
                Assert.False(usesDefaultSize);
            }
            finally
            {
                preview?.Dispose();
            }
        });
    }

    [Fact]
    public void Dpi_change_during_hover_zoom_restores_zoom_and_default_size()
    {
        wpf.Invoke(() =>
        {
            PreviewWindow? preview = null;
            try
            {
                var settings = new AppSettings();
                settings.Zoom.Enabled = true;
                settings.Zoom.Factor = 2;
                settings.Zoom.DelayMs = 0;
                preview = new PreviewWindow(
                    new ProcessSnapshot(1, "exefile", 101, "EVE - Alpha", DateTime.Now),
                    "EVE - Alpha",
                    settings,
                    new PreviewPlacement(-10000, -10000, 320, 180),
                    (_, _) => { },
                    (_, _) => { },
                    useDefaultPhysicalSize: true);
                preview.Show();
                typeof(PreviewWindow)
                    .GetMethod("StartZoom", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(preview, null);
                Assert.True((bool)typeof(PreviewWindow)
                    .GetField("_zoomed", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(preview)!);

                typeof(PreviewWindow)
                    .GetMethod("HandleDpiChange", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(preview, null);

                Assert.False((bool)typeof(PreviewWindow)
                    .GetField("_zoomed", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(preview)!);
                Assert.True((bool)typeof(PreviewWindow)
                    .GetField("_useDefaultPhysicalSize", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(preview)!);
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(preview);
                Assert.Equal(320, Math.Round(preview.Width * dpi.DpiScaleX));
                Assert.Equal(180, Math.Round(preview.Height * dpi.DpiScaleY));
            }
            finally
            {
                preview?.Dispose();
            }
        });
    }
}
