using System.Windows.Controls;
using System.Windows.Input;
using EvePreview.App.Behaviors;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class HotkeyRecorderTests(WpfApplicationFixture wpf)
{
    [Theory]
    [InlineData(Key.F4, ModifierKeys.Control | ModifierKeys.Shift, true, "Ctrl+Shift+F4")]
    [InlineData(Key.A, ModifierKeys.Alt, true, "Alt+A")]
    [InlineData(Key.D7, ModifierKeys.Control, true, "Ctrl+7")]
    [InlineData(Key.Left, ModifierKeys.Control, true, "Ctrl+Left")]
    [InlineData(Key.A, ModifierKeys.None, false, "")]
    [InlineData(Key.OemComma, ModifierKeys.Control, false, "")]
    public void Formats_recorded_chords_as_canonical_text(
        Key key,
        ModifierKeys modifiers,
        bool expectedValid,
        string expectedText)
    {
        var valid = HotkeyRecorder.TryFormat(key, modifiers, out var text);

        Assert.Equal(expectedValid, valid);
        Assert.Equal(expectedText, text);
    }

    [Fact]
    public void Mouse_side_button_does_not_replace_the_recorded_keyboard_hotkey()
    {
        wpf.Invoke(() =>
        {
            var textBox = new TextBox { Text = "Ctrl+F4" };
            HotkeyRecorder.SetIsEnabled(textBox, true);

            textBox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.XButton1)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent
            });

            Assert.Equal("Ctrl+F4", textBox.Text);
            Assert.Contains("鼠标", textBox.ToolTip?.ToString());
        });
    }
}
