using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EvePreview.Core;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    private void SetPreviewTooltip()
    {
        var (textColor, outlineColor) = PreviewTextPalette.Resolve(_settings.Overlay.TextColor);
        var textBrush = new SolidColorBrush(textColor);
        var outlineBrush = new SolidColorBrush(outlineColor);

        _windowToolTip ??= new System.Windows.Controls.ToolTip
        {
            Padding = new Thickness(8, 4, 8, 4),
            MaxWidth = 600
        };
        _contentToolTip ??= new System.Windows.Controls.ToolTip
        {
            Padding = new Thickness(8, 4, 8, 4),
            MaxWidth = 600
        };

        foreach (var toolTip in new[] { _windowToolTip, _contentToolTip })
        {
            var text = _client.MainWindowTitle;
            if (HasAlias)
            {
                var aliasLine = $"{LocalizationManager.Translate("别名")}: {_alias}";
                text = string.IsNullOrWhiteSpace(text)
                    ? aliasLine
                    : $"{text}{Environment.NewLine}{aliasLine}";
            }

            toolTip.Content = new TextBlock
            {
                Text = text,
                Foreground = textBrush,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 600
            };
            toolTip.Background = outlineBrush;
            toolTip.Foreground = textBrush;
            toolTip.BorderBrush = textBrush;
        }

        ToolTip = _windowToolTip;
        if (Content is FrameworkElement content)
        {
            content.ToolTip = _contentToolTip;
        }
    }

    private ClientSettings? FindClientSettings() =>
        _settings.Clients.FirstOrDefault(client =>
            string.Equals(client.OriginalTitle, _client.MainWindowTitle, StringComparison.OrdinalIgnoreCase));

    private static Brush ParseBrush(string value, string fallback)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }
        catch (FormatException)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback));
        }
        catch (NotSupportedException)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback));
        }
    }
    private string GetEffectiveHighlightColor(ClientSettings? clientSettings) =>
        string.IsNullOrWhiteSpace(clientSettings?.HighlightColor)
            ? _settings.Overlay.HighlightColor
            : clientSettings.HighlightColor;
}
