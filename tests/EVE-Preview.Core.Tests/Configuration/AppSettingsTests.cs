using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void DefaultsMatchCoreDesignAndCreateFiveCycleGroups()
    {
        var settings = new AppSettings();

        Assert.Equal(1, settings.SchemaVersion);
        Assert.Equal(["exefile"], settings.Monitoring.ProcessNames);
        Assert.Equal(500, settings.Monitoring.PollIntervalMs);
        Assert.Equal(320, settings.Preview.DefaultWidth);
        Assert.Equal(180, settings.Preview.DefaultHeight);
        Assert.Equal(8, settings.Preview.MinWidth);
        Assert.Equal(8, settings.Preview.MinHeight);
        Assert.Equal(32, settings.Preview.GridSizeX);
        Assert.Equal(SnapMode.Grid, settings.Preview.SnapMode);
        Assert.Equal(0.8, settings.Display.Opacity);
        Assert.True(settings.Display.HideWhenOutOfBounds);
        Assert.Equal(128, settings.Display.ExpansionThreshold);
        Assert.Equal(1, settings.Overlay.HighlightThickness);
        Assert.True(settings.Overlay.ShowCharacterNameOnly);
        Assert.Equal(14, settings.Overlay.FontSize);
        Assert.Equal("Bahnschrift SemiCondensed, Microsoft YaHei UI", settings.Overlay.FontFamily);
        Assert.Equal("#FF8040", settings.Overlay.TextColor);
        Assert.Equal(OverlayPosition.TopCenter, settings.Overlay.Position);
        Assert.Equal("#00FFFF", settings.Overlay.HighlightColor);
        Assert.Equal(100, settings.Zoom.DelayMs);
        Assert.Equal(2, settings.Zoom.Factor);
        Assert.Equal(ZoomAnchor.TopRight, settings.Zoom.Anchor);
        Assert.True(settings.Overlay.ShowName);
        Assert.Equal(RenderingMode.Dwm, settings.Compatibility.RenderingMode);
        Assert.Equal(1, settings.Compatibility.GdiFramesPerSecond);
        Assert.Equal(5, settings.CycleGroups.Count);
    }

    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]
    [InlineData("zh-HK", "zh-CN")]
    [InlineData("en-US", "en-US")]
    [InlineData("ja-JP", "en-US")]
    [InlineData("fr-FR", "en-US")]
    public void Default_language_follows_system_culture(string cultureName, string expected)
    {
        Assert.Equal(expected, GeneralOptions.GetDefaultLanguage(cultureName));
    }

    [Fact]
    public void NormalizeCanonicalizesAndDeduplicatesProcessNames()
    {
        var settings = new AppSettings();
        settings.Monitoring.ProcessNames = [" ExeFile.EXE ", @"C:\\EVE\\exefile.exe", " ", "Other"];

        settings.Normalize();

        Assert.Equal(["exefile", "other"], settings.Monitoring.ProcessNames);
    }

    [Fact]
    public void DeepCopyDetachesNestedOptionsAndLists()
    {
        var original = new AppSettings();
        original.CycleGroups[0].OrderedClientTitles.Add("EVE - A");
        var copy = original.DeepCopy();

        copy.Monitoring.ProcessNames[0] = "other";
        copy.Display.Opacity = 0.5;
        copy.CycleGroups[0].OrderedClientTitles.Add("EVE - B");
        copy.Clients.Add(new ClientSettings { OriginalTitle = "EVE - C" });

        Assert.Equal(["exefile"], original.Monitoring.ProcessNames);
        Assert.Equal(0.8, original.Display.Opacity);
        Assert.Equal(["EVE - A"], original.CycleGroups[0].OrderedClientTitles);
        Assert.Empty(original.Clients);
    }

    [Fact]
    public void ValidationRejectsInvertedPreviewSizeRange()
    {
        var settings = new AppSettings();
        settings.Preview.MinWidth = 800;
        settings.Preview.DefaultWidth = 320;

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Preview", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("poll")]
    [InlineData("grid")]
    [InlineData("opacity")]
    [InlineData("zoom")]
    [InlineData("zoom-delay")]
    [InlineData("delay")]
    [InlineData("active-delay")]
    [InlineData("highlight")]
    [InlineData("gdi")]
    [InlineData("timeout")]
    public void ValidationRejectsOutOfRangeScalarOptions(string option)
    {
        var settings = new AppSettings();
        switch (option)
        {
            case "poll": settings.Monitoring.PollIntervalMs = 99; break;
            case "grid": settings.Preview.GridSizeX = 0; break;
            case "opacity": settings.Display.Opacity = 1.01; break;
            case "zoom": settings.Zoom.Factor = 0.5; break;
            case "zoom-delay": settings.Zoom.DelayMs = 3001; break;
            case "delay": settings.Display.HideDelayMs = -1; break;
            case "active-delay": settings.Display.HideActiveDelayMs = -1; break;
            case "highlight": settings.Overlay.HighlightThickness = 0; break;
            case "gdi": settings.Compatibility.GdiFramesPerSecond = 0; break;
            case "timeout": settings.Compatibility.NativeCallTimeoutMs = 0; break;
        }

        Assert.False(settings.Validate().IsValid);
    }

    [Fact]
    public void NormalizeCanonicalizesHotKeys()
    {
        var settings = new AppSettings();
        settings.HotKeys.MinimizeAll = " shift+ctrl+f4 ";
        settings.CycleGroups[0].ForwardHotKey = "alt+f5";

        settings.Normalize();

        Assert.Equal("Ctrl+Shift+F4", settings.HotKeys.MinimizeAll);
        Assert.Equal("Alt+F5", settings.CycleGroups[0].ForwardHotKey);
    }

    [Fact]
    public void NormalizeClearsMouseHotKeys()
    {
        var settings = new AppSettings();
        settings.HotKeys.MinimizeAll = "Ctrl+XButton1";
        settings.CycleGroups[0].ForwardHotKey = "Alt+XButton2";
        settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - Alpha", HotKey = "Shift+XButton1" });

        settings.Normalize();

        Assert.Equal(string.Empty, settings.HotKeys.MinimizeAll);
        Assert.Equal(string.Empty, settings.CycleGroups[0].ForwardHotKey);
        Assert.Equal(string.Empty, settings.Clients[0].HotKey);
    }

    [Fact]
    public void ValidationRejectsMouseHotKeys()
    {
        var settings = new AppSettings();
        settings.HotKeys.MinimizeAll = "Ctrl+XButton1";

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("mouse", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidationRejectsDuplicateHotKeyBindings()
    {
        var settings = new AppSettings();
        settings.HotKeys.MinimizeAll = "Ctrl+F4";
        settings.CycleGroups[1].BackwardHotKey = "ctrl+f4";
        settings.Normalize();

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Ctrl+F4", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationRejectsMalformedHotKeyWithoutBlockingEmptyEntries()
    {
        var settings = new AppSettings();
        settings.HotKeys.MinimizeAll = "Ctrl+NotAKey";

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("MinimizeAll", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationRequiresAtLeastOneCycleGroup()
    {
        var settings = new AppSettings();
        settings.CycleGroups.Clear();

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("CycleGroups", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationAllowsDynamicCycleGroupCounts()
    {
        var settings = new AppSettings();
        settings.CycleGroups.RemoveRange(1, settings.CycleGroups.Count - 1);

        Assert.True(settings.Validate().IsValid);

        for (var index = 0; index < 3; index++)
        {
            settings.CycleGroups.Add(new CycleGroupSettings());
        }

        Assert.Equal(4, settings.CycleGroups.Count);
        Assert.True(settings.Validate().IsValid);
    }

    [Fact]
    public void NormalizePreservesDynamicCycleGroupCounts()
    {
        var settings = new AppSettings();
        settings.CycleGroups.RemoveRange(2, 3);

        settings.Normalize();

        Assert.Equal(2, settings.CycleGroups.Count);

        settings.CycleGroups.Clear();
        settings.Normalize();

        Assert.Single(settings.CycleGroups);
    }

    [Fact]
    public void ValidationRejectsUnsupportedSchemaVersion()
    {
        var settings = new AppSettings { SchemaVersion = 2 };

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("SchemaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void NormalizeDeduplicatesCycleGroupTitles()
    {
        var settings = new AppSettings();
        settings.CycleGroups[0].OrderedClientTitles = [" EVE - A ", "eve - a", "EVE - B"];
        settings.CycleGroups[0].ExcludedTitles = [" Login ", "login"];

        settings.Normalize();

        Assert.Equal(["EVE - A", "EVE - B"], settings.CycleGroups[0].OrderedClientTitles);
        Assert.Equal(["Login"], settings.CycleGroups[0].ExcludedTitles);
    }

    [Fact]
    public void ValidationRejectsUndefinedEnumValuesAndFontSize()
    {
        var settings = new AppSettings();
        settings.Preview.SnapMode = (SnapMode)99;
        settings.Compatibility.RenderingMode = (RenderingMode)99;
        settings.Zoom.Anchor = (ZoomAnchor)99;
        settings.Overlay.Position = (OverlayPosition)99;
        settings.Overlay.FontSize = 0;
        settings.Display.ExpansionThreshold = -1;

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("SnapMode", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("FontSize", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("ExpansionThreshold", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationRejectsHotKeysWithoutModifiers()
    {
        var settings = new AppSettings();
        settings.HotKeys.MinimizeAll = "A";

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("modifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidationRejectsInvalidClientWindowBounds()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings
        {
            OriginalTitle = "EVE - Main",
            ClientWindowBounds = new PixelRectangle(10, 20, 0, 720)
        });

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Client", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationRejectsDuplicateOrEmptyClientKeys()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - Main" });
        settings.Clients.Add(new ClientSettings { OriginalTitle = "eve - main" });
        settings.Clients.Add(new ClientSettings { OriginalTitle = " " });

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Clients", StringComparison.Ordinal));
    }
}
