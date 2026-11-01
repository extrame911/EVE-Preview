using System.IO;
using System.Windows.Threading;
using EvePreview.Core;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime
{
    public async Task ApplySettingsAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ThrowIfDisposed();
        settings.Normalize();
        var validation = settings.Validate();
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        await _dispatcher.InvokeAsync(
            () => ApplySettingsCore(settings),
            DispatcherPriority.Normal,
            cancellationToken);
        ScheduleSave();
    }

    private void ApplySettingsCore(
        AppSettings settings,
        bool updatePlacementsForDefaultSizeChange = true)
    {
        var previousMode = _appliedRenderingMode;
        var defaultSizeChanged = _appliedDefaultWidth != settings.Preview.DefaultWidth ||
                                 _appliedDefaultHeight != settings.Preview.DefaultHeight;
        var placementPolicyChanged = _appliedHideWhenOutOfBounds != settings.Display.HideWhenOutOfBounds ||
                                     _appliedExpansionThreshold != settings.Display.ExpansionThreshold;
        _settings = settings;
        if (previousMode != settings.Compatibility.RenderingMode)
        {
            RebuildPreviewWindows();
        }
        else
        {
            foreach (var preview in _previews.Values)
            {
                ApplySettingsToPreview(
                    preview,
                    defaultSizeChanged,
                    placementPolicyChanged,
                    updatePlacementsForDefaultSizeChange);
            }
        }

        _appliedRenderingMode = settings.Compatibility.RenderingMode;
        _appliedDefaultWidth = settings.Preview.DefaultWidth;
        _appliedDefaultHeight = settings.Preview.DefaultHeight;
        _appliedHideWhenOutOfBounds = settings.Display.HideWhenOutOfBounds;
        _appliedExpansionThreshold = settings.Display.ExpansionThreshold;
        RebuildHotkeys();
        PublishSnapshot(null);
    }

    private void ApplySettingsToPreview(
        PreviewWindow preview,
        bool defaultSizeChanged,
        bool placementPolicyChanged,
        bool updatePlacementsForDefaultSizeChange = true)
    {
        var clientSettings = FindClientSettings(preview.Client.MainWindowTitle);
        if (defaultSizeChanged)
        {
            var size = preview.ApplyDefaultPhysicalSize(_settings);
            if (updatePlacementsForDefaultSizeChange && clientSettings?.Placement is not null)
            {
                clientSettings.Placement = size;
            }
        }
        else if (placementPolicyChanged)
        {
            preview.ApplyPlacement(preview.Placement);
        }

        var alias = string.IsNullOrWhiteSpace(clientSettings?.Alias) ? null : clientSettings.Alias;
        preview.UpdateDisplayName(alias ?? preview.Client.MainWindowTitle, alias);
        preview.ApplySettings(_settings, preview.Identity == _activeIdentity);
    }
}
