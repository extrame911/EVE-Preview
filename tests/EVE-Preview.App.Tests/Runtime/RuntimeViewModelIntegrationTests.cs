using EvePreview.App.Services;
using EvePreview.App.ViewModels;
using EvePreview.Core;

namespace EvePreview.App.Tests;

public sealed class RuntimeViewModelIntegrationTests
{
    [Fact]
    public void Runtime_snapshot_populates_overview_and_diagnostics()
    {
        var runtime = new FakeRuntime();
        var viewModel = new MainWindowViewModel(runtime, new AppSettings());
        var identity = new ClientIdentity(42, 1234, DateTime.UnixEpoch);

        runtime.Push(new PreviewRuntimeSnapshot(
            [new PreviewClientStatus(identity, "Main", "EVE - Main", true, true, false)],
            "Main",
            null,
            1,
            3));

        Assert.Equal(1, viewModel.Overview.ClientCount);
        Assert.Equal("Main", viewModel.Overview.ActiveClientText);
        Assert.Single(viewModel.Overview.Clients);
        Assert.Equal("游戏可见", viewModel.Overview.Clients[0].ActivityText);
        Assert.Contains("预览窗口 1 个", viewModel.SystemSettings.DiagnosticSummary);
        Assert.Contains("已注册热键 3 个", viewModel.SystemSettings.DiagnosticSummary);
    }

    [Fact]
    public void Login_clients_are_hidden_from_client_lists_and_overview()
    {
        var runtime = new FakeRuntime();
        var viewModel = new MainWindowViewModel(runtime, new AppSettings());

        runtime.Push(new PreviewRuntimeSnapshot(
            [
                new PreviewClientStatus(
                    new ClientIdentity(41, 1233, DateTime.UnixEpoch),
                    "EVE",
                    "EVE",
                    false,
                    false,
                    false),
                new PreviewClientStatus(
                    new ClientIdentity(42, 1234, DateTime.UnixEpoch),
                    "Main",
                    "EVE - Main",
                    true,
                    true,
                    false)
            ],
            "Main",
            null,
            2,
            0));

        Assert.Equal(1, viewModel.Overview.ClientCount);
        Assert.Equal("EVE - Main", Assert.Single(viewModel.Clients.Clients).OriginalTitle);
        Assert.Equal("Main", Assert.Single(viewModel.Overview.Clients).CharacterName);
    }

    [Theory]
    [InlineData(true, "游戏可见")]
    [InlineData(false, "游戏隐藏")]
    public void Client_activity_text_follows_game_window_visibility(bool visible, string expected)
    {
        var status = new PreviewClientStatus(
            new ClientIdentity(42, 1234, DateTime.UnixEpoch),
            "Main",
            "EVE - Main",
            IsActive: true,
            IsPreviewVisible: true,
            IsExcludedFromCycle: false,
            IsGameVisible: visible);

        Assert.Equal(expected, status.ActivityText);
    }

    [Fact]
    public async Task Rapid_setting_edits_apply_only_the_latest_validated_snapshot()
    {
        var runtime = new FakeRuntime();
        var viewModel = new MainWindowViewModel(runtime, new AppSettings());

        viewModel.ThumbnailSettings.OpacityPercent = 55;
        viewModel.ThumbnailSettings.OpacityPercent = 65;
        viewModel.SystemSettings.PollingIntervalMilliseconds = 700;
        await Task.Delay(400);

        Assert.Equal(1, runtime.ApplyCalls);
        Assert.NotNull(runtime.LastApplied);
        Assert.Equal(0.65, runtime.LastApplied.Display.Opacity);
        Assert.Equal(700, runtime.LastApplied.Monitoring.PollIntervalMs);
    }

    [Fact]
    public async Task Save_all_window_layouts_command_updates_client_projections_and_status()
    {
        var runtime = new FakeRuntime
        {
            BatchLayoutResult = new ClientWindowLayoutBatchResult(
                new Dictionary<string, PixelRectangle>
                {
                    ["EVE - Alpha"] = new(10, 20, 1280, 720)
                },
                1)
        };
        var viewModel = new MainWindowViewModel(runtime, new AppSettings());
        runtime.Push(new PreviewRuntimeSnapshot(
            [new PreviewClientStatus(
                new ClientIdentity(1, 11, DateTime.UnixEpoch),
                "Alpha", "EVE - Alpha", false, true, false)],
            null, null, 1, 0));

        await viewModel.SaveAllWindowLayoutsCommand.ExecuteAsync(null);

        Assert.Equal(1, runtime.SaveAllLayoutCalls);
        Assert.Contains("(10, 20)", viewModel.Clients.Clients.Single().ClientWindowLayoutText);
        Assert.Contains("1", viewModel.Overview.LayoutStatusText);
        Assert.Contains("跳过 1", viewModel.Overview.LayoutStatusText);
    }

    [Fact]
    public void Recovered_runtime_error_is_cleared_from_the_status_bar()
    {
        var runtime = new FakeRuntime();
        var viewModel = new MainWindowViewModel(runtime, new AppSettings());

        runtime.Push(new PreviewRuntimeSnapshot([], null, "native call failed", 0, 0));
        Assert.Contains("native call failed", viewModel.ServiceStatusText);

        runtime.Push(new PreviewRuntimeSnapshot([], null, null, 0, 0));

        Assert.DoesNotContain("native call failed", viewModel.ServiceStatusText);
        Assert.Equal(string.Empty, viewModel.LastError);
    }

