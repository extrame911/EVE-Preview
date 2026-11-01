using System.Windows.Media;

namespace EvePreview.App.Services;

public static class AppTypography
{
    public const string FamilySource = "Bahnschrift SemiCondensed, Microsoft YaHei UI";
    public const string BoldFamilySource = "Bahnschrift, Microsoft YaHei UI";

    public static FontFamily Family { get; } = new(FamilySource);
    public static FontFamily BoldFamily { get; } = new(BoldFamilySource);
}
