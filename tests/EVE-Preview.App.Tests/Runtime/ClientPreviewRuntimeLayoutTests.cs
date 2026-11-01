using System.Windows;
using System.Windows.Interop;
using System.IO;
using System.Reflection;
using EvePreview.App.Services;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class ClientPreviewRuntimeLayoutTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Save_all_running_clients_persists_readable_windows_and_skips_minimized()
    {
        wpf.Invoke(async () =>
        {
            var settings = new AppSettings();
            var queue = new FakeWindowOperationQueue();
            queue.Bounds[101] = new WindowBounds(10, 20, 1280, 720);
            queue.Bounds[102] = new WindowBounds(30, 40, 1600, 900);
            queue.Bounds[103] = new WindowBounds(50, 60, 1920, 1080);
            queue.Minimized.Add(102);
            var storePath = Path.Combine(AppContext.BaseDirectory, $"layout-test-{Guid.NewGuid():N}.json");
            var runtime = new ClientPreviewRuntime(settings, new JsonSettingsStore(storePath), queue);
            try
            {
                AddPreview(runtime, settings, 1, 101, "EVE - Alpha");
                AddPreview(runtime, settings, 2, 102, "EVE - Minimized");
                AddPreview(runtime, settings, 3, 103, "EVE - Beta");

                var result = await runtime.SaveAllClientWindowLayoutsAsync();

                Assert.Equal(2, result.SavedCount);
                Assert.Equal(1, result.SkippedCount);
                Assert.Equal(new PixelRectangle(10, 20, 1280, 720), result.SavedLayouts["EVE - Alpha"]);
                Assert.Equal(new PixelRectangle(50, 60, 1920, 1080), result.SavedLayouts["EVE - Beta"]);
                Assert.Equal(2, settings.Clients.Count(client => client.ClientWindowBounds is not null));
                Assert.DoesNotContain(settings.Clients, client => client.OriginalTitle == "EVE - Minimized");
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(storePath);
            }
        });
    }

    [Fact]
    public void Added_logged_in_client_applies_the_saved_client_window_layout()
    {
        wpf.Invoke(() =>
        {
            const string characterTitle = "EVE - Alpha";
            var settings = new AppSettings();
            settings.Clients.Add(new ClientSettings
            {
                OriginalTitle = characterTitle,
                Placement = new PreviewPlacement(-10000, -10000, 320, 180),
                ClientWindowBounds = new PixelRectangle(-40, 120, 1920, 1080)
            });
            var queue = new FakeWindowOperationQueue();
            var storePath = Path.Combine(AppContext.BaseDirectory, $"layout-test-{Guid.NewGuid():N}.json");
            var runtime = new ClientPreviewRuntime(settings, new JsonSettingsStore(storePath), queue);
            Window? source = null;
            try
            {
                source = new Window
                {
                    Width = 320,
                    Height = 180,
                    Left = -10000,
                    Top = -10000,
                    WindowStyle = WindowStyle.None,
                    ShowActivated = false,
                    ShowInTaskbar = false
                };
                source.Show();
                var handle = new WindowInteropHelper(source).Handle;
                var process = new ProcessSnapshot(
                    Environment.ProcessId, "exefile", handle, characterTitle, DateTime.Now);
                var identity = new ClientIdentity(
                    process.ProcessId, process.MainWindowHandle, process.ProcessStartTime.ToUniversalTime());

                typeof(ClientPreviewRuntime)
                    .GetMethod("AddPreview", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(runtime, [new ClientSnapshot(identity, characterTitle), new[] { process }]);

                Assert.Collection(queue.Requests,
                    request => Assert.Equal(WindowOperationKind.Restore, request.Kind),
                    request => Assert.Equal(WindowOperationKind.MoveResize, request.Kind));
            }
            finally
            {
                runtime.Dispose();
                source?.Close();
                DeleteStoreFiles(storePath);
            }
        });
    }

    [Fact]
    public void Disabled_auto_restore_skips_the_saved_layout_when_a_logged_in_client_is_added()
    {
        wpf.Invoke(() =>
        {
            const string characterTitle = "EVE - Alpha";
            var settings = new AppSettings();
            settings.General.AutoRestoreClientWindowOnStartup = false;
            settings.Clients.Add(new ClientSettings
            {
                OriginalTitle = characterTitle,
                ClientWindowBounds = new PixelRectangle(-40, 120, 1920, 1080)
            });
            var queue = new FakeWindowOperationQueue();
            var storePath = Path.Combine(AppContext.BaseDirectory, $"layout-test-{Guid.NewGuid():N}.json");
            var runtime = new ClientPreviewRuntime(settings, new JsonSettingsStore(storePath), queue);
            try
            {
                AddPreview(runtime, settings, 1, 101, characterTitle);

                Assert.Empty(queue.Requests);
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(storePath);
            }
        });
    }

    [Fact]
    public void Manual_apply_uses_the_saved_layout_even_when_auto_restore_is_disabled()
    {
        wpf.Invoke(async () =>
        {
            const string characterTitle = "EVE - Alpha";
            var settings = new AppSettings();
            settings.General.AutoRestoreClientWindowOnStartup = false;
            settings.Clients.Add(new ClientSettings
            {
                OriginalTitle = characterTitle,
                ClientWindowBounds = new PixelRectangle(-40, 120, 1920, 1080)
            });
            var queue = new FakeWindowOperationQueue();
            var storePath = Path.Combine(AppContext.BaseDirectory, $"layout-test-{Guid.NewGuid():N}.json");
            var runtime = new ClientPreviewRuntime(settings, new JsonSettingsStore(storePath), queue);
            try
            {
                AddPreview(runtime, settings, 1, 101, characterTitle);
                queue.Requests.Clear();

                var applied = await runtime.ApplyClientWindowLayoutAsync(characterTitle);

                Assert.True(applied);
                Assert.Collection(queue.Requests,
                    request => Assert.Equal(WindowOperationKind.Restore, request.Kind),
                    request =>
                    {
                        Assert.Equal(WindowOperationKind.MoveResize, request.Kind);
                        Assert.Equal(new WindowBounds(-40, 120, 1920, 1080), request.Bounds);
                    });
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(storePath);
            }
        });
    }

    [Fact]
    public void Login_title_transition_applies_the_saved_client_window_layout()
    {
        wpf.Invoke(() =>
        {
            const string characterTitle = "EVE - Alpha";
            var settings = new AppSettings();
            settings.Clients.Add(new ClientSettings
            {
                OriginalTitle = characterTitle,
                ClientWindowBounds = new PixelRectangle(-40, 120, 1920, 1080)
            });
            var queue = new FakeWindowOperationQueue();
            var storePath = Path.Combine(AppContext.BaseDirectory, $"layout-test-{Guid.NewGuid():N}.json");
            var runtime = new ClientPreviewRuntime(settings, new JsonSettingsStore(storePath), queue);
            try
            {
                var identity = AddPreview(runtime, settings, 1, 101, "EVE");

                typeof(ClientPreviewRuntime)
                    .GetMethod("UpdatePreviewTitle", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(runtime, [new ClientSnapshot(identity, characterTitle)]);

                Assert.Collection(queue.Requests,
                    request => Assert.Equal(WindowOperationKind.Restore, request.Kind),
                    request =>
                    {
                        Assert.Equal(WindowOperationKind.MoveResize, request.Kind);
                        Assert.Equal(new WindowBounds(-40, 120, 1920, 1080), request.Bounds);
                    });
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(storePath);
            }
        });
    }

    [Fact]
    public void Snapshot_reports_current_preview_placement_instead_of_stale_saved_placement()
    {
        wpf.Invoke(() =>
        {
            const string characterTitle = "EVE - Alpha";
            var settings = new AppSettings();
            settings.Clients.Add(new ClientSettings
            {
                OriginalTitle = characterTitle,
                Placement = new PreviewPlacement(10, 20, 160, 90)
            });
            var storePath = Path.Combine(AppContext.BaseDirectory, $"placement-snapshot-test-{Guid.NewGuid():N}.json");
            var runtime = new ClientPreviewRuntime(settings, new JsonSettingsStore(storePath), new FakeWindowOperationQueue());
            try
            {
                AddPreview(runtime, settings, 1, 101, characterTitle);
                typeof(ClientPreviewRuntime)
                    .GetMethod("PublishSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(runtime, [null]);

                var status = Assert.Single(runtime.CurrentSnapshot.Clients);
                Assert.NotNull(status.Placement);
                Assert.Equal(320, status.Placement!.Width);
                Assert.Equal(180, status.Placement.Height);
                Assert.True(status.HasSavedPlacement);
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(storePath);
            }
        });
    }

    [Fact]
    public void Stopping_runtime_clears_default_slot_assignments()
    {
        wpf.Invoke(() =>
        {
            var settings = new AppSettings();
            var storePath = Path.Combine(AppContext.BaseDirectory, $"slot-cleanup-test-{Guid.NewGuid():N}.json");
            var runtime = new ClientPreviewRuntime(settings, new JsonSettingsStore(storePath), new FakeWindowOperationQueue());
            var slots = (Dictionary<ClientIdentity, int>)typeof(ClientPreviewRuntime)
                .GetField("_defaultPreviewSlots", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(runtime)!;
            try
            {
                slots[new ClientIdentity(42, 1234, DateTime.UnixEpoch)] = 2;
                runtime.Dispose();
                Assert.Empty(slots);
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(storePath);
            }
        });
    }

    private static ClientIdentity AddPreview(
        ClientPreviewRuntime runtime,
        AppSettings settings,
        int processId,
        nint handle,
        string title)
    {
        var preview = new PreviewWindow(
            new ProcessSnapshot(processId, "exefile", handle, title, DateTime.Now),
            title,
            settings,
            new PreviewPlacement(0, 0, 320, 180),
            (_, _) => { },
            (_, _) => { });
        var previews = (Dictionary<ClientIdentity, PreviewWindow>)typeof(ClientPreviewRuntime)
            .GetField("_previews", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(runtime)!;
        previews.Add(preview.Identity, preview);
        return preview.Identity;
    }

    private static void DeleteStoreFiles(string storePath)
    {
        foreach (var path in new[] { storePath, storePath + ".bak" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class FakeWindowOperationQueue : IWindowOperationQueue
    {
        public Dictionary<nint, WindowBounds> Bounds { get; } = [];
        public HashSet<nint> Minimized { get; } = [];
        public List<WindowOperationRequest> Requests { get; } = [];

        public nint GetForegroundWindow() => 0;
        public bool IsMinimized(nint windowHandle) => Minimized.Contains(windowHandle);
        public int GetWindowProcessId(nint windowHandle) => 0;
        public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds) =>
            Bounds.TryGetValue(windowHandle, out bounds);

        public Task<WindowOperationResult> EnqueueAsync(
            WindowOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(WindowOperationResult.Success());
        }

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
