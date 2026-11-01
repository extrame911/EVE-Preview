using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EvePreview.App.Controls;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class CutCornerPanelRenderingTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Card_style_applies_and_renders_the_shared_cut_corner_template()
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
                var panel = new CutCornerPanel
                {
                    Style = (Style)application.Resources["CardStyle"],
                    Header = "Section",
                    SectionCode = "SECTION",
                    Content = new TextBlock { Text = "Body" }
                };
                window = new Window
                {
                    Width = 400,
                    Height = 240,
                    Left = -10000,
                    Top = -10000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Content = panel
                };

                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                Assert.True(ContainsDescendant<CutCornerBorder>(panel));
            }
            finally
            {
                window?.Close();
            }
        });
    }

    private static bool ContainsDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T)
        {
            return true;
        }
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (ContainsDescendant<T>(VisualTreeHelper.GetChild(root, index)))
            {
                return true;
            }
        }
        return false;
    }
}
