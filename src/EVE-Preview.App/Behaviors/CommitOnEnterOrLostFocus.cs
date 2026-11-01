using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace EvePreview.App.Behaviors;

public static class CommitOnEnterOrLostFocus
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(CommitOnEnterOrLostFocus),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static bool CommitFocusedInput(Window window)
    {
        if (Keyboard.FocusedElement is not TextBox textBox || !window.IsAncestorOf(textBox))
        {
            return false;
        }

        Commit(textBox);
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
            textBox.LostFocus += OnLostFocus;
        }
        else
        {
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            textBox.LostFocus -= OnLostFocus;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (IsEscape(e))
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            InputFocus.Release(textBox);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        Commit(textBox);
        InputFocus.Release(textBox);
        e.Handled = true;
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            Commit(textBox);
        }
    }

    private static void Commit(TextBox textBox) =>
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

    private static bool IsEscape(KeyEventArgs e) =>
        e.Key == Key.Escape || (e.Key == Key.System && e.SystemKey == Key.Escape);
}
