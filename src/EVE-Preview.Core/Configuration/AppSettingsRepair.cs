using System.Text.Json;

namespace EvePreview.Core;

internal sealed record AppSettingsRepairResult(
    AppSettings Settings,
    IReadOnlyList<string> RepairedModules)
{
    public bool WasRepaired => RepairedModules.Count > 0;
}

internal static class AppSettingsRepair
{
    public static AppSettingsRepairResult Repair(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Configuration root must be a JSON object.");
        }

        var settings = new AppSettings();
        var repairedModules = new List<string>();
        RepairSchemaVersion(document.RootElement, settings, repairedModules);
        ApplyModule<GeneralOptions>(document.RootElement, "General", settings, repairedModules,
            (target, value) => target.General = value);
        ApplyModule<MonitoringOptions>(document.RootElement, "Monitoring", settings, repairedModules,
            (target, value) => target.Monitoring = value);
        ApplyModule<GlobalHotKeyOptions>(document.RootElement, "HotKeys", settings, repairedModules,
            (target, value) => target.HotKeys = value);
        ApplyModule<PreviewOptions>(document.RootElement, "Preview", settings, repairedModules,
            (target, value) => target.Preview = value);
        ApplyModule<DisplayOptions>(document.RootElement, "Display", settings, repairedModules,
            (target, value) => target.Display = value);
        ApplyModule<ZoomOptions>(document.RootElement, "Zoom", settings, repairedModules,
            (target, value) => target.Zoom = value);
        ApplyModule<OverlayOptions>(document.RootElement, "Overlay", settings, repairedModules,
            (target, value) => target.Overlay = value);
        ApplyModule<List<CycleGroupSettings>>(document.RootElement, "CycleGroups", settings, repairedModules,
            (target, value) => target.CycleGroups = value);
        ApplyModule<CompatibilityOptions>(document.RootElement, "Compatibility", settings, repairedModules,
            (target, value) => target.Compatibility = value);
        ApplyClientsModule(document.RootElement, settings, repairedModules);

        return new AppSettingsRepairResult(settings, repairedModules);
    }

    private static void RepairSchemaVersion(
        JsonElement root,
        AppSettings settings,
        ICollection<string> repairedModules)
    {
        if (root.TryGetProperty("SchemaVersion", out var element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out var version) &&
            version == 1)
        {
            return;
        }

        settings.SchemaVersion = 1;
        repairedModules.Add("SchemaVersion");
    }

    private static void ApplyModule<T>(
        JsonElement root,
        string name,
        AppSettings settings,
        ICollection<string> repairedModules,
        Action<AppSettings, T> apply)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            repairedModules.Add(name);
            return;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(element.GetRawText());
            if (value is null)
            {
                repairedModules.Add(name);
                return;
            }

            var candidate = settings.DeepCopy();
            apply(candidate, value);
            if (HasInvalidRequiredData(candidate) || !candidate.Validate().IsValid)
            {
                repairedModules.Add(name);
                return;
            }

            candidate.Normalize();
            settings.CopyFrom(candidate);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            repairedModules.Add(name);
        }
    }

    private static void ApplyClientsModule(
        JsonElement root,
        AppSettings settings,
        ICollection<string> repairedModules)
    {
        if (!root.TryGetProperty("Clients", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            repairedModules.Add("Clients");
            return;
        }

        try
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                repairedModules.Add("Clients");
                return;
            }

            var changed = false;
            var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var clients = new List<ClientSettings>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null)
                {
                    changed = true;
                    continue;
                }

                ClientSettings? client;
                try
                {
                    client = JsonSerializer.Deserialize<ClientSettings>(item.GetRawText());
                }
                catch (Exception exception) when (exception is JsonException or NotSupportedException)
                {
                    changed = true;
                    continue;
                }

                if (client is null)
                {
                    changed = true;
                    continue;
                }

                client.Alias ??= string.Empty;
                client.HotKey ??= string.Empty;
                client.HighlightColor ??= string.Empty;
                var originalTitle = client.OriginalTitle;
                var title = originalTitle?.Trim() ?? string.Empty;
                if (title.Length == 0 || !titles.Add(title))
                {
                    changed = true;
                    continue;
                }

                if (!string.Equals(originalTitle, title, StringComparison.Ordinal))
                {
                    changed = true;
                }

                client.OriginalTitle = title;
                if (client.Width is <= 0)
                {
                    client.Width = null;
                    changed = true;
                }

                if (client.Height is <= 0)
                {
                    client.Height = null;
                    changed = true;
                }

                if (client.ClientWindowBounds is { } bounds && (bounds.Width <= 0 || bounds.Height <= 0))
                {
                    client.ClientWindowBounds = null;
                    changed = true;
                }

                if (client.Placement?.Validate() is { IsValid: false })
                {
                    client.Placement = null;
                    changed = true;
                }

                clients.Add(client);
            }

            var candidate = settings.DeepCopy();
            candidate.Clients = clients;
            var hotkeys = clients.Select(client => client.HotKey).ToArray();
            foreach (var client in clients)
            {
                client.HotKey = string.Empty;
            }

            if (candidate.HasNullRequiredSections() || !candidate.Validate().IsValid)
            {
                repairedModules.Add("Clients");
                return;
            }

            for (var index = 0; index < clients.Count; index++)
            {
                var hotkey = hotkeys[index];
                if (string.IsNullOrWhiteSpace(hotkey))
                {
                    continue;
                }

                clients[index].HotKey = hotkey;
                if (!candidate.Validate().IsValid)
                {
                    clients[index].HotKey = string.Empty;
                    changed = true;
                }
            }

            candidate.Normalize();
            if (!candidate.Validate().IsValid)
            {
                repairedModules.Add("Clients");
                return;
            }

            settings.CopyFrom(candidate);
            if (changed)
            {
                repairedModules.Add("Clients");
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            repairedModules.Add("Clients");
        }
    }

    private static bool HasInvalidRequiredData(AppSettings settings) =>
        settings.HasNullRequiredSections() ||
        settings.General.Language is null ||
        settings.CycleGroups.Any(group =>
            group.OrderedClientTitles is null || group.ExcludedTitles is null) ||
        settings.Clients.Any(client => client is null);
}
