using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EvePreview.App.Behaviors;
using EvePreview.App.Controls;
using EvePreview.App.ViewModels;
using EvePreview.Core;
using Application = System.Windows.Application;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class InputCommitTimingRenderingTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void Plain_inputs_commit_on_enter_or_lost_focus_while_hotkeys_stay_recorder_driven()
    {
        wpf.Invoke(() =>
        {
            var windows = new List<Window>();
            try
            {
                LoadResources();
                var settings = new AppSettings();
                settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - Alpha", Alias = "Main" });
                var viewModel = new MainWindowViewModel(null, settings);
                var systemWindow = ShowPage(windows, viewModel, viewModel.SystemSettings);
                var thumbnailWindow = ShowPage(windows, viewModel, viewModel.ThumbnailSettings);
                var clientsWindow = ShowPage(windows, viewModel, viewModel.Clients);
                var cycleGroupsWindow = ShowPage(windows, viewModel, viewModel.CycleGroups);
                ShowPage(windows, viewModel, viewModel.Overview);
                Assert.Contains(FindDescendants<TextBlock>(thumbnailWindow), text => text.Text == "宽度（像素）");
                Assert.Contains(FindDescendants<TextBlock>(thumbnailWindow), text => text.Text == "高度（像素）");
                Assert.Contains(FindDescendants<TextBlock>(thumbnailWindow), text => text.Text == "X 网格尺寸（1–512 像素）");
                Assert.Contains(FindDescendants<TextBlock>(thumbnailWindow), text => text.Text == "Y 网格尺寸（1–512 像素）");
                Assert.DoesNotContain(FindDescendants<TextBlock>(thumbnailWindow), text => text.Text.Contains("DIP", StringComparison.Ordinal));

                var allInputs = windows.SelectMany(FindDescendants<TextBox>).ToArray();
                Assert.All(allInputs, textBox => Assert.True(
                    CommitOnEnterOrLostFocus.GetIsEnabled(textBox) || HotkeyRecorder.GetIsEnabled(textBox)));
                var optionInputs = windows
                    .SelectMany(FindDescendants<FrameworkElement>)
                    .Where(element => element is CheckBox or TextBox or ComboBox or ColorPickerBox)
                    .ToArray();
                Assert.Equal(47, optionInputs.Length);
                Assert.All(optionInputs, element => Assert.False(
                    string.IsNullOrWhiteSpace((element as FrameworkElement).ToolTip as string),
                    $"Missing tooltip on {element.GetType().Name}"));
                foreach (var colorPicker in optionInputs.OfType<ColorPickerBox>())
                {
                    var colorPickerButtons = FindDescendants<Button>(colorPicker).ToArray();
                    Assert.All(colorPickerButtons, button => Assert.False(
                        string.IsNullOrWhiteSpace(button.ToolTip as string),
                        $"Missing tooltip on {colorPicker.Name}.{button.Name}"));
                    Assert.Equal(
                        colorPicker.ToolTip as string,
                        colorPickerButtons.Single(button => button.Name == "PickButton").ToolTip as string);
                }
                var plainInputs = allInputs
                    .Where(textBox => !HotkeyRecorder.GetIsEnabled(textBox))
                    .ToArray();
                Assert.Equal(14, plainInputs.Length);
                var buttons = windows.SelectMany(FindDescendants<Button>).ToArray();
                Assert.NotEmpty(buttons);
                Assert.All(buttons, button => Assert.False(
                    string.IsNullOrWhiteSpace(button.ToolTip as string),
                    $"Missing tooltip on button '{button.Content}'"));
                foreach (var textBox in plainInputs)
                {
                    var binding = BindingOperations.GetBinding(textBox, TextBox.TextProperty);
                    Assert.NotNull(binding);
                    Assert.Equal(UpdateSourceTrigger.Explicit, binding!.UpdateSourceTrigger);
                    Assert.True(CommitOnEnterOrLostFocus.GetIsEnabled(textBox));
                }

                var executableNames = FindBoundTextBox(systemWindow, "ExecutableNames");
                var originalNames = viewModel.SystemSettings.ExecutableNames;
                executableNames.Text = "exefile, other";
                Assert.Equal(originalNames, viewModel.SystemSettings.ExecutableNames);
                executableNames.RaiseEvent(new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(executableNames),
                    0,
                    Key.Enter)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                });
                Assert.Equal("exefile, other", viewModel.SystemSettings.ExecutableNames);

                var alias = FindBoundTextBox(clientsWindow, "Alias");
                alias.Text = "Renamed";
                Assert.Equal("Main", viewModel.Clients.SelectedClient!.Alias);
                alias.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
                Assert.Equal("Renamed", viewModel.Clients.SelectedClient.Alias);

                var hotkeyInputs = windows
                    .SelectMany(FindDescendants<TextBox>)
                    .Where(HotkeyRecorder.GetIsEnabled)
                    .ToArray();
                Assert.Equal(6, hotkeyInputs.Length);
                foreach (var textBox in hotkeyInputs)
                {
                    var binding = BindingOperations.GetBinding(textBox, TextBox.TextProperty);
                    Assert.NotNull(binding);
                    Assert.Equal(UpdateSourceTrigger.PropertyChanged, binding!.UpdateSourceTrigger);
                    Assert.False(CommitOnEnterOrLostFocus.GetIsEnabled(textBox));
                }
            }
            finally
            {
                foreach (var window in windows)
                {
                    window.Close();
                }
            }
        });
    }

    [Fact]
    public void Focused_plain_input_is_committed_before_hiding()
    {
        wpf.Invoke(() =>
        {
            Window? window = null;
            try
            {
                var holder = new InputHolder { Name = "before" };
                var textBox = new TextBox();
                textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(InputHolder.Name))
                {
                    Source = holder,
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit
                });
                window = new Window
                {
                    Width = 300,
                    Height = 120,
                    Left = -10000,
                    Top = -10000,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Content = textBox
                };
                window.Show();
                textBox.Focus();
                textBox.Text = "typed";

                Assert.Equal("before", holder.Name);
                Assert.True(CommitOnEnterOrLostFocus.CommitFocusedInput(window));
                Assert.Equal("typed", holder.Name);
            }
            finally
            {
                window?.Close();
            }
        });
    }

    [Fact]
    public void Enter_commits_plain_input_and_releases_keyboard_focus()
    {
        wpf.Invoke(() =>
        {
            var holder = new InputHolder { Name = "before" };
            var (window, textBox) = CreatePlainInputWindow(holder);
            try
            {
                window.Show();
                textBox.Focus();
                textBox.Text = "typed";

                textBox.RaiseEvent(PreviewKey(Key.Enter, textBox));

                Assert.Equal("typed", holder.Name);
                Assert.False(textBox.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Escape_discards_plain_input_and_releases_keyboard_focus()
    {
        wpf.Invoke(() =>
        {
            var holder = new InputHolder { Name = "before" };
            var (window, textBox) = CreatePlainInputWindow(holder);
            try
            {
                window.Show();
                textBox.Focus();
                textBox.Text = "typed";

                textBox.RaiseEvent(PreviewKey(Key.Escape, textBox));

                Assert.Equal("before", holder.Name);
                Assert.Equal("before", textBox.Text);
                Assert.False(textBox.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Escape_restores_hotkey_input_and_releases_keyboard_focus()
    {
        wpf.Invoke(() =>
        {
            var textBox = new TextBox { Text = "Ctrl+F4" };
            HotkeyRecorder.SetIsEnabled(textBox, true);
            var window = ShowInputWindow(textBox);
            try
            {
                window.Show();
                textBox.Focus();
                textBox.Text = "Ctrl+F5";

                textBox.RaiseEvent(PreviewKey(Key.Escape, textBox));

                Assert.Equal("Ctrl+F4", textBox.Text);
                Assert.False(textBox.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Enter_releases_hotkey_input_without_changing_its_value()
    {
        wpf.Invoke(() =>
        {
            var textBox = new TextBox { Text = "Ctrl+F4" };
            HotkeyRecorder.SetIsEnabled(textBox, true);
            var window = ShowInputWindow(textBox);
            try
            {
                window.Show();
                textBox.Focus();
                textBox.Text = "Ctrl+F5";

                textBox.RaiseEvent(PreviewKey(Key.Enter, textBox));

                Assert.Equal("Ctrl+F5", textBox.Text);
                Assert.False(textBox.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static (Window Window, TextBox TextBox) CreatePlainInputWindow(InputHolder holder)
    {
        var textBox = new TextBox();
        textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(InputHolder.Name))
        {
            Source = holder,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        });
        CommitOnEnterOrLostFocus.SetIsEnabled(textBox, true);
        return (ShowInputWindow(textBox), textBox);
    }

    private static Window ShowInputWindow(TextBox textBox) =>
        new()
        {
            Width = 300,
            Height = 120,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = textBox
        };

    private static KeyEventArgs PreviewKey(Key key, TextBox textBox) =>
        new(Keyboard.PrimaryDevice, PresentationSource.FromVisual(textBox), 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };

    private static void LoadResources()
    {
        var application = Application.Current!;
        foreach (var relativePath in new[]
                 {
                     "Themes/IconResources.xaml",
                     "Themes/DarkTheme.xaml",
                     "Views/SystemSettingsView.xaml",
                     "Views/ThumbnailSettingsView.xaml",
                     "Views/ClientsView.xaml",
                     "Views/CycleGroupsView.xaml",
                     "Views/OverviewView.xaml"
                 })
        {
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/EVE-Preview;component/{relativePath}")
            });
        }
    }

    private static Window ShowPage(List<Window> windows, MainWindowViewModel viewModel, object page)
    {
        var template = (DataTemplate)Application.Current!.FindResource(new DataTemplateKey(page.GetType()));
        var window = new Window
        {
            Width = 900,
            Height = 700,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            DataContext = viewModel,
            Content = new ContentControl { Content = page, ContentTemplate = template }
        };
        windows.Add(window);
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        return window;
    }

    private static TextBox FindBoundTextBox(DependencyObject root, string path) =>
        FindDescendants<TextBox>(root).Single(textBox =>
            BindingOperations.GetBinding(textBox, TextBox.TextProperty)?.Path.Path == path);

    private sealed class InputHolder
    {
        public string Name { get; set; } = string.Empty;
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
