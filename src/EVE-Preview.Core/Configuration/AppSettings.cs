namespace EvePreview.Core;


public sealed class AppSettings
{
    public const int MinimumCycleGroups = 1;

    public int SchemaVersion { get; set; } = 1;
    public GeneralOptions General { get; set; } = new();
    public MonitoringOptions Monitoring { get; set; } = new();
    public GlobalHotKeyOptions HotKeys { get; set; } = new();
    public PreviewOptions Preview { get; set; } = new();
    public DisplayOptions Display { get; set; } = new();
    public ZoomOptions Zoom { get; set; } = new();
    public OverlayOptions Overlay { get; set; } = new();
    public List<CycleGroupSettings> CycleGroups { get; set; } = CreateCycleGroups();
    public CompatibilityOptions Compatibility { get; set; } = new();
    public List<ClientSettings> Clients { get; set; } = [];

    public void Normalize()
    {
        General ??= new GeneralOptions();
        Monitoring ??= new MonitoringOptions();
        HotKeys ??= new GlobalHotKeyOptions();
        Preview ??= new PreviewOptions();
        Display ??= new DisplayOptions();
        Zoom ??= new ZoomOptions();
        Overlay ??= new OverlayOptions();
        Compatibility ??= new CompatibilityOptions();
        CycleGroups ??= CreateCycleGroups();
        Clients ??= [];
        Monitoring.ProcessNames ??= ["exefile"];
        CycleGroups = CycleGroups.Where(group => group is not null).ToList();
        Clients = Clients.Where(client => client is not null).ToList();
        if (CycleGroups.Count < MinimumCycleGroups)
        {
            CycleGroups.Add(new CycleGroupSettings());
        }
        foreach (var group in CycleGroups)
        {
            group.OrderedClientTitles ??= [];
            group.ExcludedTitles ??= [];
            group.OrderedClientTitles = group.OrderedClientTitles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            group.ExcludedTitles = group.ExcludedTitles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        Monitoring.ProcessNames = Monitoring.ProcessNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(ProcessNameNormalizer.Normalize)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        HotKeys.MinimizeAll = NormalizeHotKey(HotKeys.MinimizeAll);
        HotKeys.RefreshMinimized = NormalizeHotKey(HotKeys.RefreshMinimized);
        HotKeys.ReturnToPrevious = NormalizeHotKey(HotKeys.ReturnToPrevious);
        foreach (var group in CycleGroups)
        {
            group.ForwardHotKey = NormalizeHotKey(group.ForwardHotKey);
            group.BackwardHotKey = NormalizeHotKey(group.BackwardHotKey);
        }

        foreach (var client in Clients)
        {
            client.HotKey = NormalizeHotKey(client.HotKey);
        }
    }

    public bool HasNullRequiredSections() =>
        General is null ||
        Monitoring is null ||
        Monitoring.ProcessNames is null ||
        HotKeys is null ||
        Preview is null ||
        Display is null ||
        Zoom is null ||
        Overlay is null ||
        Compatibility is null ||
        CycleGroups is null ||
        CycleGroups.Any(group => group is null) ||
        Clients is null;

    private static string NormalizeHotKey(string value)
    {
        if (!HotKeyParser.TryParse(value, out var hotkey))
        {
            return value.Trim();
        }

        return hotkey.MouseButton == HotKeyMouseButton.None ? hotkey.Canonical : string.Empty;
    }

    public AppSettings DeepCopy() =>
        System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
            System.Text.Json.JsonSerializer.Serialize(this))!;

    public void CopyFrom(AppSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = source.DeepCopy();
        SchemaVersion = copy.SchemaVersion;
        General = copy.General;
        Monitoring = copy.Monitoring;
        HotKeys = copy.HotKeys;
        Preview = copy.Preview;
        Display = copy.Display;
        Zoom = copy.Zoom;
        Overlay = copy.Overlay;
        CycleGroups = copy.CycleGroups;
        Compatibility = copy.Compatibility;
        Clients = copy.Clients;
    }

