using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using EvePreview.App.Services;
using Forms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace EvePreview.App.Controls;

public partial class ColorPickerBox : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor),
        typeof(string),
        typeof(ColorPickerBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedColorChanged));

    public static readonly DependencyProperty AllowEmptyProperty = DependencyProperty.Register(
        nameof(AllowEmpty),
        typeof(bool),
        typeof(ColorPickerBox),
        new PropertyMetadata(false, OnAllowEmptyChanged));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText),
        typeof(string),
        typeof(ColorPickerBox),
        new PropertyMetadata("默认", OnAllowEmptyChanged));

    public ColorPickerBox()
    {
        InitializeComponent();
        ClearButton.Content = LocalizationManager.Translate("清空");
        UpdatePreview();
    }

    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public bool AllowEmpty
    {
        get => (bool)GetValue(AllowEmptyProperty);
        set => SetValue(AllowEmptyProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ColorPickerBox)d).UpdatePreview();

    private static void OnAllowEmptyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ColorPickerBox)d).UpdatePreview();

    private void UpdatePreview()
    {
        var value = SelectedColor;
        var hasValue = !string.IsNullOrWhiteSpace(value);
        ValueText.Text = hasValue ? value.ToUpperInvariant() : LocalizationManager.Translate(EmptyText);
        Swatch.Background = TryParse(value, out var color)
            ? new SolidColorBrush(color)
            : Brushes.Transparent;
        ClearButton.Visibility = AllowEmpty && hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPickClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            FullOpen = true,
            Color = TryParse(SelectedColor, out var current)
                ? System.Drawing.Color.FromArgb(current.R, current.G, current.B)
                : System.Drawing.Color.FromArgb(0x4C, 0xC2, 0xFF)
        };

        var owner = Window.GetWindow(this);
        var result = owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(new WindowOwner(owner));
        if (result != Forms.DialogResult.OK)
        {
            return;
        }

        SelectedColor = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
    }

    private void OnClearClick(object sender, RoutedEventArgs e) => SelectedColor = string.Empty;

    private static bool TryParse(string? value, out MediaColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            color = (MediaColor)MediaColorConverter.ConvertFromString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private sealed class WindowOwner : Forms.IWin32Window
    {
        public WindowOwner(Window window)
        {
            Handle = new WindowInteropHelper(window).Handle;
        }

        public IntPtr Handle { get; }
    }
}
