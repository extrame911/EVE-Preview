using EvePreview.App.ViewModels;

namespace EvePreview.App.Tests;

public sealed class CycleGroupsViewModelTests
{
    [Fact]
    public void Five_groups_are_available_and_group_one_is_selected()
    {
        var viewModel = new CycleGroupsViewModel();

        Assert.Equal(5, viewModel.Groups.Count);
        Assert.Equal(1, viewModel.SelectedGroup!.GroupNumber);
        Assert.Empty(viewModel.SelectedGroup.Clients);
    }

    [Fact]
    public void Select_group_command_switches_the_visible_group()
    {
        var viewModel = new CycleGroupsViewModel();

        viewModel.SelectGroupCommand.Execute(4);

        Assert.Equal(4, viewModel.SelectedGroup!.GroupNumber);
    }

    [Fact]
    public void Move_down_command_changes_client_order()
    {
        var viewModel = new CycleGroupsViewModel();
        var first = new CycleGroupClientViewModel("客户端 A");
        var second = new CycleGroupClientViewModel("客户端 B");
        viewModel.SelectedGroup!.Clients.Add(first);
        viewModel.SelectedGroup.Clients.Add(second);

        viewModel.SelectedGroup.MoveDownCommand.Execute(first);

        Assert.Equal(new[] { second, first }, viewModel.SelectedGroup.Clients);
    }

    [Fact]
    public void Moving_boundary_client_does_not_change_order()
    {
        var viewModel = new CycleGroupsViewModel();
        var first = new CycleGroupClientViewModel("客户端 A");
        viewModel.SelectedGroup!.Clients.Add(first);

        viewModel.SelectedGroup.MoveUpCommand.Execute(first);
        viewModel.SelectedGroup.MoveDownCommand.Execute(first);

        Assert.Equal(new[] { first }, viewModel.SelectedGroup.Clients);
    }

    [Fact]
    public void Move_buttons_are_hidden_at_the_boundaries()
    {
        var viewModel = new CycleGroupsViewModel();
        var first = new CycleGroupClientViewModel("客户端 A");
        var second = new CycleGroupClientViewModel("客户端 B");
        var third = new CycleGroupClientViewModel("客户端 C");
        viewModel.SelectedGroup!.Clients.Add(first);
        viewModel.SelectedGroup.Clients.Add(second);
        viewModel.SelectedGroup.Clients.Add(third);

        Assert.False(first.CanMoveUp);
        Assert.True(first.CanMoveDown);
        Assert.True(second.CanMoveUp);
        Assert.True(second.CanMoveDown);
        Assert.True(third.CanMoveUp);
        Assert.False(third.CanMoveDown);

        viewModel.SelectedGroup.RemoveClientCommand.Execute(second);

        Assert.True(third.CanMoveUp);
        Assert.False(third.CanMoveDown);
    }

    [Fact]
    public void Groups_can_be_added_and_removed_dynamically()
    {
        var viewModel = new CycleGroupsViewModel();

        viewModel.AddGroupCommand.Execute(null);

        Assert.Equal(6, viewModel.Groups.Count);
        Assert.Equal(6, viewModel.SelectedGroup!.GroupNumber);

        var removed = viewModel.Groups[2];
        viewModel.RemoveGroupCommand.Execute(removed);

        Assert.Equal(5, viewModel.Groups.Count);
        Assert.DoesNotContain(removed, viewModel.Groups);
        Assert.Equal([1, 2, 3, 4, 5], viewModel.Groups.Select(group => group.GroupNumber));
    }

    [Fact]
    public void Removing_the_selected_group_selects_the_adjacent_group()
    {
        var viewModel = new CycleGroupsViewModel();
        viewModel.SelectedGroup = viewModel.Groups[2];

        viewModel.RemoveGroupCommand.Execute(viewModel.SelectedGroup);

        Assert.Equal(4, viewModel.Groups.Count);
        Assert.Equal(3, viewModel.SelectedGroup!.GroupNumber);

        viewModel.SelectedGroup = viewModel.Groups[^1];
        viewModel.RemoveGroupCommand.Execute(viewModel.SelectedGroup);

        Assert.Equal(3, viewModel.Groups.Count);
        Assert.Equal(3, viewModel.SelectedGroup!.GroupNumber);
    }

