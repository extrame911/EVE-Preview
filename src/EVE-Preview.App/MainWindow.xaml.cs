using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Threading;
using EvePreview.App.Behaviors;
using EvePreview.App.Services;
using EvePreview.App.ViewModels;

namespace EvePreview.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.SaveFailurePromptRequested += OnSaveFailurePromptRequested;
        StateChanged += OnStateChanged;
        SizeChanged += OnSizeChanged;
        Closing += OnClosing;
        Loaded += OnLoaded;
        Closed += OnClosed;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        LocalizationManager.LanguageChanged += OnLanguageChanged;
    }

    public event EventHandler? ExitRequested;

    public bool AllowClose { get; set; }

    public void CommitPendingInput() => CommitOnEnterOrLostFocus.CommitFocusedInput(this);

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Focus();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdatePageContext();
        BrandSubtitle.Visibility = ActualWidth >= 900 ? Visibility.Visible : Visibility.Collapsed;
        VisualLocalizer.RefreshFrom(this);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        BrandSubtitle.Visibility = ActualWidth >= 900 ? Visibility.Visible : Visibility.Collapsed;

    private void UpdatePageContext() =>
        PageContextText.Text = _viewModel.CurrentPage switch
        {
            AppPage.Overview => "01 / OVERVIEW",
            AppPage.Thumbnails => "02 / THUMBNAILS",
            AppPage.Clients => "03 / CLIENTS",
            AppPage.CycleGroups => "04 / CYCLE LIST",
            AppPage.System => "05 / SYSTEM",
            _ => throw new ArgumentOutOfRangeException()
        };

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(MainWindowViewModel.CurrentPageViewModel))
        {
            return;
        }

        UpdatePageContext();
        Dispatcher.BeginInvoke(
            () => VisualLocalizer.RefreshFrom(this),
            DispatcherPriority.Loaded);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _viewModel.CycleGroups.RefreshLocalizedText();
        Dispatcher.BeginInvoke(
            () => VisualLocalizer.RefreshFrom(this),
            DispatcherPriority.Loaded);
    }

    private async void OnSaveFailurePromptRequested(string saveError)
    {
        var message = $"{saveError}\n\n" +
            LocalizationManager.Translate("是否恢复到最后一次成功保存的配置？") + "\n" +
            LocalizationManager.Translate("选择“否”将保留当前修改，也可稍后在系统页恢复。");
        var result = MessageBox.Show(
            this,
            message,
            LocalizationManager.Translate("配置保存失败"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Error,
            MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
        {
            await _viewModel.DiscardUnsavedChangesCommand.ExecuteAsync(null);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.SaveFailurePromptRequested -= OnSaveFailurePromptRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        LocalizationManager.LanguageChanged -= OnLanguageChanged;
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnProjectLinkRequested(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _viewModel.LastError = $"无法打开 GitHub 页面：{exception.Message}";
        }

        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized &&
            DataContext is MainWindowViewModel viewModel &&
            viewModel.Settings.General.MinimizeToTray)
        {
            CommitPendingInput();
            Hide();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

        e.Cancel = true;
        CommitPendingInput();
        if (DataContext is MainWindowViewModel viewModel && viewModel.Settings.General.CloseToTray)
        {
            Hide();
        }
        else
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
