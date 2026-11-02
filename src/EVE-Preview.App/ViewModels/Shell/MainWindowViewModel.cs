using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvePreview.App.Services;
using EvePreview.Core;
using Microsoft.Win32;

namespace EvePreview.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IPreviewRuntime _runtime;
    private readonly AppSettings _settings;
    private readonly ConfigurationProfileService? _profiles;
    private int _settingsApplyVersion;
    private bool _applyingViewModelState = true;
    private bool _applyingSettingsToModel;
    private bool _saveFailurePromptShown;
    private string _lastRuntimeError = string.Empty;

    [ObservableProperty]
    private AppPage _currentPage = AppPage.Overview;

    [ObservableProperty]
    private string _lastError = string.Empty;

    [ObservableProperty]
    private bool _canDiscardUnsavedChanges;

    public MainWindowViewModel()
        : this(null, null)
    {
    }

    public MainWindowViewModel(IPreviewRuntime? runtime = null, AppSettings? settings = null, ConfigurationProfileService? profiles = null)
    {
        _runtime = runtime ?? new StandalonePreviewRuntime();
        _settings = settings ?? new AppSettings();
        _profiles = profiles;
        _settings.Normalize();
        LocalizationManager.SetLanguage(_settings.General.Language);
        PreviewToolTipPalette.Apply(_settings.Overlay.TextColor);
        Overview = new OverviewViewModel();
        ThumbnailSettings = new ThumbnailSettingsViewModel(_settings);
        Clients = new ClientsViewModel(_settings);
        if (_runtime is IClientWindowLayoutController layoutController)
        {
            Clients.ConfigureLayoutController(layoutController);
        }

        CycleGroups = new CycleGroupsViewModel(_settings);
        Hotkeys = new HotkeysViewModel(_settings);
        SystemSettings = new SystemSettingsViewModel(_settings, profiles);
        SystemSettings.ReloadProfileRequested += (_, _) => ReloadProfile();
        SystemSettings.SaveProfileAsRequested += (_, _) => SaveProfileAs();
        SystemSettings.ImportLegacyRequested += (_, _) => ImportLegacyProfile();
        SystemSettings.OpenProfileFolderRequested += (_, _) => OpenProfileFolder();
        SystemSettings.SwitchProfileRequested += (_, profile) => SwitchProfile(profile);
        _applyingViewModelState = false;

        Overview.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Overview.ServiceState))
            {
                OnPropertyChanged(nameof(ServiceStatusText));
            }
        };
        SystemSettings.SettingChanged += (_, args) =>
        {
            if (args.SettingName == SystemSettingsViewModel.CompatibilityModeSettingName)
            {
                Overview.RenderModeText = SystemSettings.RenderingModeText;
            }
        };
        ThumbnailSettings.PropertyChanged += OnChildPropertyChanged;
        Clients.PropertyChanged += OnChildPropertyChanged;
        Clients.Changed += (_, _) => OnViewModelSettingChanged();
        Clients.HotkeyChanged += client => HandleHotkeyEdit(
            client,
            nameof(ClientConfigViewModel.HotKey),
            client.HotKey,
            value => client.HotKey = value);
        Clients.ClientSettingsCleared += title => CycleGroups.RemoveClientFromAllGroups(title);
        SystemSettings.PropertyChanged += OnChildPropertyChanged;
        Hotkeys.PropertyChanged += OnChildPropertyChanged;
        CycleGroups.PropertyChanged += OnChildPropertyChanged;
        CycleGroups.OrderChanged += (_, _) => OnViewModelSettingChanged();
        CycleGroups.GroupsChanged += OnCycleGroupsChanged;
        SubscribeToCycleGroupChanges();

        if (_runtime is IPreviewRuntimeStatusSource statusSource)
        {
            statusSource.SnapshotChanged += OnRuntimeSnapshotChanged;
            ApplyRuntimeSnapshot(statusSource.CurrentSnapshot);
        }

        ApplyViewModelState();
    }

    public OverviewViewModel Overview { get; }
    public ThumbnailSettingsViewModel ThumbnailSettings { get; }
    public ClientsViewModel Clients { get; }
    public CycleGroupsViewModel CycleGroups { get; }
    public HotkeysViewModel Hotkeys { get; }
    public SystemSettingsViewModel SystemSettings { get; }
    public AppSettings Settings => _settings;
    public string BuildDate => BuildInfo.BuildDate;
    public string Version => BuildInfo.Version;

    public string ServiceStatusText => string.IsNullOrWhiteSpace(LastError)
        ? Overview.ServiceStatusText
        : $"{Overview.ServiceStatusText}：{LastError}";

    public object CurrentPageViewModel => CurrentPage switch
    {
        AppPage.Overview => Overview,
        AppPage.Thumbnails => ThumbnailSettings,
        AppPage.Clients => Clients,
        AppPage.CycleGroups => CycleGroups,
        AppPage.System => SystemSettings,
        _ => throw new ArgumentOutOfRangeException()
    };

    [RelayCommand]
    private void Navigate(AppPage page) => CurrentPage = page;

    [RelayCommand]
    private Task ShowAllClientsAsync() => RunAllClientsOperationAsync(minimize: false);

    [RelayCommand]
    private Task MinimizeAllClientsAsync() => RunAllClientsOperationAsync(minimize: true);



    [RelayCommand]
    private async Task SaveAllWindowLayoutsAsync()
    {
        if (_runtime is not IClientWindowLayoutController controller)
        {
            Overview.LayoutStatusText = "预览服务尚未启动，无法保存窗口位置。";
            return;
        }

        try
        {
            Overview.LayoutStatusText = "正在保存所有客户端窗口位置…";
            var result = await controller.SaveAllClientWindowLayoutsAsync();
            foreach (var client in Clients.Clients)
            {
                if (result.SavedLayouts.TryGetValue(client.OriginalTitle, out var bounds))
                {
                    client.SetClientWindowBounds(bounds);
                }
            }

            Overview.LayoutStatusText = result.SavedCount == 0
                ? $"未保存窗口位置；{result.SkippedCount} 个客户端最小化或无法读取。"
                : $"已保存 {result.SavedCount} 个客户端窗口位置。" +
                  (result.SkippedCount > 0 ? $"跳过 {result.SkippedCount} 个最小化或无法读取的客户端。" : string.Empty);
        }
        catch (Exception exception)
        {
            Overview.LayoutStatusText = $"保存窗口位置失败：{exception.Message}";
        }
    }

    private async Task RunAllClientsOperationAsync(bool minimize)
    {
        try
        {
            LastError = string.Empty;
            if (_runtime is IPreviewRuntimeController controller)
            {
                if (minimize)
                {
                    await controller.MinimizeAllAsync();
                }
                else
                {
                    await controller.RestoreAllAsync();
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task TogglePreviewAsync()
    {
        try
        {
            LastError = string.Empty;
            switch (Overview.ServiceState)
            {
                case PreviewServiceState.Stopped:
                case PreviewServiceState.Faulted:
                    Overview.ServiceState = PreviewServiceState.Starting;
                    ApplySettingsToModel();
                    if (_runtime is IPreviewRuntimeController controller)
                    {
                        await controller.ApplySettingsAsync(_settings);
                    }

                    await _runtime.StartAsync();
                    Overview.ServiceState = PreviewServiceState.Running;
                    break;
                case PreviewServiceState.Running:
                    await StopPreviewAsync();
                    break;
                case PreviewServiceState.Paused:
                    await _runtime.ResumeAsync();
                    Overview.ServiceState = PreviewServiceState.Running;
                    break;
            }
        }
        catch (Exception exception)
        {
            Overview.ServiceState = PreviewServiceState.Faulted;
            LastError = exception.Message;
        }
    }

    public async Task StopPreviewAsync()
    {
        if (Overview.ServiceState == PreviewServiceState.Stopped)
        {
            return;
        }

        try
        {
            Overview.ServiceState = PreviewServiceState.Stopping;
            await _runtime.StopAsync();
            Overview.ServiceState = PreviewServiceState.Stopped;
        }
        catch (Exception exception)
        {
            Overview.ServiceState = PreviewServiceState.Faulted;
            LastError = exception.Message;
        }
    }

    public void SetStartupDiagnostic(string message)
    {
        LastError = message;
        UpdateStatusText();
    }

    partial void OnCurrentPageChanged(AppPage value) =>
        OnPropertyChanged(nameof(CurrentPageViewModel));

    partial void OnLastErrorChanged(string value) => OnPropertyChanged(nameof(ServiceStatusText));

    private void OnChildPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (sender is CycleGroupViewModel group &&
            args.PropertyName is nameof(CycleGroupViewModel.ForwardHotkey) or nameof(CycleGroupViewModel.BackwardHotkey))
        {
            var value = args.PropertyName == nameof(CycleGroupViewModel.ForwardHotkey)
                ? group.ForwardHotkey
                : group.BackwardHotkey;
            if (HandleHotkeyEdit(group, args.PropertyName, value, newValue =>
                {
                    if (args.PropertyName == nameof(CycleGroupViewModel.ForwardHotkey))
                    {
                        group.ForwardHotkey = newValue;
                    }
                    else
                    {
                        group.BackwardHotkey = newValue;
                    }
                }))
            {
                return;
            }
        }
        else if (ReferenceEquals(sender, Hotkeys) &&
                 args.PropertyName is nameof(HotkeysViewModel.MinimizeAllHotkey) or
                     nameof(HotkeysViewModel.RefreshMinimizedHotkey) or nameof(HotkeysViewModel.ReturnToPreviousHotkey))
        {
            var value = args.PropertyName switch
            {
                nameof(HotkeysViewModel.MinimizeAllHotkey) => Hotkeys.MinimizeAllHotkey,
                nameof(HotkeysViewModel.RefreshMinimizedHotkey) => Hotkeys.RefreshMinimizedHotkey,
                _ => Hotkeys.ReturnToPreviousHotkey
            };
            if (HandleHotkeyEdit(Hotkeys, args.PropertyName, value, newValue =>
                {
                    switch (args.PropertyName)
                    {
                        case nameof(HotkeysViewModel.MinimizeAllHotkey): Hotkeys.MinimizeAllHotkey = newValue; break;
                        case nameof(HotkeysViewModel.RefreshMinimizedHotkey): Hotkeys.RefreshMinimizedHotkey = newValue; break;
                        default: Hotkeys.ReturnToPreviousHotkey = newValue; break;
                    }
                }))
            {
                return;
            }
        }

        if (args.PropertyName == nameof(MainWindowViewModel.LastError) ||
            args.PropertyName == nameof(ThumbnailSettingsViewModel.OpacityError) ||
            args.PropertyName == nameof(SystemSettingsViewModel.PollingIntervalError) ||
            (ReferenceEquals(sender, Clients) && args.PropertyName == nameof(ClientsViewModel.SelectedClient)) ||
            (ReferenceEquals(sender, Clients) && args.PropertyName == nameof(ClientsViewModel.LayoutStatusText)) ||
            (ReferenceEquals(sender, Hotkeys) &&
             args.PropertyName is nameof(HotkeysViewModel.ConflictText) or
                 nameof(HotkeysViewModel.DuplicateText) or nameof(HotkeysViewModel.RecorderStatusText)) ||
            (ReferenceEquals(sender, CycleGroups) &&
             args.PropertyName is (nameof(CycleGroupsViewModel.SelectedCandidate) or
                 nameof(CycleGroupsViewModel.SelectedGroup) or
                 nameof(CycleGroupsViewModel.IsCandidatePickerOpen))) ||
            _applyingSettingsToModel)
        {
            return;
        }

        OnViewModelSettingChanged();
    }

    private void OnCycleGroupsChanged(object? sender, EventArgs e)
    {
        SubscribeToCycleGroupChanges();
        OnViewModelSettingChanged();
    }

    private void SubscribeToCycleGroupChanges()
    {
        foreach (var group in CycleGroups.Groups)
        {
            group.PropertyChanged -= OnChildPropertyChanged;
            group.PropertyChanged += OnChildPropertyChanged;
        }
    }

    private string ValidateHotkeyText()
    {
        var bindings = new List<(string Label, string Value)>
        {
            ("最小化全部", Hotkeys.MinimizeAllHotkey),
            ("刷新最小化客户端", Hotkeys.RefreshMinimizedHotkey),
            ("返回此前外部应用", Hotkeys.ReturnToPreviousHotkey)
        };
        for (var index = 0; index < CycleGroups.Groups.Count; index++)
        {
            bindings.Add(($"循环组 {index + 1} 前进", CycleGroups.Groups[index].ForwardHotkey));
            bindings.Add(($"循环组 {index + 1} 后退", CycleGroups.Groups[index].BackwardHotkey));
        }

        bindings.AddRange(Clients.Clients.Select(client =>
            ($"客户端 {client.DisplayName}", client.HotKey)));

        var assigned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (label, value) in bindings)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!HotKeyParser.TryParse(value, out var hotkey))
            {
                return $"热键 '{value}'（{label}）无效。";
            }

            if (hotkey.MouseButton != HotKeyMouseButton.None)
            {
                return $"热键 '{value}'（{label}）不能设置为鼠标按键。";
            }

            if (hotkey.Modifiers == KeyModifiers.None)
            {
                return $"热键 '{value}'（{label}）必须包含至少一个修饰键。";
            }

            if (assigned.TryGetValue(hotkey.Canonical, out var previousLabel))
            {
                return $"热键冲突：'{hotkey.Canonical}' 同时绑定到 {previousLabel} 和 {label}。";
            }

            assigned[hotkey.Canonical] = label;
        }

        return string.Empty;
    }

}
