using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EvePreview.App.ViewModels;
using Application = System.Windows.Application;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class OverviewLayoutRenderingTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Client_cards_render_two_per_row_at_reduced_width()
    {
        wpf.Invoke(() =>
        {
            Window? window = null;
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
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/EVE-Preview;component/Views/OverviewView.xaml")
                });
                var viewModel = new OverviewViewModel();
                for (var index = 1; index <= 3; index++)
                {
                    viewModel.Clients.Add(new ClientSummaryViewModel
                    {
                        CharacterName = $"Character {index}",
                        ServerName = "EVE"
                    });
                }

                var template = (DataTemplate)application.FindResource(new DataTemplateKey(typeof(OverviewViewModel)));
                window = new Window
                {
                    Width = 1100,
                    Height = 760,
                    Left = -10000,
                    Top = -10000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Content = new ContentControl { Content = viewModel, ContentTemplate = template }
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                var items = FindDescendants<ItemsControl>(window).Single();
                var cards = FindDescendants<Border>(items)
                    .Where(border => Equals(border.Style, application.Resources["ClientCardStyle"]))
                    .ToArray();
                Assert.Equal(3, cards.Length);
                Assert.Equal(240, cards[0].ActualWidth);
                var positions = cards.Select(card => card.TransformToAncestor(items).Transform(new Point())).ToArray();
                var firstRowCount = positions.Count(point => Math.Abs(point.Y - positions[0].Y) < 1);
                Assert.Equal(2, firstRowCount);
                Assert.True(positions[2].Y > positions[0].Y);
            }
            finally
            {
                window?.Close();
            }
        });
    }

    [Fact]
    public void Quick_actions_replace_refresh_with_save_all_window_positions()
    {
        wpf.Invoke(() =>
        {
            Window? window = null;
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
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/EVE-Preview;component/Views/OverviewView.xaml")
                });
                var viewModel = new MainWindowViewModel();
                var template = (DataTemplate)application.FindResource(new DataTemplateKey(typeof(OverviewViewModel)));
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
                    Content = new ContentControl { Content = viewModel.Overview, ContentTemplate = template }
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                var contents = FindDescendants<Button>(window).Select(button => button.Content as string).ToArray();
                Assert.DoesNotContain("刷新最小化客户端", contents);
                Assert.Equal(1, contents.Count(content => content == "保存所有窗口位置"));
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
