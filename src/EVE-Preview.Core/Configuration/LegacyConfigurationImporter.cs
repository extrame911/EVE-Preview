using System.Globalization;
using System.Text.Json;

namespace EvePreview.Core;

public sealed record LegacyImportResult(
    AppSettings Settings,
    IReadOnlyList<string> Warnings);

public static class LegacyConfigurationImporter
{
    private static readonly HashSet<string> KnownProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "ConfigVersion", "Language", "MinimizeToTray", "IconName", "ThumbnailsOpacity",
        "ThumbnailSize", "ThumbnailMaximumSize", "ThumbnailMinimumSize",
        "ThumbnailRefreshPeriod", "EnableWineCompatibilityMode", "HideActiveClientThumbnail",
        "HideLoginClientThumbnail", "MinimizeInactiveClients", "ShowThumbnailsAlwaysOnTop",
        "PreventPreviews", "HideThumbnailsOnLostFocus", "HideThumbnailsDelay",
        "EnableThumbnailSnap", "EnableThumbnailZoom", "ThumbnailZoomFactor",
        "ThumbnailZoomAnchor", "OverlayLabelAnchor", "CycleGroupIndicatorAnchor",
        "ShowThumbnailOverlays", "ShowThumbnailFrames", "LockThumbnailLocation",
        "ThumbnailSnapToGrid", "ThumbnailSnapToGridSizeX", "ThumbnailSnapToGridSizeY",
        "EnableActiveClientHighlight", "ActiveClientHighlightColor", "PreventPreviewColor",
        "OverlayLabelColor", "OverlayLabelFont", "ActiveClientHighlightThickness",
        "LoginThumbnailLocation", "MinimizeAllClientsHotkeys", "RefreshMinimizedClientsHotkeys",
        "ClientHotkey", "DisableThumbnail", "PriorityClients", "ExecutablesToPreview",
        "PerClientAliases", "PerClientPreventPreviews", "PerClientPreventPreviewColor",
        "PerClientActiveClientHighlightColor", "PerClientThumbnailSize", "PerClientZoomAnchor",
        "PerClientLayout", "FlatLayout", "ClientLayout", "CycleGroupExclusions",
        "WindowsAnimationStyle", "CaptionOnClientsStyle", "EnablePerClientThumbnailLayouts",
        "EnableClientLayoutTracking"
    };

    public static LegacyImportResult Import(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Legacy configuration root must be a JSON object.");
        }

        var settings = new AppSettings();
        var warnings = new List<string>();
        settings.General.Language = NormalizeLanguage(GetString(root, "Language"));
        settings.General.MinimizeToTray = GetBool(root, "MinimizeToTray") ?? true;
        settings.General.MinimizeInactiveClients = GetBool(root, "MinimizeInactiveClients") ?? false;
        settings.Display.Opacity = Math.Clamp(GetDouble(root, "ThumbnailsOpacity") ?? settings.Display.Opacity, 0.2, 1.0);
        settings.Display.HideActiveClient = GetBool(root, "HideActiveClientThumbnail") ?? false;

        settings.Display.HideOnFocusLoss = GetBool(root, "HideThumbnailsOnLostFocus") ?? false;
        settings.Display.HideDelayMs = Math.Clamp(GetInt(root, "HideThumbnailsDelay") ?? 0, 0, 10000);
        settings.Display.Topmost = GetBool(root, "ShowThumbnailsAlwaysOnTop") ?? true;
        settings.Display.LockPosition = GetBool(root, "LockThumbnailLocation") ?? false;
        settings.Preview.SnapMode = GetBool(root, "ThumbnailSnapToGrid") == true
            ? SnapMode.Grid
            : GetBool(root, "EnableThumbnailSnap") == true
                ? SnapMode.Edge
                : SnapMode.None;
        settings.Preview.GridSizeX = Math.Clamp(GetInt(root, "ThumbnailSnapToGridSizeX") ?? 16, 1, 512);
        settings.Preview.GridSizeY = Math.Clamp(GetInt(root, "ThumbnailSnapToGridSizeY") ?? 16, 1, 512);
        settings.Zoom.Enabled = GetBool(root, "EnableThumbnailZoom") ?? false;
        settings.Zoom.Factor = Math.Clamp(GetDouble(root, "ThumbnailZoomFactor") ?? 1.25, 1, 4);
        settings.Zoom.Anchor = MapZoomAnchor(GetInt(root, "ThumbnailZoomAnchor"), ZoomAnchor.Center);
        settings.Overlay.ShowName = GetBool(root, "ShowThumbnailOverlays") ?? true;
        settings.Overlay.HighlightActive = GetBool(root, "EnableActiveClientHighlight") ?? true;
        settings.Overlay.HighlightThickness = Math.Clamp(GetInt(root, "ActiveClientHighlightThickness") ?? 1, 1, 8);
        settings.Overlay.HighlightColor = ReadColor(root, "ActiveClientHighlightColor") ?? settings.Overlay.HighlightColor;
        settings.Overlay.TextColor = ReadColor(root, "OverlayLabelColor") ?? settings.Overlay.TextColor;

        if (TryReadSize(root, "ThumbnailSize", out var size))
        {
            settings.Preview.DefaultWidth = Math.Clamp(size.Width, 1, 16384);
            settings.Preview.DefaultHeight = Math.Clamp(size.Height, 1, 16384);
        }

        if (TryReadSize(root, "ThumbnailMinimumSize", out var minimum))
        {
            settings.Preview.MinWidth = Math.Clamp(minimum.Width, 8, settings.Preview.DefaultWidth);
            settings.Preview.MinHeight = Math.Clamp(minimum.Height, 8, settings.Preview.DefaultHeight);
        }

        if (TryReadSize(root, "ThumbnailMaximumSize", out var maximum))
        {
            settings.Preview.MaxWidth = Math.Max(settings.Preview.DefaultWidth, maximum.Width);
            settings.Preview.MaxHeight = Math.Max(settings.Preview.DefaultHeight, maximum.Height);
        }

        var pollPeriod = GetInt(root, "ThumbnailRefreshPeriod");
        if (pollPeriod is not null)
        {
            settings.Monitoring.PollIntervalMs = Math.Clamp(pollPeriod.Value, 100, 2000);
        }

        if (root.TryGetProperty("ExecutablesToPreview", out var executables) && executables.ValueKind == JsonValueKind.Array)
        {
            settings.Monitoring.ProcessNames = executables.EnumerateArray()
                .Select(item => item.GetString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => ProcessNameNormalizer.Normalize(name!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        ReadHotkeyArray(root, "MinimizeAllClientsHotkeys", value => settings.HotKeys.MinimizeAll = value, warnings);
        ReadHotkeyArray(root, "RefreshMinimizedClientsHotkeys", value => settings.HotKeys.RefreshMinimized = value, warnings);

        var exclusions = ReadBooleanMap(root, "CycleGroupExclusions");
        for (var index = 1; index <= 5; index++)
        {
            var group = settings.CycleGroups[index - 1];
            ReadHotkeyArray(root, $"CycleGroup{index}ForwardHotkeys", value => group.ForwardHotKey = value, warnings);
            ReadHotkeyArray(root, $"CycleGroup{index}BackwardHotkeys", value => group.BackwardHotKey = value, warnings);
            group.OrderedClientTitles = ReadOrderedTitles(root, $"CycleGroup{index}ClientsOrder");
            if (exclusions is not null)
            {
                group.ExcludedTitles = exclusions.Where(pair => pair.Value).Select(pair => pair.Key).ToList();
            }
        }

        var aliases = ReadStringMap(root, "PerClientAliases");
        var disabled = ReadBooleanMap(root, "DisableThumbnail") ?? ReadBooleanMap(root, "PerClientPreventPreviews");
        var clientHotkeys = ReadStringMap(root, "ClientHotkey");
        var clientHighlights = ReadColorMap(root, "PerClientActiveClientHighlightColor");
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (aliases is not null)
        {
            titles.UnionWith(aliases.Keys);
        }

        if (disabled is not null)
        {
            titles.UnionWith(disabled.Keys);
        }

        if (clientHotkeys is not null)
        {
            titles.UnionWith(clientHotkeys.Keys);
        }

        if (clientHighlights is not null)
        {
            titles.UnionWith(clientHighlights.Keys);
        }

        foreach (var title in titles.OrderBy(title => title, StringComparer.OrdinalIgnoreCase))
        {
            var client = new ClientSettings { OriginalTitle = title };
            if (aliases?.TryGetValue(title, out var alias) == true)
            {
                client.Alias = alias;
            }

            if (disabled?.TryGetValue(title, out var isDisabled) == true)
            {
                client.DisablePreview = isDisabled;
            }

            if (clientHotkeys?.TryGetValue(title, out var hotkey) == true)
            {
                client.HotKey = hotkey;
            }

            if (clientHighlights?.TryGetValue(title, out var highlight) == true)
            {
                client.HighlightColor = highlight;
            }

            settings.Clients.Add(client);
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!KnownProperties.Contains(property.Name) &&
                !property.Name.StartsWith("CycleGroup", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"未映射旧版字段：{property.Name}");
            }
        }

        settings.Normalize();
        var validation = settings.Validate();
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        return new LegacyImportResult(settings, warnings);
    }

    private static void ReadHotkeyArray(
        JsonElement root,
        string propertyName,
        Action<string> assign,
        ICollection<string> warnings)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var first = value.EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .FirstOrDefault();
        if (first is null)
        {
            return;
        }

        if (HotKeyParser.TryNormalize(first, out var normalized))
        {
            assign(normalized);
        }
        else
        {
            warnings.Add($"无法映射热键 '{first}'（{propertyName}）。");
        }
    }

    private static List<string> ReadOrderedTitles(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return value.EnumerateObject()
            .Select(property => (Title: property.Name, Order: property.Value.ValueKind == JsonValueKind.Number
                ? property.Value.GetInt32()
                : int.MaxValue))
            .OrderBy(pair => pair.Order)
            .ThenBy(pair => pair.Title, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Title)
            .ToList();
    }

    private static bool TryReadSize(JsonElement root, string propertyName, out (int Width, int Height) size)
    {
        size = default;
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var width = GetInt(value, "Width");
        var height = GetInt(value, "Height");
        if (width is null || height is null)
        {
            return false;
        }

        size = (width.Value, height.Value);
        return true;
    }

    private static string? ReadColor(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var a = GetInt(value, "A") ?? 255;
        var r = GetInt(value, "R");
        var g = GetInt(value, "G");
        var b = GetInt(value, "B");
        return r is null || g is null || b is null
            ? null
            : a == 255
                ? $"#{r:X2}{g:X2}{b:X2}"
                : $"#{a:X2}{r:X2}{g:X2}{b:X2}";
    }

    private static Dictionary<string, string>? ReadStringMap(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return value.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty);
    }

    private static Dictionary<string, bool>? ReadBooleanMap(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return value.EnumerateObject()
            .Where(property => property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            .ToDictionary(property => property.Name, property => property.Value.GetBoolean());
    }

    private static Dictionary<string, string>? ReadColorMap(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            var color = ReadColor(value, property.Name);
            if (color is not null)
            {
                result[property.Name] = color;
            }
        }

        return result;
    }

    private static string NormalizeLanguage(string? value) =>
        value?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? "en-US" : "zh-CN";

    private static ZoomAnchor MapZoomAnchor(int? value, ZoomAnchor fallback) =>
        value is >= 0 and <= 8 ? (ZoomAnchor)value.Value : fallback;

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? GetBool(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static int? GetInt(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;

    private static double? GetDouble(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number)
            ? number
            : null;
}
