using System.Globalization;

namespace EvePreview.Core;

public sealed class MonitoringOptions
{
    public List<string> ProcessNames { get; set; } = ["exefile"];
    public int PollIntervalMs { get; set; } = 500;
}

public sealed class GeneralOptions
{
    public string Language { get; set; } = GetDefaultLanguage(CultureInfo.CurrentUICulture.Name);

    public static string GetDefaultLanguage(string cultureName) =>
        CultureInfo.GetCultureInfo(cultureName).TwoLetterISOLanguageName
            .Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? "zh-CN"
            : "en-US";
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool MinimizeInactiveClients { get; set; }
    public bool AutoRestoreClientWindowOnStartup { get; set; } = true;
    public bool UseWindowAnimations { get; set; } = true;
}

public sealed class GlobalHotKeyOptions
{
    public string MinimizeAll { get; set; } = "Ctrl+Shift+M";
    public string RefreshMinimized { get; set; } = "Ctrl+Shift+R";
    public string ReturnToPrevious { get; set; } = string.Empty;
}
