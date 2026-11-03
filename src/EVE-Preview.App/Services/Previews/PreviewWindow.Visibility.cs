using System.Windows;
using EvePreview.Core;

namespace EvePreview.App.Services;

public sealed partial class PreviewWindow
{
    public void SetFocused(bool focused)
    {
        if (_focused == focused)
        {
            return;
        }

        _focused = focused;
        RefreshVisibilityTimers();
        UpdateLayoutForDisplay();
        SyncOverlay();
    }

    /// <summary>
    /// One focus flag drives both visibility options: focused previews use the active delay,
    /// unfocused previews use the lost-focus delay. The two options are inverses.
    /// </summary>
    private void RefreshVisibilityTimers()
    {
        _activeHideTimer.Stop();
        _focusLostTimer.Stop();
        _activeHideElapsed = false;
        _focusLostElapsed = false;

        if (_focused)
        {
            if (!_settings.Display.HideActiveClient)
            {
                return;
            }

            var delay = Math.Clamp(_settings.Display.HideActiveDelayMs, 0, 10000);
            if (delay == 0)
            {
                _activeHideElapsed = true;
            }
            else
            {
                _activeHideTimer.Interval = TimeSpan.FromMilliseconds(delay);
                _activeHideTimer.Start();
            }

            return;
        }

        if (!_settings.Display.HideOnFocusLoss)
        {
            return;
        }

        var lostDelay = Math.Clamp(_settings.Display.HideDelayMs, 0, 10000);
        if (lostDelay == 0)
        {
            _focusLostElapsed = true;
        }
        else
        {
            _focusLostTimer.Interval = TimeSpan.FromMilliseconds(lostDelay);
            _focusLostTimer.Start();
        }
    }

    private void OnActiveHideTick(object? sender, EventArgs e)
    {
        _activeHideTimer.Stop();
        _activeHideElapsed = true;
        UpdateLayoutForDisplay();
        SyncOverlay();
    }

    private void OnFocusLostTick(object? sender, EventArgs e)
    {
        _focusLostTimer.Stop();
        _focusLostElapsed = true;
        UpdateLayoutForDisplay();
        SyncOverlay();
    }

    private void UpdateLayoutForDisplay()
    {
        var clientSettings = FindClientSettings();
        var hiddenForActive = _settings.Display.HideActiveClient && _focused && _activeHideElapsed;
        var hiddenForLostFocus = _settings.Display.HideOnFocusLoss && !_focused && _focusLostElapsed;
        var hiddenForLogin = ClientWindowTitle.IsLoginWindow(_client.MainWindowTitle);
        var hiddenForClient = clientSettings?.DisablePreview == true;
        var shouldShow = !hiddenForActive && !hiddenForLostFocus &&
                         !hiddenForLogin && !hiddenForClient;
        Visibility = shouldShow ? Visibility.Visible : Visibility.Hidden;
    }
}