    public ValidationResult Validate()
    {
        var errors = new List<string>();
        if (SchemaVersion != 1)
        {
            errors.Add("SchemaVersion must be 1.");
        }

        if (CycleGroups.Count < MinimumCycleGroups)
        {
            errors.Add("CycleGroups must contain at least one group.");
        }

        if (Preview.MinWidth <= 0 || Preview.MinWidth > Preview.DefaultWidth ||
            Preview.DefaultWidth > Preview.MaxWidth ||
            Preview.MinHeight <= 0 || Preview.MinHeight > Preview.DefaultHeight ||
            Preview.DefaultHeight > Preview.MaxHeight)
        {
            errors.Add("Preview sizes must satisfy 0 < minimum <= default <= maximum.");
        }

        if (Monitoring.PollIntervalMs is < 100 or > 2000)
        {
            errors.Add("Monitoring.PollIntervalMs must be between 100 and 2000.");
        }

        if (Preview.GridSizeX <= 0 || Preview.GridSizeY <= 0)
        {
            errors.Add("Preview grid sizes must be greater than zero.");
        }

        if (!Enum.IsDefined(Preview.SnapMode))
        {
            errors.Add("Preview.SnapMode is not a supported value.");
        }

        if (!Enum.IsDefined(Compatibility.RenderingMode))
        {
            errors.Add("Compatibility.RenderingMode is not a supported value.");
        }

        if (!Enum.IsDefined(Zoom.Anchor))
        {
            errors.Add("Zoom.Anchor is not a supported value.");
        }

        if (!Enum.IsDefined(Overlay.Position))
        {
            errors.Add("Overlay.Position is not a supported value.");
        }

        if (Overlay.FontSize is < 8 or > 72)
        {
            errors.Add("Overlay.FontSize must be between 8 and 72.");
        }

        if (Display.ExpansionThreshold < 0)
        {
            errors.Add("Display.ExpansionThreshold must not be negative.");
        }

        if (Display.Opacity is < 0.2 or > 1.0)
        {
            errors.Add("Display.Opacity must be between 0.2 and 1.0.");
        }

        if (Zoom.Factor is < 1.0 or > 4.0)
        {
            errors.Add("Zoom.Factor must be between 1.0 and 4.0.");
        }

        if (Zoom.DelayMs is < 0 or > 3000)
        {
            errors.Add("Zoom.DelayMs must be between 0 and 3000.");
        }

        if (Display.HideDelayMs is < 0 or > 10000)
        {
            errors.Add("Display.HideDelayMs must be between 0 and 10000.");
        }

        if (Display.HideActiveDelayMs is < 0 or > 10000)
        {
            errors.Add("Display.HideActiveDelayMs must be between 0 and 10000.");
        }

        if (Overlay.HighlightThickness is < 1 or > 8)
        {
            errors.Add("Overlay.HighlightThickness must be between 1 and 8.");
        }

        if (Compatibility.GdiFramesPerSecond is < 1 or > 30)
        {
            errors.Add("Compatibility.GdiFramesPerSecond must be between 1 and 30.");
        }

        if (Compatibility.NativeCallTimeoutMs is < 100 or > 5000)
        {
            errors.Add("Compatibility.NativeCallTimeoutMs must be between 100 and 5000.");
        }

        ValidateHotKeys(errors);
        ValidateClients(errors);
        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private void ValidateClients(List<string> errors)
    {
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < Clients.Count; index++)
        {
            var client = Clients[index];
            if (string.IsNullOrWhiteSpace(client.OriginalTitle))
            {
                errors.Add($"Clients[{index}].OriginalTitle is required.");
            }
            else if (!titles.Add(client.OriginalTitle.Trim()))
            {
                errors.Add($"Clients[{index}].OriginalTitle duplicates another client.");
            }

            if (client.Width is <= 0 || client.Height is <= 0)
            {
                errors.Add($"Clients[{index}] dimensions must be greater than zero when specified.");
            }

            if (client.ClientWindowBounds is { } bounds &&
                (bounds.Width <= 0 || bounds.Height <= 0))
            {
                errors.Add($"Clients[{index}].ClientWindowBounds must have a positive size.");
            }

            if (client.Placement?.Validate() is { IsValid: false } placementValidation)
            {
                errors.Add($"Clients[{index}].Placement is invalid: {string.Join(" ", placementValidation.Errors)}");
            }
        }
    }

    private void ValidateHotKeys(List<string> errors)
    {
        var bindings = new List<(string Name, string Value)>
        {
            ("HotKeys.MinimizeAll", HotKeys.MinimizeAll),
            ("HotKeys.RefreshMinimized", HotKeys.RefreshMinimized),
            ("HotKeys.ReturnToPrevious", HotKeys.ReturnToPrevious)
        };
        for (var index = 0; index < CycleGroups.Count; index++)
        {
            bindings.Add(($"CycleGroups[{index}].ForwardHotKey", CycleGroups[index].ForwardHotKey));
            bindings.Add(($"CycleGroups[{index}].BackwardHotKey", CycleGroups[index].BackwardHotKey));
        }

        bindings.AddRange(Clients.Select((client, index) => ($"Clients[{index}].HotKey", client.HotKey)));
        var assigned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in bindings)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!HotKeyParser.TryParse(value, out var hotkey))
            {
                errors.Add($"{name} is not a valid hot key.");
                continue;
            }

            if (hotkey.MouseButton != HotKeyMouseButton.None)
            {
                errors.Add($"{name} must not use a mouse button.");
                continue;
            }

            if (hotkey.Modifiers == KeyModifiers.None)
            {
                errors.Add($"{name} must include at least one modifier key.");
                continue;
            }

            var normalized = hotkey.Canonical;
            if (!assigned.TryAdd(normalized, name))
            {
                errors.Add($"Hot key '{normalized}' is assigned to both {assigned[normalized]} and {name}.");
            }
        }
    }

    private static List<CycleGroupSettings> CreateCycleGroups() =>
        Enumerable.Range(0, 5).Select(_ => new CycleGroupSettings()).ToList();
}

