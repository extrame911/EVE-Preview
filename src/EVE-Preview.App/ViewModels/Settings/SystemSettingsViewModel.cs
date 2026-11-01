using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvePreview.App.Services;
using EvePreview.Core;

namespace EvePreview.App.ViewModels;

public sealed partial class SystemSettingsViewModel : ObservableObject
{
    public const string CompatibilityModeSettingName = "Monitoring.UseCompatibilityMode";
    private readonly ConfigurationProfileService? _profiles;

    public SystemSettingsViewModel()
    {
    }

    public SystemSettingsViewModel(AppSettings settings, ConfigurationProfileService? profiles = null)
    {
        _profiles = profiles;
        Profiles = new ObservableCollection<string>(profiles?.ListProfiles() ?? []);
        LoadFrom(settings);
        RefreshSelectedProfile();
    }

    [ObservableProperty]
    private string _language = "简体中文";

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private bool _minimizeInactiveClients;

    [ObservableProperty]
    private bool _enableWindowAnimations = true;

    [ObservableProperty]
    private string _executableNames = "ExeFile";

    [ObservableProperty]
    private string _currentProfileName = "EVE-Preview.json";

    [ObservableProperty]
    private string? _selectedProfile;

    [ObservableProperty]
    private string _profileStatusText = string.Empty;

    public ObservableCollection<string> Profiles { get; } = [];

    public event EventHandler? ReloadProfileRequested;
    public event EventHandler? SaveProfileAsRequested;
    public event EventHandler? ImportLegacyRequested;
    public event EventHandler? OpenProfileFolderRequested;
    public event EventHandler<string>? SwitchProfileRequested;

    [ObservableProperty]
    private string _diagnosticSummary = "尚无原生调用错误。";

    [ObservableProperty]
    private bool _useCompatibilityMode;

    private int _pollingIntervalMilliseconds = 500;

    [ObservableProperty]
    private string _pollingIntervalError = string.Empty;

    public event EventHandler<SettingChangedEventArgs>? SettingChanged;

    public string RenderingModeText => LocalizationManager.Translate(UseCompatibilityMode
        ? "GDI 兼容模式（约 1 FPS）"
        : "DWM 实时预览");

    public int PollingIntervalMilliseconds
    {
        get => _pollingIntervalMilliseconds;
        set
        {
            if (value is < 100 or > 2000)
            {
                PollingIntervalError = "轮询周期范围为 100–2000 毫秒。";
                return;
            }

            PollingIntervalError = string.Empty;
            if (!SetProperty(ref _pollingIntervalMilliseconds, value))
            {
                return;
            }

            SettingChanged?.Invoke(this,
                new SettingChangedEventArgs("Monitoring.PollingIntervalMilliseconds", value));
        }
    }

    partial void OnUseCompatibilityModeChanged(bool value)
    {
        OnPropertyChanged(nameof(RenderingModeText));
        SettingChanged?.Invoke(this, new SettingChangedEventArgs(CompatibilityModeSettingName, value));
    }

    public void LoadFrom(AppSettings settings)
    {
        Language = settings.General.Language.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            ? "English"
            : "简体中文";
        MinimizeToTray = settings.General.MinimizeToTray;
        CloseToTray = settings.General.CloseToTray;
        MinimizeInactiveClients = settings.General.MinimizeInactiveClients;
        EnableWindowAnimations = settings.General.UseWindowAnimations;
        ExecutableNames = string.Join(", ", settings.Monitoring.ProcessNames);
        PollingIntervalMilliseconds = settings.Monitoring.PollIntervalMs;
        UseCompatibilityMode = settings.Compatibility.RenderingMode == RenderingMode.Gdi;
    }

    public void SetProfilePath(string path)
    {
        CurrentProfileName = Path.GetFileName(path);
        RefreshProfileList();
    }

    public void RefreshProfileList()
    {
        if (_profiles is null)
        {
            return;
        }

        Profiles.Clear();
        foreach (var profile in _profiles.ListProfiles())
        {
            Profiles.Add(profile);
        }

        RefreshSelectedProfile();
    }

    private void RefreshSelectedProfile()
    {
        SelectedProfile = Profiles.FirstOrDefault(profile =>
            string.Equals(profile, CurrentProfileName, StringComparison.OrdinalIgnoreCase))
            ?? Profiles.FirstOrDefault();
    }

    [RelayCommand]
    private void ReloadProfile()
    {
        if (_profiles is null)
        {
            ProfileStatusText = "配置服务不可用。";
            return;
        }

        ReloadProfileRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SaveProfileAs()
    {
        if (_profiles is null)
        {
            ProfileStatusText = "配置服务不可用。";
            return;
        }

        SaveProfileAsRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ImportLegacyProfile()
    {
        if (_profiles is null)
        {
            ProfileStatusText = "配置服务不可用。";
            return;
        }

        ImportLegacyRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenProfileFolder()
    {
        if (_profiles is null)
        {
            ProfileStatusText = "配置服务不可用。";
            return;
        }

        OpenProfileFolderRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SwitchProfile()
    {
        if (_profiles is null || string.IsNullOrWhiteSpace(SelectedProfile))
        {
            ProfileStatusText = "请选择配置文件。";
            return;
        }

        SwitchProfileRequested?.Invoke(this, SelectedProfile);
    }
}
