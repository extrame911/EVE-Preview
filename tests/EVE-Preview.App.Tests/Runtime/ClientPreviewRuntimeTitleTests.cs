using System.Reflection;
using System.IO;
using System.Windows;
using EvePreview.App.Services;
using EvePreview.Core;
using EvePreview.Windows;
using Application = System.Windows.Application;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class ClientPreviewRuntimeTitleTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Title_change_without_saved_client_settings_still_reports_previous_title()
    {
        wpf.Invoke(() =>
        {
            ClientPreviewRuntime? runtime = null;
            try
            {
                runtime = new ClientPreviewRuntime(
                    new AppSettings(),
                    new JsonSettingsStore(Path.Combine(AppContext.BaseDirectory, "unused-settings.json")));
                const string loginTitle = "星战前夜：晨曦 [Infinity]";
                const string characterTitle = "星战前夜：晨曦 [Infinity] - 上不完的班";

                typeof(ClientPreviewRuntime)
                    .GetMethod("MigrateClientTitle", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(runtime, [loginTitle, characterTitle]);

                var renamed = (Dictionary<string, string>)typeof(ClientPreviewRuntime)
                    .GetField("_renamedClientTitles", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(runtime)!;
                Assert.True(renamed.TryGetValue(characterTitle, out var previousTitle));
                Assert.Equal(loginTitle, previousTitle);
            }
            finally
            {
                runtime?.Dispose();
            }
        });
    }

    [Fact]
    public void Login_transition_applies_saved_preview_placement_and_alias()
    {
        wpf.Invoke(() =>
        {
            ClientPreviewRuntime? runtime = null;
            try
            {
                const string loginTitle = "星战前夜：晨曦 [Infinity]";
                const string characterTitle = "星战前夜：晨曦 [Infinity] - 上不完的班";
                var settings = new AppSettings();
                settings.Clients.Add(new ClientSettings
                {
                    OriginalTitle = characterTitle,
                    Alias = "Main",
                    Placement = new PreviewPlacement(120, 140, 320, 180)
                });
                runtime = new ClientPreviewRuntime(
                    settings,
                    new JsonSettingsStore(Path.Combine(AppContext.BaseDirectory, "unused-settings.json")));
                var preview = new PreviewWindow(
                    new ProcessSnapshot(7, "exefile", 701, loginTitle, DateTime.Now),
                    loginTitle,
                    settings,
                    new PreviewPlacement(10, 20, 320, 180),
                    (_, _) => { },
                    (_, _) => { });
                var previews = (Dictionary<ClientIdentity, PreviewWindow>)typeof(ClientPreviewRuntime)
                    .GetField("_previews", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(runtime)!;
                previews.Add(preview.Identity, preview);

                typeof(ClientPreviewRuntime)
                    .GetMethod("UpdatePreviewTitle", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(runtime, [new ClientSnapshot(preview.Identity, characterTitle)]);

                Assert.Equal(120, preview.Placement.Left);
                Assert.Equal(140, preview.Placement.Top);
                Assert.Equal("Main", preview.DisplayName);
                var overlayTitle = (string)typeof(PreviewWindow)
                    .GetMethod("GetOverlayTitle", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(preview, null)!;
                Assert.Equal("Main", overlayTitle);
            }
            finally
            {
                runtime?.Dispose();
            }
        });
    }
}
