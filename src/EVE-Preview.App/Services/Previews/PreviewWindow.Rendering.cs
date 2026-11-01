using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    private string? StartRendering()
    {
        if (_privacyActive)
        {
            _privacyOverlay.Visibility = Visibility.Visible;
            return null;
        }

        if (_settings.Compatibility.RenderingMode == RenderingMode.Gdi)
        {
            StartGdiMode();
            return null;
        }

        try
        {
            _thumbnail = new DwmThumbnail(_client.MainWindowHandle, new WindowInteropHelper(this).Handle);
            UpdateThumbnailBounds();
            return null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or COMException or ArgumentException)
        {
            if (_settings.Compatibility.UseGdiFallback)
            {
                StartGdiMode();
                return $"DWM 预览不可用，已回退到 GDI 兼容模式：{exception.Message}";
            }

            return $"DWM 预览不可用：{exception.Message}";
        }
    }

    private void ApplyPrivacyMode(bool privacy)
    {
        if (_privacyActive == privacy)
        {
            return;
        }

        _privacyActive = privacy;
        if (privacy)
        {
            _thumbnail?.Dispose();
            _thumbnail = null;
            _gdiTimer?.Stop();
            _gdiTimer = null;
            _gdiImage.Source = null;
            _gdiImage.Visibility = Visibility.Collapsed;
            _privacyOverlay.Visibility = Visibility.Visible;
        }
        else if (_initialized)
        {
            _privacyOverlay.Visibility = Visibility.Collapsed;
            _ = StartRendering();
        }
    }

    private void SyncOverlay()
    {
        if (_overlay is null)
        {
            return;
        }

        var clientSettings = FindClientSettings();
        var privacy = _settings.Overlay.PrivacyMask || clientSettings?.PrivacyMask == true;
        var effectiveSettings = privacy == _settings.Overlay.PrivacyMask
            ? _settings
            : WithClientPrivacy(_settings);
        _overlay.TitleSource = GetOverlayTitle();
        var contentVisible = Visibility == Visibility.Visible && IsVisible && !_paused;
        _overlay.UpdateAppearance(
            effectiveSettings,
            _active,
            (int)Math.Round(Math.Clamp(_settings.Display.Opacity, 0.1, 1.0) * 255),
            contentVisible,
            GetEffectiveHighlightColor(clientSettings),
            IsCompactPreview);
        if (contentVisible)
        {
            if (!_overlay.IsVisible)
            {
                _overlay.Show();
            }

            _overlay.Sync(this);
        }
        else
        {
            _overlay.Hide();
        }
    }

    private bool IsCompactPreview =>
        CompactPreviewSurface.ShouldUse(ActualWidth, ActualHeight);

    private void UpdateCompactState()
    {
        var compact = IsCompactPreview;
        var clientSettings = FindClientSettings();
        var compactColor = GetEffectiveHighlightColor(clientSettings);
        _compactSurface.Update(
            compact,
            ParseBrush(compactColor, "#F3F6F8"),
            _paused ? 0 : Math.Clamp(_settings.Display.Opacity, 0.1, 1.0));

        if (_gdiTimer is not null)
        {
            _gdiImage.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private string GetOverlayTitle()
    {
        if (!_settings.Overlay.ShowCharacterNameOnly || HasAlias)
        {
            return DisplayName;
        }

        return ClientWindowTitle.TrySplit(_client.MainWindowTitle, out _, out var characterName) &&
               !string.IsNullOrWhiteSpace(characterName)
            ? characterName
            : DisplayName;
    }

    private static AppSettings WithClientPrivacy(AppSettings settings) =>
        new()
        {
            Overlay = new OverlayOptions
            {
                ShowName = settings.Overlay.ShowName,
                ShowCharacterNameOnly = settings.Overlay.ShowCharacterNameOnly,
                FontFamily = settings.Overlay.FontFamily,
                FontSize = settings.Overlay.FontSize,
                TextColor = settings.Overlay.TextColor,
                Position = settings.Overlay.Position,
                HighlightActive = settings.Overlay.HighlightActive,
                HighlightColor = settings.Overlay.HighlightColor,
                HighlightThickness = settings.Overlay.HighlightThickness,
                PrivacyMask = true
            }
        };

    private void UpdateThumbnailBounds()
    {
        if (_disposed || _thumbnail is null || ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var bounds = new WindowBounds(
            0,
            0,
            Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY)));
        var opacity = IsCompactPreview
            ? (byte)0
            : (byte)Math.Round(Math.Clamp(_settings.Display.Opacity, 0.1, 1.0) * 255);
        try
        {
            _thumbnail.Update(new DwmThumbnailProperties(bounds, opacity, true, true));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or COMException or ArgumentException)
        {
            _thumbnail.Dispose();
            _thumbnail = null;
            if (!_privacyActive && _settings.Compatibility.UseGdiFallback)
            {
                StartGdiMode();
            }
        }
    }

    private void StartGdiMode()
    {
        _thumbnail?.Dispose();
        _thumbnail = null;
        _gdiImage.Visibility = Visibility.Visible;
        _gdiTimer ??= new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1d / Math.Clamp(_settings.Compatibility.GdiFramesPerSecond, 1, 30))
        };
        _gdiTimer.Tick -= OnGdiTick;
        _gdiTimer.Tick += OnGdiTick;
        _gdiTimer.Start();
        UpdateCompactState();
        _ = CaptureGdiFrameAsync();
    }

    private void OnGdiTick(object? sender, EventArgs e) => _ = CaptureGdiFrameAsync();

    private async Task CaptureGdiFrameAsync()
    {
        if (_disposed || _paused || _privacyActive || Visibility != Visibility.Visible ||
            Interlocked.Exchange(ref _gdiCaptureInFlight, 1) != 0)
        {
            return;
        }

        try
        {
            var windowHandle = _client.MainWindowHandle;
            var source = await Task.Run(() =>
            {
                using var bitmap = new GdiScreenshotService().Capture(windowHandle);
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    var image = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        0,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    return image;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }).ConfigureAwait(true);

            if (!_disposed && !_paused && !_privacyActive && Visibility == Visibility.Visible)
            {
                _gdiImage.Source = source;
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            ArgumentException or
            ExternalException or
            OperationCanceledException)
        {
            // A transient client-exit, shutdown, or capture failure is retried on the next frame.
        }
        finally
        {
            Volatile.Write(ref _gdiCaptureInFlight, 0);
        }
    }

    private void UpdateGdiImageSize()
    {
        if (_gdiImage.Source is BitmapSource source && ActualWidth > 0)
        {
            _gdiImage.Width = ActualWidth;
            _gdiImage.Height = ActualHeight;
        }
    }
}
