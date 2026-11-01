using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace EvePreview.App.Services;

public sealed class LocalizationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var source = value switch
        {
            ComboBoxItem item => item.Content as string,
            string text => text,
            _ => null
        };
        return string.IsNullOrWhiteSpace(source)
            ? value
            : LocalizationManager.Translate(source);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
