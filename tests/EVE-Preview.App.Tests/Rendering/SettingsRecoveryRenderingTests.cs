using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EvePreview.App.ViewModels;
using Application = System.Windows.Application;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class SettingsRecoveryRenderingTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Save_failure_exposes_a_bound_discard_changes_button()
    {
        wpf.Invoke(() =>
        {
            Window? window = null;
            try
            {
                var application = Application.Current!;
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/EVE-Preview;component/Themes/IconResources.xaml")
                });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/EVE-Preview;component/Themes/DarkTheme.xaml")
                });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/EVE-Preview;component/Views/SystemSettingsView.xaml")
                });
                var viewModel = new MainWindowViewModel { CanDiscardUnsavedChanges = true };
                var template = (DataTemplate)application.FindResource(new DataTemplateKey(typeof(SystemSettingsViewModel)));
                window = new Window
                {
                    Width = 780,
                    Height = 538,
                    Left = -10000,
                    Top = -10000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    DataContext = viewModel,
                    Content = new ContentControl { Content = viewModel.SystemSettings, ContentTemplate = template }
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                var button = FindDescendants<Button>(window).Single(candidate =>
                    Equals(candidate.Content, "取消未保存修改"));
                Assert.True(button.IsEnabled);
                Assert.Same(viewModel.DiscardUnsavedChangesCommand, button.Command);
            }
            finally
            {
                window?.Close();
            }
        });
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var descendant in FindDescendants<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return descendant;
            }
        }
    }
}
