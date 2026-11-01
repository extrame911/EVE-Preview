using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EvePreview.App.Services;

namespace EvePreview.App.Behaviors;

public static class HotkeyRecorder
{
    private static readonly DependencyProperty OriginalTextProperty = DependencyProperty.RegisterAttached(
        "OriginalText",
        typeof(string),
        typeof(HotkeyRecorder),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(HotkeyRecorder),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static bool TryFormat(Key key, ModifierKeys modifiers, out string text)
    {
        text = string.Empty;
        if (modifiers == ModifierKeys.None)
        {
            return false;
        }

        var keyText = key switch
        {
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
            >= Key.F1 and <= Key.F24 => key.ToString(),
            Key.Back => "Back",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Space => "Space",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.End => "End",
            Key.Home => "Home",
            Key.Left => "Left",
            Key.Up => "Up",
            Key.Right => "Right",
            Key.Down => "Down",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            _ => string.Empty
        };
        if (keyText.Length == 0)
        {
            return false;
        }

        var parts = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(keyText);
        text = string.Join('+', parts);
        return true;
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            textBox.PreviewMouseDown += OnPreviewMouseDown;
            textBox.GotKeyboardFocus += OnGotKeyboardFocus;
        }
        else
        {
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            textBox.PreviewMouseDown -= OnPreviewMouseDown;
            textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
        }
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SetValue(OriginalTextProperty, textBox.Text);
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (e.Key == Key.Escape || (e.Key == Key.System && e.SystemKey == Key.Escape))
        {
            var original = (string)textBox.GetValue(OriginalTextProperty);
            textBox.SetCurrentValue(TextBox.TextProperty, original);
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            InputFocus.Release(textBox);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            InputFocus.Release(textBox);
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
            Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.None)
        {
            return;
        }

        if (!TryFormat(key, Keyboard.Modifiers, out var text))
        {
            textBox.ToolTip = "请使用至少一个修饰键，或按 Esc 清除。支持字母、数字、F1–F24 和常用导航键。";
            return;
        }

        textBox.Text = text;
        textBox.CaretIndex = textBox.Text.Length;
        textBox.ToolTip = null;
        e.Handled = true;
    }

    private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox ||
            e.ChangedButton is not (MouseButton.XButton1 or MouseButton.XButton2))
        {
            return;
        }

        textBox.ToolTip = LocalizationManager.IsEnglish
            ? "Hotkeys support keyboard combinations only; mouse buttons are not allowed."
            : "热键仅支持键盘组合，不能设置为鼠标按键。";
        e.Handled = true;
    }
}
