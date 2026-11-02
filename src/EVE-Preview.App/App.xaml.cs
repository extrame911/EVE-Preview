using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using EvePreview.App.Services;
using EvePreview.App.ViewModels;
using EvePreview.Core;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace EvePreview.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private ClientPreviewRuntime? _runtime;
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIcon;
    private SingleInstanceGuard? _singleInstance;
    private ProfileRestartRequest? _restartRequest;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterExceptionHandlers();
        LocalizationManager.Initialize();

        _singleInstance = SingleInstanceGuard.TryAcquire(out var alreadyRunning);
        if (alreadyRunning)
        {
            SingleInstanceGuard.SignalExistingInstance();
            Shutdown();
            return;
        }

        _singleInstance!.ActivationRequested += (_, _) =>
            Dispatcher.BeginInvoke(() => _mainWindow?.ShowFromTray(), DispatcherPriority.Normal);
        _singleInstance.StartListening();

        var storePath = Path.Combine(AppContext.BaseDirectory, "EVE-Preview.json");
        var requestedProfile = GetRequestedProfilePath(e.Args);
        if (requestedProfile is not null)
        {
            storePath = requestedProfile;
        }

        var store = new JsonSettingsStore(storePath);
        SettingsLoadResult load;
        try
        {
            load = store.Load();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"无法读取配置文件：\n{exception.Message}\n\n请确认程序目录可写后重试。",
                "EVE-Preview 启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton(load.Settings);
        services.AddSingleton(store);
        services.AddSingleton(new ConfigurationProfileService(store.Path));
        services.AddSingleton<ClientPreviewRuntime>();
        services.AddSingleton<IPreviewRuntime>(provider => provider.GetRequiredService<ClientPreviewRuntime>());
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();
        _runtime = _services.GetRequiredService<ClientPreviewRuntime>();
        var profileService = _services.GetRequiredService<ConfigurationProfileService>();
        profileService.RestartRequested += (_, request) =>
        {
            _restartRequest = request;
            ExitApplication();
        };

        var viewModel = _services.GetRequiredService<MainWindowViewModel>();
        viewModel.SetProfilePath(store.Path);
        LocalizationManager.SetLanguage(load.Settings.General.Language);
        if (!string.IsNullOrWhiteSpace(load.Diagnostic))
        {
            viewModel.SetStartupDiagnostic(load.Diagnostic);
        }

        _mainWindow = _services.GetRequiredService<MainWindow>();
        _mainWindow.ExitRequested += (_, _) => ExitApplication();
        var windowIcon = AppIconProvider.LoadWindowIcon();
        if (windowIcon is not null)
        {
            _mainWindow.Icon = windowIcon;
        }

        _mainWindow.Show();

        _trayIcon = new TrayIconService(
            () => _mainWindow.ShowFromTray(),
            () => TogglePreviewAsync(viewModel),
            ExitApplication,
            AppIconProvider.LoadTrayIcon());
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.ServiceStatusText))
            {
                _trayIcon?.SetPreviewRunning(viewModel.Overview.ServiceState == PreviewServiceState.Running);
            }
        };

        Dispatcher.BeginInvoke(
            () => viewModel.TogglePreviewCommand.Execute(null),
            DispatcherPriority.ApplicationIdle);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }

    private async Task TogglePreviewAsync(MainWindowViewModel viewModel)
    {
        await viewModel.TogglePreviewCommand.ExecuteAsync(null);
    }

    private void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        if (_mainWindow is not null)
        {
            _mainWindow.CommitPendingInput();
            _mainWindow.AllowClose = true;
        }

        string? saveError = null;
        try
        {
            saveError = Cleanup();
        }
        catch (Exception exception)
        {
            saveError = $"退出清理失败：{exception.Message}";
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(saveError))
            {
                MessageBox.Show(
                    $"{saveError}\n\n配置可能未保存。请检查程序目录权限或磁盘空间。",
                    "EVE-Preview",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            Shutdown();
        }
    }

    private string? Cleanup()
    {
        var restartRequest = _restartRequest;
        _restartRequest = null;
        var skipSave = restartRequest is { SaveCurrentSettings: false };
        var saveError = _runtime is null
            ? null
            : skipSave ? DisposeRuntimeWithoutSaving() : _runtime.StopForShutdown();
        _runtime = null;
        try
        {
            _trayIcon?.Dispose();
        }
        catch (Exception exception)
        {
            saveError ??= $"托盘清理失败：{exception.Message}";
        }

        _trayIcon = null;
        try
        {
            _services?.Dispose();
        }
        catch (Exception exception)
        {
            saveError ??= $"服务清理失败：{exception.Message}";
        }

        _services = null;
        try
        {
            _singleInstance?.Dispose();
        }
        catch (Exception exception)
        {
            saveError ??= $"单实例清理失败：{exception.Message}";
        }

        _singleInstance = null;
        if (restartRequest is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath
                        ?? throw new InvalidOperationException("The application executable path is unavailable."),
                    Arguments = $"--profile \"{restartRequest.Path}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                saveError ??= $"重启失败：{exception.Message}";
            }
        }

        return saveError;
    }

    private string? DisposeRuntimeWithoutSaving()
    {
        try
        {
            _runtime?.Dispose();
            return null;
        }
        catch (Exception exception)
        {
            return $"关闭当前配置失败：{exception.Message}";
        }
    }

    private static string? GetRequestedProfilePath(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (!string.Equals(arguments[index], "--profile", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var path = Path.GetFullPath(arguments[index + 1]);
                return File.Exists(path) ? path : null;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        return null;
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            WriteDiagnostic("Dispatcher", args.Exception);
            args.Handled = true;
            if (!_exiting)
            {
                MessageBox.Show(
                    $"发生未处理错误：{args.Exception.Message}\n\n详细诊断已写入程序目录的 EVE-Preview.log。",
                    "EVE-Preview",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteDiagnostic("UnobservedTask", args.Exception);
            args.SetObserved();
        };
    }

    private static void WriteDiagnostic(string source, Exception exception)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "EVE-Preview.log"),
                $"{DateTimeOffset.Now:O} [{source}] {exception}{Environment.NewLine}");
        }
        catch (Exception writeException) when (writeException is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must never trigger a second failure.
        }
    }
}
