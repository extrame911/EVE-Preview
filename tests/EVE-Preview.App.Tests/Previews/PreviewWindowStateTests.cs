using System.Reflection;
using EvePreview.App.Services;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class PreviewWindowStateTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void External_placement_updates_are_ignored_during_a_drag()
    {
        wpf.Invoke(() =>
        {
            PreviewWindow? preview = null;
            try
            {
                preview = new PreviewWindow(
                    new ProcessSnapshot(1, "exefile", 101, "EVE - Alpha", DateTime.Now),
                    "EVE - Alpha",
                    new AppSettings(),
                    new PreviewPlacement(10, 10, 320, 180),
                    (_, _) => { },
                    (_, _) => { });
                preview.Show();
                typeof(PreviewWindow)
                    .GetField("_rightDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(preview, true);

                preview.ApplyPlacement(new PreviewPlacement(300, 200, 320, 180));

                Assert.Equal(10, preview.Left);
                Assert.Equal(10, preview.Top);
            }
            finally
            {
                preview?.Dispose();
            }
        });
    }

    [Fact]
    public void Managed_resize_is_blocked_until_the_right_button_is_released()
    {
        wpf.Invoke(() =>
        {
            PreviewWindow? preview = null;
            try
            {
                preview = new PreviewWindow(
                    new ProcessSnapshot(1, "exefile", 101, "EVE - Alpha", DateTime.Now),
                    "EVE - Alpha",
                    new AppSettings(),
                    new PreviewPlacement(10, 10, 320, 180),
                    (_, _) => { },
                    (_, _) => { });
                preview.Show();
                var target = new PixelRectangle(500, 400, 640, 360);
                typeof(PreviewWindow)
                    .GetField("_rightDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(preview, true);
                typeof(PreviewWindow)
                    .GetField("_physicalDragTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(preview, target);

                preview.Width = 999;
                preview.Height = 777;

                var boundsArguments = new object?[] { null };
                var hasBounds = (bool)typeof(PreviewWindow)
                    .GetMethod("TryGetPhysicalWindowBounds", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(preview, boundsArguments)!;
                Assert.True(hasBounds);
                var actual = (PixelRectangle)boundsArguments[0]!;
                Assert.Equal(target.Width, actual.Width);
                Assert.Equal(target.Height, actual.Height);
            }
            finally
            {
                preview?.Dispose();
            }
        });
    }

    [Fact]
    public void Hover_zoom_does_not_start_while_a_drag_is_active()
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
                    new PreviewPlacement(10, 10, 320, 180),
                    (_, _) => { },
                    (_, _) => { });
                typeof(PreviewWindow)
                    .GetField("_rightDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(preview, true);
                typeof(PreviewWindow)
                    .GetMethod("StartZoom", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(preview, null);

                Assert.False((bool)typeof(PreviewWindow)
                    .GetField("_zoomed", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(preview)!);
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
                    .GetValue(preview)!, "StartZoom should set zoomed state");

                typeof(PreviewWindow)
                    .GetMethod(
                        "HandleDpiChange",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        binder: null,
                        types: Type.EmptyTypes,
                        modifiers: null)!
                    .Invoke(preview, null);

                Assert.False((bool)typeof(PreviewWindow)
                    .GetField("_zoomed", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(preview)!);
                Assert.True((bool)typeof(PreviewWindow)
                    .GetField("_useDefaultPhysicalSize", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(preview)!, "DPI change should preserve default physical sizing");
                var physicalSize = TestWindowGeometry.GetPhysicalSize(preview);
                Assert.Equal(320, physicalSize.Width);
                Assert.Equal(180, physicalSize.Height);
            }
            finally
            {
                preview?.Dispose();
            }
        });
    }
}
