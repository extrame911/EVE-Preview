using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow : Window, IDisposable
{
    private readonly Action<PreviewWindow, PreviewClickAction> _clickAction;
    private readonly Action<PreviewWindow, PreviewPlacement> _placementChanged;
    private readonly Grid _privacyOverlay;
    private readonly Image _gdiImage;
    private readonly CompactPreviewSurface _compactSurface;
    private readonly DispatcherTimer _activeHideTimer;
    private readonly DispatcherTimer _focusLostTimer;
    private readonly DispatcherTimer _zoomDelayTimer;
    private ProcessSnapshot _client;
    private string? _alias;
    private System.Windows.Controls.ToolTip? _windowToolTip;
    private System.Windows.Controls.ToolTip? _contentToolTip;
    private DwmThumbnail? _thumbnail;
    private PreviewOverlayWindow? _overlay;
    private DispatcherTimer? _gdiTimer;
    private int _gdiCaptureInFlight;
    private AppSettings _settings;
    private PreviewPlacement _placement;
    private PreviewPlacement _basePlacement;
    private bool _initialized;
    private bool _active;
    private bool _focused;
    private bool _activeHideElapsed;
    private bool _focusLostElapsed;
    private bool _paused;
    private bool _privacyActive;
    private bool _leftDown;
    private bool _rightDown;
    private bool _resizing;
    private bool _zoomed;
    private bool _zoomPending;
    private bool _useDefaultPhysicalSize;
    private bool _suppressClick;
    private bool _disposed;
    private Point _dragStart;
    private double _dragLeft;
    private double _dragTop;
    private double _dragWidth;
    private double _dragHeight;

    public PreviewWindow(
        ProcessSnapshot client,
        string displayName,
        AppSettings settings,
        PreviewPlacement placement,
        Action<PreviewWindow, PreviewClickAction> clickAction,
        Action<PreviewWindow, PreviewPlacement> placementChanged,
        bool useDefaultPhysicalSize = false)
    {
        _client = client;
        _settings = settings;
        _useDefaultPhysicalSize = useDefaultPhysicalSize;
        _placement = placement;
        _basePlacement = placement;
        _clickAction = clickAction;
        _placementChanged = placementChanged;
        DisplayName = displayName;
        _alias = string.Equals(displayName, client.MainWindowTitle, StringComparison.Ordinal)
            ? null
            : displayName;

        ConfigureWindow(placement);
        (_gdiImage, _compactSurface, _privacyOverlay) = BuildVisualTree();
        SubscribeEvents();
        (_activeHideTimer, _focusLostTimer, _zoomDelayTimer) = CreateDelayTimers();
        ApplySettings(settings, active: false);
    }

    public ProcessSnapshot Client => _client;
    public string DisplayName { get; private set; }
    public ClientIdentity Identity => new(
        _client.ProcessId,
        _client.MainWindowHandle,
        _client.ProcessStartTime.ToUniversalTime());
    public PreviewPlacement Placement => _placement;
    public bool IsActiveClient => _active;
    public bool IsPreviewWindowVisible => Visibility == Visibility.Visible;
    private bool HasAlias => !string.IsNullOrWhiteSpace(_alias);

    public void ShowPreview()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (_overlay is null)
        {
            var overlay = new PreviewOverlayWindow(this)
            {
                TitleSource = DisplayName
            };
            try
            {
                overlay.Show();
                _overlay = overlay;
                SyncOverlay();
            }
            catch
            {
                _overlay = null;
                try
                {
                    overlay.Close();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                }

                throw;
            }
        }
    }

    public bool TryInitialize(out string? error)
    {
        if (_initialized)
        {
            error = null;
            return true;
        }

        _initialized = true;
        error = StartRendering();
        return error is null || _thumbnail is not null || _gdiTimer is not null;
    }
}
