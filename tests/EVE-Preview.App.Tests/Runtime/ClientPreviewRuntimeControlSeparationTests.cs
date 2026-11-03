using System.IO;
using System.Reflection;
using EvePreview.App.Services;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class ClientPreviewRuntimeControlSeparationTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Activation_uses_a_known_client_when_no_preview_window_exists()
    {
        wpf.Invoke(async () =>
        {
            var settings = new AppSettings();
            var queue = new FakeWindowOperationQueue();
            var identity = new ClientIdentity(11, 201, DateTime.UnixEpoch);
            var client = new ProcessSnapshot(11, "exefile", 201, "EVE - Alpha", DateTime.UnixEpoch);
            var runtime = CreateRuntime(settings, queue, identity, client);
            try
            {
                await InvokeTask(runtime, "ActivateClientCoreAsync", identity, CancellationToken.None);

                var request = Assert.Single(queue.Requests);
                Assert.Equal(WindowOperationKind.Activate, request.Kind);
                Assert.Equal((nint)201, request.WindowHandle);
                Assert.Equal(11, request.ExpectedProcessId);
            }
            finally
            {
                runtime.Dispose();
            }
        });
    }

    [Fact]
    public void Cycle_group_uses_known_clients_when_previews_are_stopped()
    {
        wpf.Invoke(async () =>
        {
            var settings = new AppSettings();
            settings.CycleGroups[0].OrderedClientTitles = ["EVE - Alpha", "EVE - Beta"];
            var queue = new FakeWindowOperationQueue();
            var alphaIdentity = new ClientIdentity(11, 201, DateTime.UnixEpoch);
            var betaIdentity = new ClientIdentity(12, 202, DateTime.UnixEpoch);
            var alpha = new ProcessSnapshot(11, "exefile", 201, "EVE - Alpha", DateTime.UnixEpoch);
            var beta = new ProcessSnapshot(12, "exefile", 202, "EVE - Beta", DateTime.UnixEpoch);
            var runtime = CreateRuntime(settings, queue, alphaIdentity, alpha, betaIdentity, beta);
            try
            {
                typeof(ClientPreviewRuntime)
                    .GetField("_activeIdentity", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(runtime, alphaIdentity);

                await InvokeTask(runtime, "CycleAsync", 0, CycleDirection.Forward);

                var request = Assert.Single(queue.Requests);
                Assert.Equal(WindowOperationKind.Activate, request.Kind);
                Assert.Equal((nint)202, request.WindowHandle);
            }
            finally
            {
                runtime.Dispose();
            }
        });
    }

    private static ClientPreviewRuntime CreateRuntime(
        AppSettings settings,
        FakeWindowOperationQueue queue,
        params object[] clients)
    {
        var storePath = Path.Combine(AppContext.BaseDirectory, $"control-separation-{Guid.NewGuid():N}.json");
        var runtime = new ClientPreviewRuntime(settings, new JsonSettingsStore(storePath), queue);
        var known = (Dictionary<ClientIdentity, ProcessSnapshot>)typeof(ClientPreviewRuntime)
            .GetField("_knownClients", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(runtime)!;
        for (var index = 0; index < clients.Length; index += 2)
        {
            known[(ClientIdentity)clients[index]] = (ProcessSnapshot)clients[index + 1];
        }

        return runtime;
    }

    private static async Task InvokeTask(ClientPreviewRuntime runtime, string method, params object[] arguments)
    {
        var task = (Task)typeof(ClientPreviewRuntime)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(runtime, arguments)!;
        await task;
    }

    private sealed class FakeWindowOperationQueue : IWindowOperationQueue
    {
        public List<WindowOperationRequest> Requests { get; } = [];

        public nint GetForegroundWindow() => 0;
        public bool IsMinimized(nint windowHandle) => false;
        public int GetWindowProcessId(nint windowHandle) => 0;
        public bool TryGetWindowBounds(nint windowHandle, out WindowBounds bounds)
        {
            bounds = new WindowBounds(0, 0, 1280, 720);
            return true;
        }

        public Task<WindowOperationResult> EnqueueAsync(
            WindowOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(WindowOperationResult.Success());
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