    [Fact]
    public void Save_failure_prompt_reports_the_error_once_until_save_recovers()
    {
        var runtime = new FakeRuntime();
        var viewModel = new MainWindowViewModel(runtime, new AppSettings());
        var prompts = new List<string>();
        viewModel.SaveFailurePromptRequested += error => prompts.Add(error);

        runtime.Push(new PreviewRuntimeSnapshot([], null, "保存配置失败：磁盘不可用", 0, 0,
            CanDiscardUnsavedChanges: true, SaveError: "保存配置失败：磁盘不可用"));
        runtime.Push(new PreviewRuntimeSnapshot([], null, "保存配置失败：磁盘不可用", 0, 0,
            CanDiscardUnsavedChanges: true, SaveError: "保存配置失败：磁盘不可用"));
        runtime.Push(new PreviewRuntimeSnapshot([], null, "窗口操作失败：客户端已退出", 0, 0,
            CanDiscardUnsavedChanges: true, SaveError: "保存配置失败：磁盘不可用"));

        Assert.Equal("保存配置失败：磁盘不可用", Assert.Single(prompts));

        runtime.Push(new PreviewRuntimeSnapshot([], null, null, 0, 0));
        runtime.Push(new PreviewRuntimeSnapshot([], null, "保存配置失败：目录已只读", 0, 0,
            CanDiscardUnsavedChanges: true, SaveError: "保存配置失败：目录已只读"));

        Assert.Equal(2, prompts.Count);
        Assert.Equal("保存配置失败：目录已只读", prompts[1]);
    }

    [Fact]
    public async Task Save_failure_can_discard_edits_and_refresh_every_settings_view()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - Alpha", Alias = "Main" });
        var runtime = new FakeRuntime { RestoredSettings = settings.DeepCopy() };
        var viewModel = new MainWindowViewModel(runtime, settings);
        runtime.Push(new PreviewRuntimeSnapshot([], null, "保存配置失败：磁盘不可用", 0, 0,
            CanDiscardUnsavedChanges: true));
        viewModel.ThumbnailSettings.OpacityPercent = 55;
        viewModel.SystemSettings.PollingIntervalMilliseconds = 900;
        viewModel.Hotkeys.MinimizeAllHotkey = "Ctrl+F9";
        viewModel.Clients.SelectedClient!.Alias = "Changed";
        viewModel.CycleGroups.Groups[0].ForwardHotkey = "Ctrl+F10";

        await viewModel.DiscardUnsavedChangesCommand.ExecuteAsync(null);

        Assert.Equal(1, runtime.RestoreCalls);
        Assert.Equal(0.8, viewModel.Settings.Display.Opacity);
        Assert.Equal(80, viewModel.ThumbnailSettings.OpacityPercent);
        Assert.Equal(500, viewModel.SystemSettings.PollingIntervalMilliseconds);
        Assert.Equal("Ctrl+Shift+M", viewModel.Hotkeys.MinimizeAllHotkey);
        Assert.Equal("Main", viewModel.Clients.SelectedClient!.Alias);
        Assert.Equal(string.Empty, viewModel.CycleGroups.Groups[0].ForwardHotkey);
        Assert.False(viewModel.CanDiscardUnsavedChanges);
        Assert.Contains("已取消未保存修改", viewModel.SystemSettings.ProfileStatusText);
    }

    private sealed class FakeRuntime : IPreviewRuntime, IPreviewRuntimeController, IPreviewRuntimeStatusSource, IClientWindowLayoutController, IPreviewRuntimeSettingsRecovery
    {
        private PreviewRuntimeSnapshot _snapshot = new([], null, null, 0, 0);

        public int ApplyCalls { get; private set; }
        public AppSettings? LastApplied { get; private set; }
        public PreviewRuntimeSnapshot CurrentSnapshot => _snapshot;

        public event EventHandler<PreviewRuntimeSnapshot>? SnapshotChanged;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ApplySettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            LastApplied = settings.DeepCopy();
            return Task.CompletedTask;
        }

        public Task MinimizeAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RestoreAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ClientWindowLayoutBatchResult BatchLayoutResult { get; init; } = new(
            new Dictionary<string, PixelRectangle>(), 0);
        public int SaveAllLayoutCalls { get; private set; }

        public Task<PixelRectangle?> SaveClientWindowLayoutAsync(
            string originalWindowTitle,
            CancellationToken cancellationToken = default) => Task.FromResult<PixelRectangle?>(null);

        public Task<ClientWindowLayoutBatchResult> SaveAllClientWindowLayoutsAsync(
            CancellationToken cancellationToken = default)
        {
            SaveAllLayoutCalls++;
            return Task.FromResult(BatchLayoutResult);
        }

        public Task<bool> ApplyClientWindowLayoutAsync(
            string originalWindowTitle,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public AppSettings RestoredSettings { get; init; } = new();
        public int RestoreCalls { get; private set; }

        public Task<AppSettings> RestoreLastSavedSettingsAsync(
            AppSettings target,
            CancellationToken cancellationToken = default)
        {
            RestoreCalls++;
            target.CopyFrom(RestoredSettings);
            Push(new PreviewRuntimeSnapshot([], null, null, 0, 0));
            return Task.FromResult(RestoredSettings.DeepCopy());
        }

        public void Push(PreviewRuntimeSnapshot snapshot)
        {
            _snapshot = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }
    }
}
