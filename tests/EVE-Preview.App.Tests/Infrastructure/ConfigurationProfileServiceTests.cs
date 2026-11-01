using System.IO;
using EvePreview.App.Services;
using EvePreview.Core;

namespace EvePreview.App.Tests;

public sealed class ConfigurationProfileServiceTests
{
    [Fact]
    public void ListsProfilesAndSavesNormalizedProfileNames()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"eve-profile-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var current = Path.Combine(directory, "EVE-Preview.json");
            new JsonSettingsStore(current).Save(new AppSettings());
            new JsonSettingsStore(Path.Combine(directory, "Alt.json")).Save(new AppSettings());
            File.WriteAllText(Path.Combine(directory, "Broken.json.bak"), "{}");

            var service = new ConfigurationProfileService(current);
            var setting = new AppSettings();
            setting.Display.Opacity = 0.6;
            service.SaveAs("Raid", setting);

            Assert.Contains("EVE-Preview.json", service.ListProfiles());
            Assert.Contains("Alt.json", service.ListProfiles());
            Assert.Contains("Raid.json", service.ListProfiles());
            Assert.DoesNotContain("Broken.json.bak", service.ListProfiles());
            Assert.Equal(0.6, new JsonSettingsStore(service.CurrentPath).Load().Settings.Display.Opacity);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RestartRequestCarriesProfileAndSavePolicy()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"eve-profile-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "EVE-Preview.json");
            new JsonSettingsStore(path).Save(new AppSettings());
            var service = new ConfigurationProfileService(path);
            ProfileRestartRequest? request = null;
            service.RestartRequested += (_, value) => request = value;

            service.RequestRestart(path, saveCurrentSettings: false);

            Assert.NotNull(request);
            Assert.Equal(path, request.Path);
            Assert.False(request.SaveCurrentSettings);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