    [Fact]
    public void Group_row_projection_tracks_client_count_and_renumbering()
    {
        var viewModel = new CycleGroupsViewModel();
        var group = viewModel.SelectedGroup!;

        Assert.Equal(1, group.GroupNumber);
        Assert.Equal("00", group.ClientCountText);

        group.Clients.Add(new CycleGroupClientViewModel("EVE - A"));
        viewModel.RemoveGroupCommand.Execute(viewModel.Groups[0]);

        Assert.Equal("01", group.ClientCountText);
        Assert.Equal(1, viewModel.Groups[0].GroupNumber);
        Assert.Equal(4, viewModel.Groups.Count);
    }

    [Fact]
    public void Last_group_cannot_be_removed()
    {
        var viewModel = new CycleGroupsViewModel();
        viewModel.RemoveGroupCommand.Execute(viewModel.Groups[4]);
        viewModel.RemoveGroupCommand.Execute(viewModel.Groups[3]);
        viewModel.RemoveGroupCommand.Execute(viewModel.Groups[2]);
        viewModel.RemoveGroupCommand.Execute(viewModel.Groups[1]);

        viewModel.RemoveGroupCommand.Execute(viewModel.Groups[0]);

        Assert.Single(viewModel.Groups);
        Assert.False(viewModel.CanRemoveSelectedGroup);
    }

    [Fact]
    public void LoadFrom_rebuilds_the_group_collection_to_the_saved_count()
    {
        var settings = new EvePreview.Core.AppSettings();
        settings.CycleGroups.RemoveRange(1, settings.CycleGroups.Count - 1);
        settings.CycleGroups.Add(new EvePreview.Core.CycleGroupSettings { ForwardHotKey = "Ctrl+F7" });

        var viewModel = new CycleGroupsViewModel(settings);

        Assert.Equal(2, viewModel.Groups.Count);
        Assert.Equal(1, viewModel.SelectedGroup!.GroupNumber);
        Assert.Equal("Ctrl+F7", viewModel.Groups[1].ForwardHotkey);
    }

    [Fact]
    public void New_groups_start_empty_and_online_clients_become_candidates()
    {
        var viewModel = new CycleGroupsViewModel();
        viewModel.SyncOnlineClients(["EVE - A"]);

        viewModel.AddGroupCommand.Execute(null);

        Assert.Empty(viewModel.SelectedGroup!.Clients);
        Assert.Equal(["EVE - A"], viewModel.Candidates.Select(candidate => candidate.OriginalTitle));
    }

    [Fact]
    public void Online_clients_are_candidates_and_can_be_added_explicitly()
    {
        var viewModel = new CycleGroupsViewModel();

        viewModel.SyncOnlineClients(["EVE - A", "EVE - B"]);
        viewModel.SyncOnlineClients(["eve - a", "EVE - C"]);

        Assert.Equal(["eve - a", "EVE - C"], viewModel.Candidates.Select(candidate => candidate.OriginalTitle));
        Assert.Empty(viewModel.SelectedGroup!.Clients);

        viewModel.SelectedCandidate = viewModel.Candidates[0];
        viewModel.AddCandidateCommand.Execute(null);

        Assert.Single(viewModel.SelectedGroup.Clients);
        Assert.Equal("eve - a", viewModel.SelectedGroup.Clients[0].OriginalTitle);
        Assert.Equal(["EVE - C"], viewModel.Candidates.Select(candidate => candidate.OriginalTitle));
    }

    [Fact]
    public void Duplicate_online_titles_do_not_duplicate_candidates()
    {
        var viewModel = new CycleGroupsViewModel();

        viewModel.SyncOnlineClients(["EVE - A", " EVE - A ", "eve - a"]);

        Assert.Single(viewModel.Candidates);
        Assert.Equal("EVE - A", viewModel.Candidates[0].OriginalTitle);
    }

