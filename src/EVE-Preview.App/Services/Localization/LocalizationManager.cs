namespace EvePreview.App.Services;

/// <summary>
/// Lightweight runtime localization for labels and status text. It preserves the Chinese source
/// string and swaps only exact known UI phrases, so user-provided names and window titles are untouched.
/// </summary>
public static class LocalizationManager
{
    private static string _language = "zh-CN";

    public static event EventHandler? LanguageChanged;

    public static string CurrentLanguage => _language;

    public static bool IsEnglish => _language.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    public static string Translate(string source) =>
        IsEnglish && LocalizationCatalog.English.TryGetValue(source, out var translated)
            ? translated
            : source;

    public static void Initialize() => VisualLocalizer.Initialize();

    public static void SetLanguage(string? language)
    {
        var normalized = language?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true
            ? "en-US"
            : "zh-CN";
        if (string.Equals(_language, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _language = normalized;
        VisualLocalizer.Refresh();
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }
}
