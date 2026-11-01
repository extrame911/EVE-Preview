using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class LegacyConfigurationImporterTests
{
    [Fact]
    public void ImportsCommonLegacyThumbnailAndClientSettings()
    {
        const string json = """
        {
          "ConfigVersion": 2,
          "Language": "en-US",
          "ThumbnailsOpacity": 0.65,
          "ThumbnailSize": { "Width": 400, "Height": 220 },
          "ThumbnailMinimumSize": { "Width": 200, "Height": 120 },
          "ThumbnailMaximumSize": { "Width": 1200, "Height": 800 },
          "ThumbnailRefreshPeriod": 750,
          "HideThumbnailsOnLostFocus": true,
          "HideThumbnailsDelay": 800,
          "ShowThumbnailsAlwaysOnTop": true,
          "MinimizeToTray": false,
          "MinimizeAllClientsHotkeys": [ "Ctrl+Shift+F10" ],
          "RefreshMinimizedClientsHotkeys": [ "Ctrl+Shift+F11" ],
          "CycleGroup1ForwardHotkeys": [ "Ctrl+1" ],
          "CycleGroup1ClientsOrder": { "EVE - B": 2, "EVE - A": 1 },
          "PerClientAliases": { "EVE - A": "主账号" },
          "DisableThumbnail": { "EVE - B": true },
          "ClientHotkey": { "EVE - A": "Ctrl+Shift+2" },
          "ActiveClientHighlightColor": { "A": 255, "R": 76, "G": 194, "B": 255 },
          "ExecutablesToPreview": [ "C:\\Games\\EVE\\exefile.exe" ],
          "UnknownLegacyField": 1
        }
        """;

        var result = LegacyConfigurationImporter.Import(json);

        Assert.Equal("en-US", result.Settings.General.Language);
        Assert.False(result.Settings.General.MinimizeToTray);
        Assert.Equal(0.65, result.Settings.Display.Opacity);
        Assert.Equal(400, result.Settings.Preview.DefaultWidth);
        Assert.Equal(220, result.Settings.Preview.DefaultHeight);
        Assert.Equal(750, result.Settings.Monitoring.PollIntervalMs);
        Assert.Equal(["exefile"], result.Settings.Monitoring.ProcessNames);
        Assert.Equal("Ctrl+Shift+F10", result.Settings.HotKeys.MinimizeAll);
        Assert.Equal("Ctrl+1", result.Settings.CycleGroups[0].ForwardHotKey);
        Assert.Equal(["EVE - A", "EVE - B"], result.Settings.CycleGroups[0].OrderedClientTitles);
        Assert.Equal("#4CC2FF", result.Settings.Overlay.HighlightColor);
        Assert.Contains(result.Settings.Clients, client => client.OriginalTitle == "EVE - A" &&
                                                          client.Alias == "主账号" &&
                                                          client.HotKey == "Ctrl+Shift+2");
        Assert.Contains(result.Settings.Clients, client => client.OriginalTitle == "EVE - B" &&
                                                          client.DisablePreview);
        Assert.Contains(result.Warnings, warning => warning.Contains("UnknownLegacyField", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMalformedLegacyConfiguration()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            LegacyConfigurationImporter.Import("{ not json"));
    }
}