    [Fact]
    public void Candidate_picker_opens_even_when_there_are_no_candidates()
    {
        var viewModel = new CycleGroupsViewModel();

        viewModel.OpenCandidatePickerCommand.Execute(null);

        Assert.True(viewModel.IsCandidatePickerOpen);
        Assert.Empty(viewModel.Candidates);
    }

    [Fact]
    public void Candidate_picker_opens_and_closes()
    {
        var viewModel = new CycleGroupsViewModel();
        viewModel.SyncOnlineClients(["EVE - A"]);

        viewModel.OpenCandidatePickerCommand.Execute(null);

        Assert.True(viewModel.IsCandidatePickerOpen);
        Assert.Single(viewModel.Candidates);

        viewModel.CloseCandidatePickerCommand.Execute(null);

        Assert.False(viewModel.IsCandidatePickerOpen);
    }

    [Fact]
    public void Excluded_clients_are_not_candidates()
    {
        var settings = new EvePreview.Core.AppSettings();
        settings.Clients.Add(new EvePreview.Core.ClientSettings
        {
            OriginalTitle = "EVE - Excluded",
            ExcludeFromCycle = true
        });
        var viewModel = new CycleGroupsViewModel(settings);

        viewModel.SyncOnlineClients(["EVE - Excluded", "EVE - Included"]);

        Assert.Equal(["EVE - Included"], viewModel.Candidates.Select(candidate => candidate.OriginalTitle));
    }

    [Fact]
    public void Removing_a_group_client_updates_candidates()
    {
        var viewModel = new CycleGroupsViewModel();
        viewModel.SyncOnlineClients(["EVE - A"]);
        viewModel.SelectedCandidate = viewModel.Candidates[0];
        viewModel.AddCandidateCommand.Execute(null);

        var group = viewModel.SelectedGroup!;
        group.RemoveClientCommand.Execute(group.Clients[0]);

        Assert.Empty(group.Clients);
        Assert.Equal(["EVE - A"], viewModel.Candidates.Select(candidate => candidate.OriginalTitle));
    }

    [Fact]
    public void Clearing_client_settings_removes_the_client_from_all_groups()
    {
        var viewModel = new CycleGroupsViewModel();
        foreach (var group in viewModel.Groups)
        {
            group.Clients.Add(new CycleGroupClientViewModel("EVE - A", "EVE - A"));
        }

        viewModel.RemoveClientFromAllGroups("EVE - A");

        Assert.All(viewModel.Groups, group => Assert.Empty(group.Clients));
    }

    [Fact]
    public void Unchanged_online_snapshots_keep_the_selected_candidate()
    {
        var viewModel = new CycleGroupsViewModel();
        viewModel.SyncOnlineClients(["EVE - A", "EVE - B"]);
        viewModel.OpenCandidatePickerCommand.Execute(null);
        viewModel.SelectedCandidate = viewModel.Candidates[1];

        viewModel.SyncOnlineClients(["EVE - A", "EVE - B"]);

        Assert.Equal("EVE - B", viewModel.SelectedCandidate?.OriginalTitle);

        viewModel.AddCandidateCommand.Execute(null);

        Assert.Equal(["EVE - B"], viewModel.SelectedGroup!.Clients.Select(client => client.OriginalTitle));
    }

    [Fact]
    public void Candidate_rebuild_keeps_the_selected_candidate_when_it_is_still_available()
    {
        var viewModel = new CycleGroupsViewModel();
        viewModel.SyncOnlineClients(["EVE - A", "EVE - B"]);
        viewModel.SelectedCandidate = viewModel.Candidates[1];

        viewModel.RefreshCandidates();

        Assert.Equal("EVE - B", viewModel.SelectedCandidate?.OriginalTitle);
    }

    [Fact]
    public void Candidate_selection_falls_back_to_the_first_entry_when_the_choice_goes_offline()
    {
        var viewModel = new CycleGroupsViewModel();
        viewModel.SyncOnlineClients(["EVE - A", "EVE - B"]);
        viewModel.SelectedCandidate = viewModel.Candidates[1];

        viewModel.SyncOnlineClients(["EVE - A"]);

        Assert.Equal("EVE - A", viewModel.SelectedCandidate?.OriginalTitle);
    }
}
