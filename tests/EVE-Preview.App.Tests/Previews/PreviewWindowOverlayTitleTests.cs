using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EvePreview.App.Services;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class PreviewWindowOverlayTitleTests(WpfApplicationFixture wpf)
{
    private const string ClientTitle = "EVE - Pilot One";

    [Fact]
    public void Overlay_uses_alias_before_character_name()
    {
        wpf.Invoke(() =>
        {
            using var preview = CreatePreview(displayName: "Main", alias: "Main");
            Assert.Equal("Main", GetOverlayTitle(preview));
        });
    }

    [Fact]
    public void Overlay_uses_character_name_when_enabled_and_no_alias_exists()
    {
        wpf.Invoke(() =>
        {
            var settings = new AppSettings();
            settings.Overlay.ShowCharacterNameOnly = true;
            using var preview = CreatePreview(displayName: ClientTitle, settings: settings);
            Assert.Equal("Pilot One", GetOverlayTitle(preview));
        });
    }

    [Fact]
    public void Overlay_uses_full_client_title_when_character_only_is_disabled()
    {
        wpf.Invoke(() =>
        {
            var settings = new AppSettings();
            settings.Overlay.ShowCharacterNameOnly = false;
            using var preview = CreatePreview(displayName: ClientTitle, settings: settings);
            Assert.Equal(ClientTitle, GetOverlayTitle(preview));
        });
    }

    [Fact]
    public void Overlay_name_text_is_bold()
    {
        wpf.Invoke(() =>
        {
            using var preview = CreatePreview(displayName: "Main", alias: "Main");
            preview.Left = -10000;
            preview.Top = -10000;
            preview.Show();
            var overlay = new PreviewOverlayWindow(preview);
            var title = (TextBlock)typeof(PreviewOverlayWindow)
                .GetField("_title", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(overlay)!;
            Assert.Equal(FontWeights.Bold, title.FontWeight);
            Assert.Equal(AppTypography.BoldFamilySource, title.FontFamily.Source);
            Assert.Equal(FontStretches.SemiCondensed, title.FontStretch);
            var typeface = new Typeface(title.FontFamily, title.FontStyle, title.FontWeight, title.FontStretch);
            Assert.True(typeface.TryGetGlyphTypeface(out var glyph));
            Assert.Equal(700, glyph.Weight.ToOpenTypeWeight());
            Assert.Equal(4, glyph.Stretch.ToOpenTypeStretch());
        });
    }

    [Fact]
    public void Overlay_bounds_match_the_preview_window_in_physical_pixels()
    {
        wpf.Invoke(() =>
        {
            using var preview = CreatePreview(displayName: "Main", alias: "Main");
            preview.Left = 100;
            preview.Top = 100;
            preview.Show();
            var overlay = new PreviewOverlayWindow(preview);
            try
            {
                overlay.Show();
                overlay.Sync(preview);

                var ownerOrigin = preview.PointToScreen(new Point(0, 0));
                var ownerEnd = preview.PointToScreen(new Point(preview.ActualWidth, preview.ActualHeight));
                var overlayOrigin = overlay.PointToScreen(new Point(0, 0));
                var overlayEnd = overlay.PointToScreen(new Point(overlay.ActualWidth, overlay.ActualHeight));

                Assert.Equal(ownerOrigin.X, overlayOrigin.X, 1);
                Assert.Equal(ownerOrigin.Y, overlayOrigin.Y, 1);
                Assert.Equal(ownerEnd.X, overlayEnd.X, 1);
                Assert.Equal(ownerEnd.Y, overlayEnd.Y, 1);
            }
            finally
            {
                overlay.Close();
            }
        });
    }

    [Fact]
    public void Overlay_title_keeps_its_physical_size_across_monitor_dpi()
    {
        wpf.Invoke(() =>
        {
            var settings = new AppSettings();
            settings.Overlay.FontSize = 14;
            using var preview = CreatePreview(displayName: "Main", alias: "Main");
            preview.Left = -10000;
            preview.Top = -10000;
            preview.Show();
            var overlay = new PreviewOverlayWindow(preview);
            try
            {
                overlay.UpdateAppearance(settings, false, 255, true);
                typeof(PreviewOverlayWindow)
                    .GetMethod(
                        "UpdateDpiCompensation",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        binder: null,
                        types: new[] { typeof(double) },
                        modifiers: null)!
                    .Invoke(overlay, new object[] { 1.5 });

                var title = (TextBlock)typeof(PreviewOverlayWindow)
                    .GetField("_title", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(overlay)!;
                var expected = 14 * PreviewMonitorDpi.GetPrimaryScale().X / 1.5;
                Assert.Equal(expected, title.FontSize, 6);
                Assert.Equal(14 * PreviewMonitorDpi.GetPrimaryScale().X, title.FontSize * 1.5, 6);
            }
            finally
            {
                overlay.Close();
            }
        });
    }

    [Fact]
    public void Overlay_size_is_locked_while_the_owner_is_dragging()
    {
        wpf.Invoke(() =>
        {
            using var preview = CreatePreview(displayName: "Main", alias: "Main");
            preview.Left = -10000;
            preview.Top = -10000;
            preview.Show();
            var overlay = new PreviewOverlayWindow(preview);
            try
            {
                overlay.Show();
                overlay.Sync(preview);
                typeof(PreviewWindow)
                    .GetField("_rightDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(preview, true);
                var expected = TestWindowGeometry.GetPhysicalSize(overlay);

                overlay.Width = 999;
                overlay.Height = 777;

                Assert.Equal(expected, TestWindowGeometry.GetPhysicalSize(overlay));
            }
            finally
            {
                overlay.Close();
            }
        });
    }

    private static PreviewWindow CreatePreview(
        string displayName,
        string? alias = null,
        AppSettings? settings = null) =>
        new(
            new ProcessSnapshot(1, "exefile", 101, ClientTitle, DateTime.Now),
            alias ?? displayName,
            settings ?? new AppSettings(),
            new PreviewPlacement(10, 20, 320, 180),
            (_, _) => { },
            (_, _) => { });

    private static string GetOverlayTitle(PreviewWindow preview) =>
        (string)typeof(PreviewWindow)
            .GetMethod("GetOverlayTitle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(preview, null)!;
}
