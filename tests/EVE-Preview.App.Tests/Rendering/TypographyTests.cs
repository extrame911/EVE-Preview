using System.Globalization;
using System.Windows;
using System.Windows.Media;
using EvePreview.App.Services;

namespace EvePreview.App.Tests;

public sealed class TypographyTests
{
    private const string LatinFamilyName = "Bahnschrift SemiCondensed";

    [Fact]
    public void Application_family_uses_Bahnschrift_SemiCondensed_with_YaHei_fallback()
    {
        Assert.Equal("Bahnschrift SemiCondensed, Microsoft YaHei UI", AppTypography.FamilySource);
        Assert.Equal(AppTypography.FamilySource, AppTypography.Family.Source);
    }

    [Theory]
    [InlineData(400, 400)]
    [InlineData(500, 400)]
    public void Bahnschrift_resolves_weight_and_expected_glyph_coverage(int weight, int expectedWeight)
    {
        var typeface = new Typeface(
            new FontFamily(LatinFamilyName),
            FontStyles.Normal,
            FontWeight.FromOpenTypeWeight(weight),
            FontStretches.Normal);

        Assert.True(typeface.TryGetGlyphTypeface(out var glyph));
        var resolved = $"{glyph.FontUri} | {string.Join(",", glyph.FamilyNames.Values)}";
        Assert.True(glyph.Weight.ToOpenTypeWeight() == expectedWeight, $"Requested {weight}, resolved {glyph.Weight.ToOpenTypeWeight()} | {resolved}");
        Assert.True(glyph.CharacterToGlyphMap.ContainsKey('A'), resolved);
        Assert.True(glyph.CharacterToGlyphMap.ContainsKey('8'), resolved);
        Assert.False(glyph.CharacterToGlyphMap.ContainsKey('中'), resolved);
    }

    [Fact]
    public void Composite_family_uses_Bahnschrift_for_Latin_and_YaHei_for_Chinese()
    {
        var bahnschrift = new Typeface(
            new FontFamily(LatinFamilyName),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
        var yaHei = new Typeface(
            new FontFamily("Microsoft YaHei UI"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
        var composite = new Typeface(
            AppTypography.Family,
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

        Assert.Equal(Measure(bahnschrift, "EVE 0123456789"), Measure(composite, "EVE 0123456789"), 6);
        Assert.Equal(Measure(yaHei, "循环组设置"), Measure(composite, "循环组设置"), 6);

        static double Measure(Typeface typeface, string text) =>
            new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                16,
                Brushes.Black,
                1).WidthIncludingTrailingWhitespace;
    }
}
