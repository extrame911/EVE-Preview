using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvePreview.App.Services;
using EvePreview.Core;

namespace EvePreview.App.ViewModels;

public sealed partial class ClientsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly ObservableCollection<ClientConfigViewModel> _clients = [];
    private IClientWindowLayoutController? _layoutController;
    private bool _loading;

    [ObservableProperty]
    private ClientConfigViewModel? _selectedClient;

    [ObservableProperty]
    private string _layoutStatusText = string.Empty;

    public ClientsViewModel(AppSettings settings)
    {
        _settings = settings;
        Clients = new ReadOnlyObservableCollection<ClientConfigViewModel>(_clients);
        foreach (var client in settings.Clients.Where(client =>
                     !ClientWindowTitle.IsLoginWindow(client.OriginalTitle)))
        {
            Add(new ClientConfigViewModel(client));
        }

        SelectedClient = _clients.FirstOrDefault();
    }

    public ReadOnlyObservableCollection<ClientConfigViewModel> Clients { get; }

    public event EventHandler? Changed;

    public event Action<ClientConfigViewModel>? HotkeyChanged;

    public event Action<string>? ClientSettingsCleared;

    public void ConfigureLayoutController(IClientWindowLayoutController controller) =>
        _layoutController = controller;

    public void ApplySnapshot(PreviewRuntimeSnapshot snapshot)
    {
        _loading = true;
        try
        {
            foreach (var status in snapshot.Clients)
            {
                if (!string.IsNullOrWhiteSpace(status.PreviousWindowTitle))
                {
                    RenameClient(status.PreviousWindowTitle, status.OriginalWindowTitle);
                }

                var config = _clients.FirstOrDefault(client =>
                    string.Equals(client.OriginalTitle, status.OriginalWindowTitle, StringComparison.OrdinalIgnoreCase));
                if (config is null)
                {
                    config = new ClientConfigViewModel(new ClientSettings
                    {
                        OriginalTitle = status.OriginalWindowTitle
                    });
                    Add(config);
                }

                config.ApplyLiveStatus(status);
            }

            foreach (var config in _clients)
            {
                if (snapshot.Clients.All(status =>
                        !string.Equals(status.OriginalWindowTitle, config.OriginalTitle, StringComparison.OrdinalIgnoreCase)))
                {
                    config.ApplyLiveStatus(null);
                }
            }

            SelectedClient ??= _clients.FirstOrDefault(client => client.IsOnline) ?? _clients.FirstOrDefault();
        }
        finally
        {
            _loading = false;
        }
    }

    public void LoadFrom(AppSettings settings)
    {
        var selectedTitle = SelectedClient?.OriginalTitle;
        _loading = true;
        try
        {
            foreach (var client in _clients)
            {
                client.PropertyChanged -= OnClientPropertyChanged;
            }

            _clients.Clear();
            foreach (var client in settings.Clients.Where(client =>
                         !ClientWindowTitle.IsLoginWindow(client.OriginalTitle)))
            {
                Add(new ClientConfigViewModel(client));
            }

            SelectedClient = _clients.FirstOrDefault(client =>
                                 string.Equals(client.OriginalTitle, selectedTitle, StringComparison.OrdinalIgnoreCase))
                             ?? _clients.FirstOrDefault();
        }
        finally
        {
            _loading = false;
        }
    }

    public void SaveTo(AppSettings settings)
    {
        foreach (var viewModel in _clients)
        {
            var target = settings.Clients.FirstOrDefault(client =>
                string.Equals(client.OriginalTitle, viewModel.OriginalTitle, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                target = new ClientSettings { OriginalTitle = viewModel.OriginalTitle };
                settings.Clients.Add(target);
            }

            viewModel.ApplyTo(target);
        }
    }

    public void RenameClient(string previousTitle, string currentTitle)
    {
        if (string.IsNullOrWhiteSpace(currentTitle) ||
            string.Equals(previousTitle, currentTitle, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var client = _clients.FirstOrDefault(item =>
            string.Equals(item.OriginalTitle, previousTitle, StringComparison.OrdinalIgnoreCase));
        client?.RenameOriginalTitle(currentTitle);
    }

    [RelayCommand]
    private void DeleteSelectedSettings()
    {
        if (SelectedClient is null)
        {
            return;
        }

        var client = SelectedClient;
        var target = _settings.Clients.FirstOrDefault(item =>
            string.Equals(item.OriginalTitle, client.OriginalTitle, StringComparison.OrdinalIgnoreCase));
        if (client.IsOnline)
        {
            if (target is not null)
            {
                ClearSettings(target);
            }

            client.ClearSettings();
            LayoutStatusText = "已清空此客户端的所有配置。";
            ClientSettingsCleared?.Invoke(client.OriginalTitle);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (target is not null)
        {
            _settings.Clients.Remove(target);
        }

        client.PropertyChanged -= OnClientPropertyChanged;
        _clients.Remove(client);
        SelectedClient = _clients.FirstOrDefault(item => item.IsOnline) ?? _clients.FirstOrDefault();
        LayoutStatusText = "已删除此客户端的配置。";
        ClientSettingsCleared?.Invoke(client.OriginalTitle);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void ClearSettings(ClientSettings target)
    {
        target.Alias = string.Empty;
        target.HotKey = string.Empty;
        target.Width = null;
        target.Height = null;
        target.ZoomAnchor = null;
        target.HighlightColor = string.Empty;
        target.PrivacyMask = false;
        target.ExcludeFromCycle = false;
        target.DisablePreview = false;
        target.Placement = null;
        target.ClientWindowBounds = null;
    }

    private void Add(ClientConfigViewModel client)
    {
        _clients.Add(client);
        client.PropertyChanged += OnClientPropertyChanged;
        if (SelectedClient is null)
        {
            SelectedClient = client;
        }
    }

    private void OnClientPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (e.PropertyName is nameof(ClientConfigViewModel.IsOnline) or
            nameof(ClientConfigViewModel.LiveWindowTitle) or
            nameof(ClientConfigViewModel.OnlineStatusText) or
            nameof(ClientConfigViewModel.OnlineStatusDetail) or
            nameof(ClientConfigViewModel.PlacementText) or
            nameof(ClientConfigViewModel.ClientWindowLayoutText) or
            nameof(ClientConfigViewModel.OriginalTitle) or
            nameof(ClientConfigViewModel.DisplayName) or
            nameof(LayoutStatusText))
        {
            return;
        }

        if (e.PropertyName == nameof(ClientConfigViewModel.HotKey) && sender is ClientConfigViewModel client)
        {
            HotkeyChanged?.Invoke(client);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
