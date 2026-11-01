using System.Diagnostics;
using System.IO;
using EvePreview.Core;

namespace EvePreview.App.Services;

/// <summary>
/// Owns the active profile and coordinates file operations that require an application restart.
/// </summary>
public sealed record ProfileRestartRequest(string Path, bool SaveCurrentSettings);

public sealed class ConfigurationProfileService
{
    public ConfigurationProfileService(string currentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);
        CurrentPath = Path.GetFullPath(currentPath);
        DirectoryPath = Path.GetDirectoryName(CurrentPath)
            ?? throw new InvalidOperationException("The configuration path has no parent directory.");
    }

    public string CurrentPath { get; private set; }
    public string DirectoryPath { get; }

    public event EventHandler<ProfileRestartRequest>? RestartRequested;

    public IReadOnlyList<string> ListProfiles() =>
        Directory.Exists(DirectoryPath)
            ? Directory.EnumerateFiles(DirectoryPath, "*.json")
                .Where(path => !path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) &&
                               !Path.GetFileName(path).Contains(".corrupt-", StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFileName(path))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

    public string ResolveProfilePath(string profileName)
    {
        var name = SanitizeProfileName(profileName);
        return Path.IsPathRooted(name) ? name : Path.Combine(DirectoryPath, name);
    }

    public void SaveAs(string profileName, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var path = Path.IsPathRooted(profileName)
            ? EnsureJsonExtension(Path.GetFullPath(profileName))
            : ResolveProfilePath(profileName);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        new JsonSettingsStore(path).Save(settings);
        CurrentPath = path;
    }

    public LegacyImportResult ImportLegacy(string path)
    {
        var json = File.ReadAllText(path);
        return LegacyConfigurationImporter.Import(json);
    }

    public void OpenDirectory()
    {
        System.IO.Directory.CreateDirectory(DirectoryPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{DirectoryPath}\"",
            UseShellExecute = true
        });
    }

    public void RequestRestart(string path, bool saveCurrentSettings = true)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected configuration profile does not exist.", fullPath);
        }

        CurrentPath = fullPath;
        RestartRequested?.Invoke(this, new ProfileRestartRequest(fullPath, saveCurrentSettings));
    }

    private static string SanitizeProfileName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Profile name is required.", nameof(name));
        }

        trimmed = EnsureJsonExtension(trimmed);
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            trimmed.Contains(Path.DirectorySeparatorChar) ||
            trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Profile name contains invalid characters.", nameof(name));
        }

        return trimmed;
    }

    private static string EnsureJsonExtension(string name) =>
        name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : name + ".json";
}
