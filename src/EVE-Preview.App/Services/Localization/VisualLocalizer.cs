using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace EvePreview.App.Services;

/// <summary>Applies the localization catalog to loaded WPF text and button content.</summary>
internal static class VisualLocalizer
{
    private static readonly ConditionalWeakTable<FrameworkElement, ElementState> States = new();
    private static readonly List<WeakReference<FrameworkElement>> Attached = [];
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        EventManager.RegisterClassHandler(
            typeof(TextBlock),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTextBlockLoaded),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(Button),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnContentControlLoaded),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(CheckBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnContentControlLoaded),
            handledEventsToo: true);
    }

    public static void Refresh()
    {
        foreach (var element in SnapshotAttached())
        {
            Apply(element);
        }
    }

    public static void RefreshFrom(DependencyObject root)
    {
        if (root is TextBlock textBlock)
        {
            Attach(textBlock, () => textBlock.Text, value => textBlock.Text = value);
        }
        else if (root is ContentControl control)
        {
            Attach(control, () => control.Content as string, value => control.Content = value);
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            RefreshFrom(VisualTreeHelper.GetChild(root, index));
        }
    }

    private static string Translate(string source) => LocalizationManager.Translate(source);

    private static void OnTextBlockLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is TextBlock textBlock)
        {
            Attach(textBlock, () => textBlock.Text, value => textBlock.Text = value);
        }
    }

    private static void OnContentControlLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is ContentControl control)
        {
            Attach(control, () => control.Content as string, value => control.Content = value);
        }
    }

    private static void Attach(FrameworkElement element, Func<string?> getter, Action<string> setter)
    {
        if (States.TryGetValue(element, out _))
        {
            Apply(element);
            return;
        }

        var property = element is TextBlock ? TextBlock.TextProperty : ContentControl.ContentProperty;
        if (BindingOperations.GetBindingExpression(element, property) is not null ||
            BindingOperations.GetMultiBindingExpression(element, property) is not null)
        {
            return;
        }

        var text = getter();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var state = new ElementState(text, getter, setter);
        States.Add(element, state);
        Attached.Add(new WeakReference<FrameworkElement>(element));
        var descriptor = DependencyPropertyDescriptor.FromProperty(property, element.GetType());
        if (descriptor is null)
        {
            States.Remove(element);
            return;
        }

        state.ValueChanged = (_, _) => OnValueChanged(element, state);
        descriptor.AddValueChanged(element, state.ValueChanged);
        state.Detach = () => descriptor.RemoveValueChanged(element, state.ValueChanged);
        element.Unloaded += (_, _) =>
        {
            state.Detach?.Invoke();
            state.Detach = null;
        };
        Apply(element);
    }

    private static void OnValueChanged(FrameworkElement element, ElementState state)
    {
        if (state.Applying)
        {
            return;
        }

        var current = state.Getter();
        if (current is null)
        {
            return;
        }

        state.Original = current;
        Apply(element);
    }

    private static void Apply(FrameworkElement element)
    {
        if (!States.TryGetValue(element, out var state) || state.Applying)
        {
            return;
        }

        state.Applying = true;
        try
        {
            state.Setter(Translate(state.Original));
        }
        finally
        {
            state.Applying = false;
        }
    }

    private static IEnumerable<FrameworkElement> SnapshotAttached()
    {
        foreach (var reference in Attached.ToArray())
        {
            if (reference.TryGetTarget(out var element))
            {
                yield return element;
            }
        }
    }

    private sealed class ElementState(string original, Func<string?> getter, Action<string> setter)
    {
        public string Original { get; set; } = original;
        public Func<string?> Getter { get; } = getter;
        public Action<string> Setter { get; } = setter;
        public bool Applying { get; set; }
        public EventHandler? ValueChanged { get; set; }
        public Action? Detach { get; set; }
    }
}
