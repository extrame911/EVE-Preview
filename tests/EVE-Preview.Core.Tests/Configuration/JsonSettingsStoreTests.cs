using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public void FirstLaunchCreatesDefaultConfigurationWithoutRecoveryDiagnostic()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            var store = new JsonSettingsStore(path);

            var result = store.Load();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.Equal(500, result.Settings.Monitoring.PollIntervalMs);
            Assert.Null(result.Diagnostic);
            Assert.Null(result.BackupPath);
            Assert.True(File.Exists(path));
            var saved = new JsonSettingsStore(path).Load();
            Assert.Equal(SettingsLoadStatus.Loaded, saved.Status);
            Assert.Equal(500, saved.Settings.Monitoring.PollIntervalMs);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoadRoundTripsNormalizedSettings()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            var store = new JsonSettingsStore(path);
            var settings = new AppSettings();
            settings.Monitoring.ProcessNames = [" ExeFile.exe "];
            settings.HotKeys.MinimizeAll = "shift+ctrl+f4";

            store.Save(settings);
            var result = store.Load();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.Equal(["exefile"], result.Settings.Monitoring.ProcessNames);
            Assert.Equal("Ctrl+Shift+F4", result.Settings.HotKeys.MinimizeAll);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CorruptConfigurationIsBackedUpAndAutomaticallyReplacedWithDefaults()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            File.WriteAllText(path, "{ invalid json");
            var store = new JsonSettingsStore(path);

            var result = store.Load();

            Assert.Equal(SettingsLoadStatus.RecoveredCorrupt, result.Status);
            Assert.False(result.RequiresConfirmation);
            Assert.NotNull(result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));
            Assert.Equal("{ invalid json", File.ReadAllText(result.BackupPath!));
            Assert.NotEqual("{ invalid json", File.ReadAllText(path));
            Assert.Equal(500, result.Settings.Monitoring.PollIntervalMs);
            Assert.Equal(SettingsLoadStatus.Loaded, store.Load().Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveRetainsPreviousConfigurationAsBackupAndLeavesNoTempFile()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            var store = new JsonSettingsStore(path);
            store.Save(new AppSettings());
            var settings = new AppSettings();
            settings.Display.Opacity = 0.75;

            store.Save(settings);

            Assert.True(File.Exists(path + ".bak"));
            Assert.Contains("\"Opacity\": 0.75", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveReportsIoFailureWithoutReplacingTheExistingFileOrLeavingTempData()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            var store = new JsonSettingsStore(path);
            store.Save(new AppSettings());
            var original = File.ReadAllText(path);

            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var exception = Assert.Throws<SettingsPersistenceException>(() => store.Save(new AppSettings()));
                Assert.NotNull(exception.InnerException);
            }

            Assert.Equal(original, File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Corrupt_client_entries_are_dropped_or_downgraded_without_losing_other_clients()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            const string json = """
                {
                  "SchemaVersion": 1,
                  "Clients": [
                    {
                      "OriginalTitle": "EVE - Alpha",
                      "Alias": "Keep Alpha",
                      "Placement": { "Left": 10, "Top": 20, "Width": 0, "Height": 0 }
                    },
                    {
                      "OriginalTitle": "EVE - Beta",
                      "Alias": "Keep Beta",
                      "Width": -1,
                      "Height": 0,
                      "HotKey": "invalid hotkey",
                      "ClientWindowBounds": { "X": 0, "Y": 0, "Width": 0, "Height": 0 }
                    },
                    { "OriginalTitle": "eve - alpha", "Alias": "Duplicate" },
                    { "OriginalTitle": "EVE - Broken", "Width": "auto" },
                    { "OriginalTitle": "   ", "Alias": "Blank" },
                    null
                  ]
                }
                """;
            File.WriteAllText(path, json);
            var store = new JsonSettingsStore(path);

            var result = store.Load();

            Assert.Equal(SettingsLoadStatus.RecoveredCorrupt, result.Status);
            Assert.Equal(2, result.Settings.Clients.Count);
            var alpha = Assert.Single(result.Settings.Clients, client => client.OriginalTitle == "EVE - Alpha");
            Assert.Equal("Keep Alpha", alpha.Alias);
            Assert.Null(alpha.Placement);
            var beta = Assert.Single(result.Settings.Clients, client => client.OriginalTitle == "EVE - Beta");
            Assert.Equal("Keep Beta", beta.Alias);
            Assert.Null(beta.Width);
            Assert.Null(beta.Height);
            Assert.Null(beta.ClientWindowBounds);
            Assert.Equal(string.Empty, beta.HotKey);
            Assert.Equal(SettingsLoadStatus.Loaded, store.Load().Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void InvalidModulesUseDefaultsWhileValidModulesSurviveAndTheWholeFileIsRewritten()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            const string json = """
                {
                  "SchemaVersion": 1,
                  "General": { "Language": "en-US" },
                  "Monitoring": { "ProcessNames": ["exefile"], "PollIntervalMs": 800 },
                  "Preview": { "DefaultWidth": 4000 },
                  "Display": { "Opacity": 0.65 },
                  "Clients": [{ "OriginalTitle": "EVE - Alpha", "Alias": "Main" }]
                }
                """;
            File.WriteAllText(path, json);
            var store = new JsonSettingsStore(path);

            var result = store.Load();

            Assert.Equal(SettingsLoadStatus.RecoveredCorrupt, result.Status);
            Assert.False(result.RequiresConfirmation);
            Assert.Contains("Preview", result.Diagnostic);
            Assert.Equal(320, result.Settings.Preview.DefaultWidth);
            Assert.Equal("en-US", result.Settings.General.Language);
            Assert.Equal(800, result.Settings.Monitoring.PollIntervalMs);
            Assert.Equal(0.65, result.Settings.Display.Opacity);
            Assert.Equal("Main", Assert.Single(result.Settings.Clients).Alias);
            Assert.Equal(json, File.ReadAllText(result.BackupPath!));

            var reloaded = store.Load();
            Assert.Equal(SettingsLoadStatus.Loaded, reloaded.Status);
            Assert.Equal(320, reloaded.Settings.Preview.DefaultWidth);
            Assert.Equal(800, reloaded.Settings.Monitoring.PollIntervalMs);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("{\"Preview\":null}")]
    [InlineData("{\"Monitoring\":{\"ProcessNames\":null}}")]
    [InlineData("null")]
    [InlineData("")]
    public void StructurallyInvalidJsonIsBackedUpAndAutomaticallyRepaired(string json)
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            File.WriteAllText(path, json);
            var store = new JsonSettingsStore(path);

            var result = store.Load();

            Assert.Equal(SettingsLoadStatus.RecoveredCorrupt, result.Status);
            Assert.False(result.RequiresConfirmation);
            Assert.NotNull(result.BackupPath);
            Assert.Equal(json, File.ReadAllText(result.BackupPath!));
            Assert.Equal(SettingsLoadStatus.Loaded, store.Load().Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ConcurrentSavesLeaveOneCompleteValidDocument()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            var store = new JsonSettingsStore(path);

            Parallel.For(0, 12, index =>
            {
                var settings = new AppSettings();
                settings.Display.Opacity = 0.3 + index / 20d;
                store.Save(settings);
            });

            var loaded = store.Load();
            Assert.Equal(SettingsLoadStatus.Loaded, loaded.Status);
            Assert.True(loaded.Settings.Display.Opacity is >= 0.2 and <= 1.0);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadPreservesDynamicCycleGroupCountAndOtherSettings()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            var settings = new AppSettings();
            settings.Display.Opacity = 0.7;
            settings.CycleGroups.RemoveRange(2, 3);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(settings));
            var store = new JsonSettingsStore(path);

            var result = store.Load();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.Equal(2, result.Settings.CycleGroups.Count);
            Assert.Equal(0.7, result.Settings.Display.Opacity);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eve-preview-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
