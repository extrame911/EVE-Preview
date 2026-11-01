using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvePreview.Core;

namespace EvePreview.App.ViewModels;

public sealed partial class CycleGroupsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly List<string> _onlineTitles = [];
    private readonly ObservableCollection<CycleGroupClientViewModel> _candidates = [];
    private bool _rebuildingCandidates;
    private string _onlineSignature = string.Empty;

    [ObservableProperty]
    private CycleGroupViewModel? _selectedGroup;

    [ObservableProperty]
    private CycleGroupClientViewModel? _selectedCandidate;

    [ObservableProperty]
    private bool _isCandidatePickerOpen;

    public CycleGroupsViewModel() : this(new AppSettings())
    {
    }

    public CycleGroupsViewModel(AppSettings settings)
    {
        _settings = settings;
        Candidates = new ReadOnlyObservableCollection<CycleGroupClientViewModel>(_candidates);
        LoadFrom(settings);
    }

    public ObservableCollection<CycleGroupViewModel> Groups { get; } = [];

    public ReadOnlyObservableCollection<CycleGroupClientViewModel> Candidates { get; }

    public bool CanRemoveSelectedGroup => Groups.Count > 1;

    public event EventHandler? OrderChanged;

    /// <summary>Raised when groups are added, removed, or reloaded so hosts can rebuild subscriptions.</summary>
    public event EventHandler? GroupsChanged;

    partial void OnSelectedGroupChanged(CycleGroupViewModel? value)
    {
        IsCandidatePickerOpen = false;
        OnPropertyChanged(nameof(CanRemoveSelectedGroup));
        RebuildCandidates();
    }

    public void LoadFrom(AppSettings settings)
    {
        foreach (var group in Groups)
        {
            group.OrderChanged -= OnGroupOrderChanged;
        }

        Groups.Clear();
        var source = settings.CycleGroups;
        if (source.Count == 0)
        {
            source.Add(new CycleGroupSettings());
        }

        for (var index = 0; index < source.Count; index++)
        {
            var group = new CycleGroupViewModel(index + 1, source[index]);
            group.OrderChanged += OnGroupOrderChanged;
            Groups.Add(group);
        }

        SelectedGroup = Groups[0];
        OnPropertyChanged(nameof(CanRemoveSelectedGroup));
        GroupsChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<string> GetOrderedTitles(int groupIndex) =>
        groupIndex >= 0 && groupIndex < Groups.Count
            ? Groups[groupIndex].Clients.Select(client => client.OriginalTitle).ToArray()
            : [];

    public void SyncOnlineClients(IEnumerable<string> onlineTitles)
    {
        var normalized = onlineTitles
            .Select(title => title.Trim())
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        // Snapshots are published on every poll; rebuilding an unchanged candidate list would reset the
        // picker's selection, so identical online sets are ignored.
        var signature = string.Join('\u001F', normalized);
        if (string.Equals(signature, _onlineSignature, StringComparison.Ordinal))
        {
            return;
        }

        _onlineSignature = signature;
        _onlineTitles.Clear();
        _onlineTitles.AddRange(normalized);
        RebuildCandidates();
    }

    public void RefreshCandidates() => RebuildCandidates();

    public void RefreshLocalizedText()
    {
        foreach (var group in Groups)
        {
            group.RefreshLocalizedText();
        }
    }

    public void RemoveClientFromAllGroups(string title)
    {
        var removed = false;
        foreach (var group in Groups)
        {
            var client = group.Clients.FirstOrDefault(item =>
                string.Equals(item.OriginalTitle, title, StringComparison.OrdinalIgnoreCase));
            if (client is not null)
            {
                group.Clients.Remove(client);
                removed = true;
            }
        }

        RebuildCandidates();
        if (removed)
        {
            OrderChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RebuildCandidates()
    {
        if (_rebuildingCandidates)
        {
            return;
        }

        _rebuildingCandidates = true;
        try
        {
            RebuildCandidatesCore();
        }
        finally
        {
            _rebuildingCandidates = false;
        }
    }

    private void RebuildCandidatesCore()
    {
        var previousTitle = SelectedCandidate?.OriginalTitle;
        _candidates.Clear();
        if (SelectedGroup is null)
        {
            SelectedCandidate = null;
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var title in _onlineTitles)
        {
            if (!seen.Add(title))
            {
                continue;
            }

            if (SelectedGroup.Clients.Any(client =>
                    string.Equals(client.OriginalTitle, title, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var settings = _settings.Clients.FirstOrDefault(client =>
                string.Equals(client.OriginalTitle, title, StringComparison.OrdinalIgnoreCase));
            if (settings?.ExcludeFromCycle == true)
            {
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(settings?.Alias) ? title : settings.Alias;
            _candidates.Add(new CycleGroupClientViewModel(displayName, title));
        }

        // Keep the picker's current choice when it is still an available candidate.
        SelectedCandidate = _candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.OriginalTitle, previousTitle, StringComparison.OrdinalIgnoreCase))
            ?? _candidates.FirstOrDefault();
    }

    [RelayCommand]
    private void OpenCandidatePicker()
    {
        RebuildCandidates();
        SelectedCandidate = Candidates.FirstOrDefault();
        IsCandidatePickerOpen = true;
    }

    [RelayCommand]
    private void CloseCandidatePicker() => IsCandidatePickerOpen = false;

    [RelayCommand]
    private void AddCandidate()
    {
        if (SelectedGroup is null || SelectedCandidate is null)
        {
            return;
        }

        if (SelectedGroup.Clients.Any(client =>
                string.Equals(client.OriginalTitle, SelectedCandidate.OriginalTitle, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedGroup.Clients.Add(new CycleGroupClientViewModel(
            SelectedCandidate.DisplayName,
            SelectedCandidate.OriginalTitle));
        RebuildCandidates();
        IsCandidatePickerOpen = false;
        OrderChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnGroupOrderChanged(object? sender, EventArgs e)
    {
        RebuildCandidates();
        OrderChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectGroup(int groupNumber)
    {
        var group = Groups.FirstOrDefault(item => item.GroupNumber == groupNumber);
        if (group is not null)
        {
            SelectedGroup = group;
        }
    }

    [RelayCommand]
    private void AddGroup()
    {
        var group = new CycleGroupViewModel(Groups.Count + 1);
        group.OrderChanged += OnGroupOrderChanged;
        Groups.Add(group);
        SelectedGroup = group;
        NotifyGroupsChanged();
    }

    [RelayCommand]
    private void RemoveGroup(CycleGroupViewModel? group)
    {
        group ??= SelectedGroup;
        if (group is null || Groups.Count <= AppSettings.MinimumCycleGroups)
        {
            return;
        }

        var index = Groups.IndexOf(group);
        if (index < 0)
        {
            return;
        }

        group.OrderChanged -= OnGroupOrderChanged;
        Groups.RemoveAt(index);
        RenumberGroups();
        SelectedGroup = Groups[Math.Min(index, Groups.Count - 1)];
        NotifyGroupsChanged();
    }

    private void RenumberGroups()
    {
        for (var index = 0; index < Groups.Count; index++)
        {
            Groups[index].GroupNumber = index + 1;
        }
    }

    private void NotifyGroupsChanged()
    {
        OnPropertyChanged(nameof(CanRemoveSelectedGroup));
        GroupsChanged?.Invoke(this, EventArgs.Empty);
        OrderChanged?.Invoke(this, EventArgs.Empty);
    }
}
