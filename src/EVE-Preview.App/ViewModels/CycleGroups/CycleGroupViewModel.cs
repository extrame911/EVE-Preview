using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvePreview.App.Services;
using EvePreview.Core;

namespace EvePreview.App.ViewModels;

public sealed partial class CycleGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private int _groupNumber;

    [ObservableProperty]
    private string _forwardHotkey = string.Empty;

    [ObservableProperty]
    private string _backwardHotkey = string.Empty;

    public CycleGroupViewModel(int groupNumber)
    {
        _groupNumber = groupNumber;
        Clients.CollectionChanged += (_, _) =>
        {
            UpdateMoveState();
            OnPropertyChanged(nameof(ClientCountText));
        };
    }

    public CycleGroupViewModel(int groupNumber, CycleGroupSettings settings) : this(groupNumber)
    {
        LoadFrom(settings);
    }

    public ObservableCollection<CycleGroupClientViewModel> Clients { get; } = [];

    public string TitleText => LocalizationManager.IsEnglish
        ? $"Cycle List {GroupNumber} settings"
        : $"循环组 {GroupNumber} 设置";

    public string DisplayText => LocalizationManager.IsEnglish
        ? $"Cycle List {GroupNumber:00}"
        : $"循环组 {GroupNumber:00}";

    public string ClientCountText => Clients.Count.ToString("00");

    partial void OnGroupNumberChanged(int value) => RefreshLocalizedText();

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(DisplayText));
    }

    public event EventHandler? OrderChanged;

    public void LoadFrom(CycleGroupSettings settings)
    {
        ForwardHotkey = settings.ForwardHotKey;
        BackwardHotkey = settings.BackwardHotKey;
        Clients.Clear();
        foreach (var title in settings.OrderedClientTitles)
        {
            Clients.Add(new CycleGroupClientViewModel(title, title));
        }

        UpdateMoveState();
    }

    private void UpdateMoveState()
    {
        for (var index = 0; index < Clients.Count; index++)
        {
            Clients[index].CanMoveUp = index > 0;
            Clients[index].CanMoveDown = index < Clients.Count - 1;
        }
    }

    [RelayCommand]
    private void MoveUp(CycleGroupClientViewModel? client)
    {
        var index = client is null ? -1 : Clients.IndexOf(client);
        if (index > 0)
        {
            Clients.Move(index, index - 1);
            OrderChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void MoveDown(CycleGroupClientViewModel? client)
    {
        var index = client is null ? -1 : Clients.IndexOf(client);
        if (index >= 0 && index < Clients.Count - 1)
        {
            Clients.Move(index, index + 1);
            OrderChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void RemoveClient(CycleGroupClientViewModel? client)
    {
        if (client is not null && Clients.Remove(client))
        {
            OrderChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
