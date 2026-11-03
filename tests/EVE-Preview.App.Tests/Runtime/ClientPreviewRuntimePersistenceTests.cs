using System.IO;
using EvePreview.App.Services;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class ClientPreviewRuntimePersistenceTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Save_failure_restores_last_persisted_settings_without_reading_the_locked_file()
    {
        wpf.Invoke(async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"persistence-test-{Guid.NewGuid():N}.json");
            var store = new JsonSettingsStore(path);
            var settings = new AppSettings();
            store.Save(settings);
            var runtime = new ClientPreviewRuntime(settings, store);
            try
            {
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    settings.Display.Opacity = 0.55;
                    await runtime.ApplySettingsAsync(settings);
                    await WaitForAsync(() => runtime.CurrentSnapshot.CanDiscardUnsavedChanges);

                    var restored = await runtime.RestoreLastSavedSettingsAsync(settings);

                    Assert.Equal(0.8, restored.Display.Opacity);
                    Assert.Equal(0.8, runtime.Settings.Display.Opacity);
                    Assert.False(runtime.CurrentSnapshot.CanDiscardUnsavedChanges);
                    Assert.Null(runtime.CurrentSnapshot.LastError);
                }

                Assert.Equal(0.8, store.Load().Settings.Display.Opacity);
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(path);
            }
        });
    }

    [Fact]
    public void Saving_recovers_after_the_file_lock_is_released()
    {
        wpf.Invoke(async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"persistence-test-{Guid.NewGuid():N}.json");
            var store = new JsonSettingsStore(path);
            var settings = new AppSettings();
            store.Save(settings);
            var runtime = new ClientPreviewRuntime(settings, store);
            try
            {
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    settings.Display.Opacity = 0.55;
                    await runtime.ApplySettingsAsync(settings);
                    await WaitForAsync(() => runtime.CurrentSnapshot.CanDiscardUnsavedChanges);
                }

                settings.Display.Opacity = 0.65;
                await runtime.ApplySettingsAsync(settings);
                await WaitForAsync(() => !runtime.CurrentSnapshot.CanDiscardUnsavedChanges);

                Assert.Null(runtime.CurrentSnapshot.LastError);
                Assert.Equal(0.65, store.Load().Settings.Display.Opacity);
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(path);
            }
        });
    }

    [Fact]
    public void Unrelated_window_error_does_not_hide_unsaved_changes_recovery()
    {
        wpf.Invoke(async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"persistence-test-{Guid.NewGuid():N}.json");
            var settings = new AppSettings();
            var store = new JsonSettingsStore(path);
            store.Save(settings);
            var queue = new FakeWindowOperationQueue();
            var runtime = new ClientPreviewRuntime(settings, store, queue);
            try
            {
                AddPreview(runtime, settings, new PreviewPlacement(40, 50, 400, 200));
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    settings.Display.Opacity = 0.55;
                    await runtime.ApplySettingsAsync(settings);
                    await WaitForAsync(() => runtime.CurrentSnapshot.CanDiscardUnsavedChanges);

                    queue.Result = WindowOperationResult.Failure(WindowOperationError.InvalidWindow, message: "gone");
                    await runtime.MinimizeAllAsync();

                    Assert.True(runtime.CurrentSnapshot.CanDiscardUnsavedChanges);
                    Assert.Contains("窗口操作失败", runtime.CurrentSnapshot.LastError);
                }
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(path);
            }
        });
    }

    [Fact]
    public void Stop_save_failure_keeps_unsaved_changes_recovery_available()
    {
        wpf.Invoke(async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"persistence-test-{Guid.NewGuid():N}.json");
            var settings = new AppSettings();
            settings.Monitoring.ProcessNames = ["eve-preview-no-such-client"];
            settings.HotKeys.MinimizeAll = string.Empty;
            settings.HotKeys.RefreshMinimized = string.Empty;
            var store = new JsonSettingsStore(path);
            store.Save(settings);
            var runtime = new ClientPreviewRuntime(settings, store, new FakeWindowOperationQueue());
            try
            {
                await runtime.StartAsync();
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    await runtime.StopAsync();

                    Assert.True(runtime.CurrentSnapshot.CanDiscardUnsavedChanges);
                }
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(path);
            }
        });
    }

    [Fact]
    public void Restore_does_not_rewrite_the_last_saved_preview_placement()
    {
        wpf.Invoke(async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"persistence-test-{Guid.NewGuid():N}.json");
            var settings = new AppSettings();
            var originalPlacement = new PreviewPlacement(40, 50, 400, 200);
            settings.Clients.Add(new ClientSettings
            {
                OriginalTitle = "EVE - Alpha",
                Placement = originalPlacement
            });
            var store = new JsonSettingsStore(path);
            store.Save(settings);
            var runtime = new ClientPreviewRuntime(settings, store, new FakeWindowOperationQueue());
            try
            {
                AddPreview(runtime, settings, originalPlacement);
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    settings.Preview.DefaultWidth = 640;
                    settings.Preview.DefaultHeight = 360;
                    await runtime.ApplySettingsAsync(settings);
                    await WaitForAsync(() => runtime.CurrentSnapshot.CanDiscardUnsavedChanges);

                    var restored = await runtime.RestoreLastSavedSettingsAsync(settings);

                    Assert.Equal(originalPlacement, restored.Clients[0].Placement);
                    Assert.Equal(originalPlacement, runtime.Settings.Clients[0].Placement);
                }
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(path);
            }
        });
    }

    [Fact]
    public void Layout_saved_after_recovery_survives_the_next_settings_edit()
    {
        wpf.Invoke(async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"persistence-test-{Guid.NewGuid():N}.json");
            var settings = new AppSettings();
            settings.Clients.Add(new ClientSettings
            {
                OriginalTitle = "EVE - Alpha",
                ClientWindowBounds = new PixelRectangle(10, 20, 300, 200)
            });
            var store = new JsonSettingsStore(path);
            store.Save(settings);
            var queue = new FakeWindowOperationQueue();
            queue.Bounds[101] = new WindowBounds(100, 200, 1280, 720);
            var runtime = new ClientPreviewRuntime(settings, store, queue);
            try
            {
                AddPreview(runtime, settings, new PreviewPlacement(40, 50, 400, 200));
                var viewModel = new EvePreview.App.ViewModels.MainWindowViewModel(runtime, settings);
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    viewModel.ThumbnailSettings.OpacityPercent = 55;
                    await WaitForAsync(() => runtime.CurrentSnapshot.CanDiscardUnsavedChanges, "initial save failure");
                    await viewModel.DiscardUnsavedChangesCommand.ExecuteAsync(null);
                }

                await viewModel.SaveAllWindowLayoutsCommand.ExecuteAsync(null);
                var expectedBounds = new PixelRectangle(100, 200, 1280, 720);
                Assert.Equal(expectedBounds, runtime.Settings.Clients[0].ClientWindowBounds);
                await WaitForAsync(() => store.Load().Settings.Clients[0].ClientWindowBounds == expectedBounds, "layout save");

                viewModel.ThumbnailSettings.OpacityPercent = 65;
                await WaitForAsync(() => store.Load().Settings.Display.Opacity == 0.65, "post-layout settings save");

                Assert.Equal(expectedBounds, store.Load().Settings.Clients[0].ClientWindowBounds);
            }
            finally
            {
                runtime.Dispose();
                DeleteStoreFiles(path);
            }
        });
    }

    [Fact]
    public void Edits_made_while_restore_waits_cannot_be_saved_after_restore()
    {
        wpf.Invoke(async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"persistence-test-{Guid.NewGuid():N}.json");
            var settings = new AppSettings();
            var store = new JsonSettingsStore(path);
            store.Save(settings);
            var runtime = new ClientPreviewRuntime(settings, store, new FakeWindowOperationQueue());
            var saveGate = (SemaphoreSlim)typeof(ClientPreviewRuntime)
                .GetField("_saveGate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(runtime)!;
            saveGate.Wait();
            var gateHeld = true;
            try
            {
                var restoreTask = runtime.RestoreLastSavedSettingsAsync(settings);
                settings.Display.Opacity = 0.55;
                await runtime.ApplySettingsAsync(settings);
                saveGate.Release();
                gateHeld = false;

                await restoreTask;
                Assert.Equal(0.8, settings.Display.Opacity);
                await Task.Delay(800);

                Assert.Equal(0.8, store.Load().Settings.Display.Opacity);
            }
            finally
            {
                if (gateHeld)
                {
                    saveGate.Release();
                }

                runtime.Dispose();
                DeleteStoreFiles(path);
            }
        });
    }

    private static void AddPreview(
        ClientPreviewRuntime runtime,
        AppSettings settings,
        PreviewPlacement placement)
    {
        var preview = new PreviewWindow(
            new ProcessSnapshot(1, "exefile", 101, "EVE - Alpha", DateTime.Now),
            "EVE - Alpha",
            settings,
            placement,
            (_, _) => { },
            (_, _) => { });
        var previews = (Dictionary<ClientIdentity, PreviewWindow>)typeof(ClientPreviewRuntime)
            .GetField("_previews", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(runtime)!;
        previews.Add(preview.Identity, preview);
        var knownClients = (Dictionary<ClientIdentity, ProcessSnapshot>)typeof(ClientPreviewRuntime)
            .GetField("_knownClients", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(runtime)!;
        knownClients[preview.Identity] = preview.Client;
    }

    private sealed class FakeWindowOperationQueue : EvePreview.Windows.IWindowOperationQueue
    {
        public Dictionary<nint, WindowBounds> Bounds { get; } = [];
        public WindowOperationResult Result { get; set; } = WindowOperationResult.Success();
        public nint GetForegroundWindow() => 0;
        public bool IsMinimized(nint windowHandle) => false;
        public int GetWindowProcessId(nint windowHandle) => 1;
        public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds) =>
            Bounds.TryGetValue(windowHandle, out bounds);
        public Task<WindowOperationResult> EnqueueAsync(
            WindowOperationRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(Result);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static async Task WaitForAsync(Func<bool> condition, string? stage = null)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(4);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"The expected runtime state was not reached: {stage ?? "unspecified"}.");
            }

            await Task.Delay(50);
        }
    }

    private static void DeleteStoreFiles(string path)
    {
        foreach (var candidate in Directory.EnumerateFiles(
                     Path.GetDirectoryName(path)!,
                     Path.GetFileName(path) + "*"))
        {
            File.Delete(candidate);
        }
    }
}
