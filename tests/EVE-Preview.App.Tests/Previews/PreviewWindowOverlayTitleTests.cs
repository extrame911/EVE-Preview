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
