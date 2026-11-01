using System.Windows;

namespace EvePreview.App.Controls;

public static class NavigationButtonProperties
{
    public static readonly DependencyProperty IndexProperty = DependencyProperty.RegisterAttached(
        "Index",
        typeof(string),
        typeof(NavigationButtonProperties),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty IsCurrentProperty = DependencyProperty.RegisterAttached(
        "IsCurrent",
        typeof(bool),
        typeof(NavigationButtonProperties),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static void SetIndex(DependencyObject element, string value) =>
        element.SetValue(IndexProperty, value);

    public static string GetIndex(DependencyObject element) =>
        (string)element.GetValue(IndexProperty);

    public static void SetIsCurrent(DependencyObject element, bool value) =>
        element.SetValue(IsCurrentProperty, value);

    public static bool GetIsCurrent(DependencyObject element) =>
        (bool)element.GetValue(IsCurrentProperty);
}
