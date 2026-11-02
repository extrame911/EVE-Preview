using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EvePreview.App.ViewModels;
using Application = System.Windows.Application;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class MainWindowStatusRenderingTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Status_bar_renders_the_build_date_and_three_part_version()
    {
        wpf.Invoke(() =>
        {
            MainWindow? window = null;
            try
            {
                var application = Application.Current!;
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/EVE-Preview;component/Themes/DarkTheme.xaml")
                });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/EVE-Preview;component/Themes/IconResources.xaml")
                });
                var viewModel = new MainWindowViewModel();
                window = new MainWindow(viewModel)
                {
                    Left = -10000,
                    Top = -10000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    AllowClose = true
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                var texts = FindDescendants<TextBlock>(window).Select(text => text.Text).ToArray();
                Assert.Contains(viewModel.BuildDate, texts);
                Assert.Contains($"v{viewModel.Version}", texts);
                Assert.Matches(@"^\d+\.\d+\.\d+$", viewModel.Version);
                Assert.DoesNotContain("EVE-PREVIEW / x64", texts);
            }
            finally
            {
                window?.Close();
            }
        });
    }

    [Fact]
    public void Closing_to_tray_commits_the_focused_explicit_input()
    {
        wpf.Invoke(() =>
        {
            MainWindow? window = null;
            try
            {
                var application = Application.Current!;
                foreach (var relativePath in new[]
                         {
                             "Themes/IconResources.xaml",
                             "Themes/DarkTheme.xaml",
                             "Views/ThumbnailSettingsView.xaml"
                         })
                {
                    application.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"pack://application:,,,/EVE-Preview;component/{relativePath}")
                    });
                }

                var viewModel = new MainWindowViewModel();
                viewModel.Settings.General.CloseToTray = true;
                viewModel.NavigateCommand.Execute(AppPage.Thumbnails);
                window = new MainWindow(viewModel)
                {
                    Left = -10000,
                    Top = -10000,
                    ShowActivated = false,
                    ShowInTaskbar = false
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                var widthInput = FindDescendants<TextBox>(window)
                    .Single(textBox => System.Windows.Data.BindingOperations
                        .GetBinding(textBox, TextBox.TextProperty)?.Path.Path == nameof(ThumbnailSettingsViewModel.DefaultWidth));
                widthInput.Focus();
                widthInput.Text = "640";
                Assert.Equal(320, viewModel.Settings.Preview.DefaultWidth);

                window.Close();

                Assert.Equal(640, viewModel.Settings.Preview.DefaultWidth);
                window.AllowClose = true;
            }
            finally
            {
                if (window is not null)
                {
                    window.AllowClose = true;
                    window.Close();
                }
            }
        });
    }

    [Fact]
    public void Sidebar_navigation_and_project_link_have_tooltips()
    {
        wpf.Invoke(() =>
        {
            MainWindow? window = null;
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
                window = new MainWindow(new MainWindowViewModel())
                {
                    Left = -10000,
                    Top = -10000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    AllowClose = true
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                var buttons = FindDescendants<Button>(window).ToArray();
                Assert.NotEmpty(buttons);
                Assert.All(buttons, button => Assert.False(
                    string.IsNullOrWhiteSpace(button.ToolTip as string),
                    $"Missing tooltip on button '{button.Content}'"));
                var link = FindDescendants<TextBlock>(window)
                    .SelectMany(text => text.Inlines.OfType<System.Windows.Documents.Hyperlink>())
                    .Single(hyperlink => hyperlink.NavigateUri?.AbsoluteUri ==
                                         "https://github.com/extrame911/EVE-Preview");
                Assert.False(string.IsNullOrWhiteSpace(link.ToolTip as string));
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
