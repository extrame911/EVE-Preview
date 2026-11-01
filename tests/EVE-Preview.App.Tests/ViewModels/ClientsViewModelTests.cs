using EvePreview.App.Services;
using EvePreview.App.ViewModels;
using EvePreview.Core;

namespace EvePreview.App.Tests;

public sealed class ClientsViewModelTests
{
    [Fact]
    public void Online_clients_are_added_once_and_keep_configuration_when_offline()
    {
        var settings = new AppSettings();
        var viewModel = new ClientsViewModel(settings);
        var identity = new ClientIdentity(42, 1234, DateTime.UnixEpoch);

        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot(
            [new PreviewClientStatus(identity, "EVE - Main", "EVE - Main", false, true, false)],
            null,
            null,
            1,
            0));
        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot(
            [new PreviewClientStatus(identity, "EVE - Main", "EVE - Main", false, true, false)],
            null,
            null,
            1,
            0));

        var client = Assert.Single(viewModel.Clients);
        Assert.True(client.IsOnline);

        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot([], null, null, 0, 0));

        Assert.Single(viewModel.Clients);
        Assert.False(client.IsOnline);
        Assert.Equal("离线", client.OnlineStatusText);
    }

    [Fact]
    public void Stored_login_configuration_is_not_shown_in_client_list()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings { OriginalTitle = "星战前夜：晨曦 [Infinity]" });
        settings.Clients.Add(new ClientSettings { OriginalTitle = "星战前夜：晨曦 [Infinity] - 上不完的班" });

        var viewModel = new ClientsViewModel(settings);

        Assert.Equal("星战前夜：晨曦 [Infinity] - 上不完的班", Assert.Single(viewModel.Clients).OriginalTitle);
    }

    [Theory]
    [InlineData("星战前夜：晨曦 [Infinity] - 上不完的班", "上不完的班", "星战前夜：晨曦 [Infinity]")]
    [InlineData("星战前夜：晨曦 [Infinity]", "[未登录]", "星战前夜：晨曦 [Infinity]")]
    public void Client_list_uses_character_and_server_names_from_window_title(
        string title,
        string character,
        string server)
    {
        var client = new ClientConfigViewModel(new ClientSettings { OriginalTitle = title });

        Assert.Equal(character, client.CharacterName);
        Assert.Equal(server, client.ServerName);
        Assert.Equal($"{character} · {server}", client.AutomationName);
    }

    [Fact]
    public void Automation_name_follows_the_live_window_title()
    {
        var client = new ClientConfigViewModel(new ClientSettings { OriginalTitle = "EVE - Login" });
        var changed = new List<string?>();
        client.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        client.LiveWindowTitle = "EVE - Main";

        Assert.Equal("Main · EVE", client.AutomationName);
        Assert.Contains(nameof(ClientConfigViewModel.AutomationName), changed);
    }

    [Fact]
    public void Alias_and_client_options_are_saved_to_core_settings()
    {
        var settings = new AppSettings();
        var viewModel = new ClientsViewModel(settings);
        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot(
            [new PreviewClientStatus(
                new ClientIdentity(42, 1234, DateTime.UnixEpoch),
                "EVE - Main",
                "EVE - Main",
                false,
                true,
                false)],
            null,
            null,
            1,
            0));
        var client = viewModel.Clients[0];
        var changes = 0;
        viewModel.Changed += (_, _) => changes++;

        client.Alias = "主账号";
        client.HotKey = "Ctrl+Shift+1";
        client.ExcludeFromCycle = true;
        client.PrivacyMask = true;
        viewModel.SaveTo(settings);

        var saved = Assert.Single(settings.Clients);
        Assert.Equal("主账号", saved.Alias);
        Assert.Equal("Ctrl+Shift+1", saved.HotKey);
        Assert.True(saved.ExcludeFromCycle);
        Assert.True(saved.PrivacyMask);
        Assert.Equal("主账号", client.DisplayName);
        Assert.True(changes >= 4);
    }

    [Fact]
    public void Offline_client_settings_are_deleted()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - Offline", Alias = "Backup" });
        var viewModel = new ClientsViewModel(settings);
        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot([], null, null, 0, 0));

        viewModel.DeleteSelectedSettingsCommand.Execute(null);

        Assert.Empty(viewModel.Clients);
        Assert.Empty(settings.Clients);
    }

    [Fact]
    public void Online_client_settings_are_cleared_but_client_stays_online()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings
        {
            OriginalTitle = "EVE - Main",
            Alias = "Main",
            HotKey = "Alt+1",
            HighlightColor = "#112233",
            PrivacyMask = true,
            ExcludeFromCycle = true,
            DisablePreview = true,
            Placement = new PreviewPlacement(10, 20, 300, 200),
            ClientWindowBounds = new PixelRectangle(1, 2, 300, 200)
        });
        var viewModel = new ClientsViewModel(settings);
        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot(
            [new PreviewClientStatus(
                new ClientIdentity(42, 1234, DateTime.UnixEpoch),
                "EVE - Main",
                "EVE - Main",
                false,
                true,
                false)],
            null,
            null,
            1,
            0));

        viewModel.DeleteSelectedSettingsCommand.Execute(null);

        var client = Assert.Single(viewModel.Clients);
        var saved = Assert.Single(settings.Clients);
        Assert.True(client.IsOnline);
        Assert.Equal(string.Empty, client.Alias);
        Assert.Equal(string.Empty, client.HotKey);
        Assert.Equal(string.Empty, client.HighlightColor);
        Assert.False(client.PrivacyMask);
        Assert.False(client.ExcludeFromCycle);
        Assert.False(client.DisablePreview);
        Assert.Equal(string.Empty, saved.Alias);
        Assert.Null(saved.Placement);
        Assert.Null(saved.ClientWindowBounds);
    }

    [Fact]
    public void Offline_configuration_is_kept_and_marked_after_restart()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - Offline", Alias = "备用" });

        var viewModel = new ClientsViewModel(settings);
        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot([], null, null, 0, 0));

        var client = Assert.Single(viewModel.Clients);
        Assert.False(client.IsOnline);
        Assert.Equal("备用", client.DisplayName);
        Assert.Equal("EVE - Offline", client.OriginalTitle);
    }

    [Fact]
    public void Title_change_renames_existing_configuration_instead_of_adding_an_orphan()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings
        {
            OriginalTitle = "EVE - Old",
            Alias = "主账号",
            HotKey = "Ctrl+Shift+2"
        });
        var viewModel = new ClientsViewModel(settings);

        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot(
            [new PreviewClientStatus(
                new ClientIdentity(42, 1234, DateTime.UnixEpoch),
                "主账号",
                "EVE - New",
                false,
                true,
                false,
                "EVE - Old")],
            null,
            null,
            1,
            0));

        var client = Assert.Single(viewModel.Clients);
        Assert.Equal("EVE - New", client.OriginalTitle);
        Assert.Equal("主账号", client.DisplayName);
        Assert.Equal("Ctrl+Shift+2", client.HotKey);
    }

    [Fact]
    public async Task Layout_commands_delegate_to_runtime_and_report_result()
    {
        var viewModel = new ClientsViewModel(new AppSettings());
        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot(
            [new PreviewClientStatus(
                new ClientIdentity(42, 1234, DateTime.UnixEpoch),
                "EVE - Main",
                "EVE - Main",
                false,
                true,
                false)],
            null,
            null,
            1,
            0));
        var controller = new FakeLayoutController();
        viewModel.ConfigureLayoutController(controller);

        await viewModel.SaveSelectedLayoutCommand.ExecuteAsync(null);
        Assert.Equal(1, controller.SaveCalls);
        Assert.Contains("已保存", viewModel.LayoutStatusText);

        await viewModel.ApplySelectedLayoutCommand.ExecuteAsync(null);
        Assert.Equal(1, controller.ApplyCalls);
        Assert.Contains("已应用", viewModel.LayoutStatusText);
    }

    [Fact]
    public async Task Changing_selection_while_layout_save_is_pending_updates_the_original_client()
    {
        var viewModel = new ClientsViewModel(new AppSettings());
        var firstIdentity = new ClientIdentity(42, 1234, DateTime.UnixEpoch);
        var secondIdentity = new ClientIdentity(43, 5678, DateTime.UnixEpoch);
        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot(
            [
                new PreviewClientStatus(firstIdentity, "EVE - First", "EVE - First", false, true, false),
                new PreviewClientStatus(secondIdentity, "EVE - Second", "EVE - Second", false, true, false)
            ],
            null,
            null,
            2,
            0));
        var first = viewModel.Clients.Single(client => client.OriginalTitle == "EVE - First");
        var second = viewModel.Clients.Single(client => client.OriginalTitle == "EVE - Second");
        viewModel.SelectedClient = first;
        var pending = new TaskCompletionSource<PixelRectangle?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new FakeLayoutController { PendingSave = pending };
        viewModel.ConfigureLayoutController(controller);

        var saveTask = viewModel.SaveSelectedLayoutCommand.ExecuteAsync(null);
        viewModel.SelectedClient = second;
        pending.SetResult(new PixelRectangle(10, 20, 300, 200));
        await saveTask;

        Assert.Contains("(10, 20)", first.ClientWindowLayoutText);
        Assert.DoesNotContain("(10, 20)", second.ClientWindowLayoutText);
    }

    [Fact]
    public async Task Consecutive_single_layout_saves_refresh_coordinates_and_pixel_size()
    {
        var viewModel = new ClientsViewModel(new AppSettings());
        viewModel.ApplySnapshot(new PreviewRuntimeSnapshot(
            [new PreviewClientStatus(
                new ClientIdentity(42, 1234, DateTime.UnixEpoch),
                "EVE - Main",
                "EVE - Main",
                false,
                true,
                false)],
            null,
            null,
            1,
            0));
        var controller = new FakeLayoutController
        {
            NextSavedBounds = new PixelRectangle(10, 20, 300, 200)
        };
        viewModel.ConfigureLayoutController(controller);
        var client = Assert.Single(viewModel.Clients);

        await viewModel.SaveSelectedLayoutCommand.ExecuteAsync(null);
        Assert.Contains("(10, 20)", client.ClientWindowLayoutText);
        Assert.Contains("300 × 200", client.ClientWindowLayoutText);

        controller.NextSavedBounds = new PixelRectangle(40, 50, 400, 250);
        await viewModel.SaveSelectedLayoutCommand.ExecuteAsync(null);
        Assert.Contains("(40, 50)", client.ClientWindowLayoutText);
        Assert.Contains("400 × 250", client.ClientWindowLayoutText);
        Assert.DoesNotContain("(10, 20)", client.ClientWindowLayoutText);
        Assert.Equal(2, controller.SaveCalls);
    }

    [Fact]
    public void Online_placement_summary_converts_saved_dip_values_to_physical_pixels()
    {
        var placement = new PreviewPlacement(100, 120, 160, 90);
        var client = new ClientConfigViewModel(new ClientSettings
        {
            OriginalTitle = "EVE - Main",
            Placement = placement
        });
        Assert.DoesNotContain("DIP", client.PlacementText, StringComparison.Ordinal);

        client.ApplyLiveStatus(new PreviewClientStatus(
            new ClientIdentity(42, 1234, DateTime.UnixEpoch),
            "EVE - Main",
            "EVE - Main",
            false,
            true,
            false,
            Placement: placement,
            DpiScaleX: 2,
            DpiScaleY: 2,
            HasSavedPlacement: true));

        Assert.Equal("位置 200 × 240，尺寸 320 × 180 像素", client.PlacementText);
    }

    [Fact]
    public void Online_placement_summary_marks_current_layout_as_unsaved_until_it_is_persisted()
    {
        var placement = new PreviewPlacement(100, 120, 160, 90);
        var status = new PreviewClientStatus(
            new ClientIdentity(42, 1234, DateTime.UnixEpoch),
            "EVE - Main",
            "EVE - Main",
            false,
            true,
            false,
            Placement: placement,
            DpiScaleX: 2,
            DpiScaleY: 2);

        var client = new ClientConfigViewModel(new ClientSettings { OriginalTitle = "EVE - Main" });
        client.ApplyLiveStatus(status);

        Assert.Equal("当前位置 200 × 240，尺寸 320 × 180 像素（未保存）", client.PlacementText);

        client.ClearSettings();
        client.ApplyLiveStatus(status);
        Assert.DoesNotContain("已保存", client.PlacementText, StringComparison.Ordinal);
    }

    private sealed class FakeLayoutController : IClientWindowLayoutController
    {
        public int SaveCalls { get; private set; }
        public int ApplyCalls { get; private set; }
        public PixelRectangle NextSavedBounds { get; set; } = new(10, 20, 1280, 720);
        public TaskCompletionSource<PixelRectangle?>? PendingSave { get; set; }

        public Task<PixelRectangle?> SaveClientWindowLayoutAsync(
            string originalWindowTitle,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return PendingSave?.Task ?? Task.FromResult<PixelRectangle?>(NextSavedBounds);
        }

        public Task<ClientWindowLayoutBatchResult> SaveAllClientWindowLayoutsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ClientWindowLayoutBatchResult(
                new Dictionary<string, PixelRectangle>(),
                0));

        public Task<bool> ApplyClientWindowLayoutAsync(
            string originalWindowTitle,
            CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            return Task.FromResult(true);
        }
    }
}
