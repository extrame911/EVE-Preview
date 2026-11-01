using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using EvePreview.Core;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace EvePreview.App.Services;

/// <summary>
/// DWM thumbnails are composed above the destination window's WPF content. This click-through
/// companion window therefore owns only non-interactive overlay pixels above the raw preview.
/// </summary>
public sealed class PreviewOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020;
    private const long WsExToolWindow = 0x00000080;
    private const long WsExNoActivate = 0x08000000;
    private readonly Border _border;
    private readonly Border _titlePanel;
    private readonly TextBlock _title;

    public PreviewOverlayWindow(Window owner)
    {
        Owner = owner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        IsHitTestVisible = false;
        Title = "EVE-Preview Overlay";

        var root = new Grid();
        _border = new Border
        {
            BorderThickness = new Thickness(0),
            Margin = new Thickness(1),
            IsHitTestVisible = false
        };

        _titlePanel = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false
        };
        _title = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontFamily = AppTypography.BoldFamily,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            FontStretch = FontStretches.SemiCondensed,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF6, 0xF8))
        };
        _titlePanel.Child = _title;
        root.Children.Add(_titlePanel);
        root.Children.Add(_border);
        Content = root;
        SourceInitialized += (_, _) => MakeClickThrough();
    }

    public void Sync(Window owner)
    {
        Left = owner.Left;
        Top = owner.Top;
        Width = owner.Width;
        Height = owner.Height;
        Topmost = owner.Topmost;
    }

    public void UpdateAppearance(
        AppSettings settings,
        bool active,
        int thumbnailOpacity,
        bool contentVisible,
        string? highlightColor = null,
        bool compact = false)
    {
        _title.Text = TitleSource;
        _title.FontSize = Math.Clamp(settings.Overlay.FontSize, 10, 32);
        var (textColor, outlineColor) = PreviewTextPalette.Resolve(settings.Overlay.TextColor);
        _title.Foreground = new SolidColorBrush(textColor);
        _title.Effect = new DropShadowEffect
        {
            Color = outlineColor,
            BlurRadius = 1.5,
            ShadowDepth = 0,
            Opacity = 1
        };
        _titlePanel.Visibility = settings.Overlay.ShowName && !settings.Overlay.PrivacyMask && !compact
            ? Visibility.Visible
            : Visibility.Collapsed;
        _titlePanel.VerticalAlignment = settings.Overlay.Position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight => VerticalAlignment.Top,
            OverlayPosition.BottomLeft or OverlayPosition.BottomCenter or OverlayPosition.BottomRight => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center
        };
        _titlePanel.HorizontalAlignment = settings.Overlay.Position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft => HorizontalAlignment.Left,
            OverlayPosition.TopCenter or OverlayPosition.Center or OverlayPosition.BottomCenter => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Right
        };

        var highlight = active && settings.Overlay.HighlightActive;
        var showBorder = highlight;
        _border.BorderThickness = showBorder
            ? new Thickness(highlight ? Math.Clamp(settings.Overlay.HighlightThickness, 1, 8) : 1)
            : new Thickness(0);
        _border.BorderBrush = ParseBrush(
            highlight
                ? string.IsNullOrWhiteSpace(highlightColor)
                    ? settings.Overlay.HighlightColor
                    : highlightColor
                : "#526170",
            "#4CC2FF");
        var contentOpacity = Math.Clamp(thumbnailOpacity / 255d, 0.1, 1.0);
        _titlePanel.Opacity = contentOpacity;
        _border.Opacity = showBorder ? 0.8 : 0;
        Opacity = contentVisible ? 1 : 0;
    }

    public string TitleSource { get; set; } = string.Empty;

    private void MakeClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(
            handle,
            GwlExStyle,
            new nint(extendedStyle | WsExTransparent | WsExToolWindow | WsExNoActivate));
    }

    private static Brush ParseBrush(string value, string fallback) =>
        new SolidColorBrush(ParseColor(value, fallback));

    private static Color ParseColor(string value, string fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }
        catch (FormatException)
        {
            return (Color)ColorConverter.ConvertFromString(fallback);
        }
        catch (NotSupportedException)
        {
            return (Color)ColorConverter.ConvertFromString(fallback);
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);
}
