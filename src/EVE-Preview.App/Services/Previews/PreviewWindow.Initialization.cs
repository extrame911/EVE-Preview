using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EvePreview.Core;
using Image = System.Windows.Controls.Image;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    private void ConfigureWindow(PreviewPlacement placement)
    {
        Title = DisplayName;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        AllowsTransparency = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Background = Brushes.Transparent;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
    }

    private (Image GdiImage, CompactPreviewSurface CompactSurface, Grid PrivacyOverlay) BuildVisualTree()
    {
        var root = new Grid { Background = Brushes.Transparent };
        var gdiImage = new Image { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
        root.Children.Add(gdiImage);
        var compactSurface = new CompactPreviewSurface();
        root.Children.Add(compactSurface);

        var privacyOverlay = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(0x17, 0x1E, 0x26)),
            Visibility = Visibility.Collapsed
        };
        privacyOverlay.Children.Add(new TextBlock
        {
            Text = "隐私遮挡已启用\n原始画面未显示",
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xB4)),
            FontFamily = AppTypography.Family,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
        root.Children.Add(privacyOverlay);
        Content = root;
        SetPreviewTooltip();
        return (gdiImage, compactSurface, privacyOverlay);
    }

    private void SubscribeEvents()
    {
        Loaded += (_, _) =>
        {
            UpdateCompactState();
            UpdateThumbnailBounds();
        };
        LocationChanged += (_, _) => SyncOverlay();
        DpiChanged += (_, _) => HandleDpiChange();
        IsVisibleChanged += (_, _) => SyncOverlay();
        SizeChanged += (_, _) =>
        {
            UpdateCompactState();
            UpdateThumbnailBounds();
            UpdateGdiImageSize();
            SyncOverlay();
        };
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseRightButtonUp += OnMouseRightButtonUp;
        MouseMove += OnMouseMove;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        Deactivated += (_, _) => ResetPointerState();
        SourceInitialized += OnSourceInitialized;
    }

    private (DispatcherTimer ActiveHide, DispatcherTimer FocusLost, DispatcherTimer ZoomDelay) CreateDelayTimers()
    {
        var activeHide = new DispatcherTimer(DispatcherPriority.Background);
        activeHide.Tick += OnActiveHideTick;
        var focusLost = new DispatcherTimer(DispatcherPriority.Background);
        focusLost.Tick += OnFocusLostTick;
        var zoomDelay = new DispatcherTimer(DispatcherPriority.Input);
        zoomDelay.Tick += OnZoomDelayTick;
        return (activeHide, focusLost, zoomDelay);
    }

    private void HandleDpiChange()
    {
        if (_zoomed || _zoomPending)
        {
            RestoreZoom();
        }

        if (_useDefaultPhysicalSize)
        {
            ApplyDefaultPhysicalSize(_settings);
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        var margins = new Margins
        {
            Left = -1,
            Right = -1,
            Top = -1,
            Bottom = -1
        };
        DwmExtendFrameIntoClientArea(handle, ref margins);
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
        {
            target.BackgroundColor = Colors.Transparent;
        }

        if (_useDefaultPhysicalSize)
        {
            ApplyDefaultPhysicalSize(_settings);
        }
    }
}
