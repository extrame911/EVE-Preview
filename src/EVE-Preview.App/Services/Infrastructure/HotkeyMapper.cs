using EvePreview.Core;
using EvePreview.Windows;

namespace EvePreview.App.Services;

public static class HotkeyMapper
{
    public static bool TryMap(string? text, out Hotkey hotkey)
    {
        hotkey = new Hotkey(HotkeyModifiers.None);
        if (!HotKeyParser.TryParse(text, out var gesture) || gesture == HotKeyGesture.None ||
            gesture.MouseButton != HotKeyMouseButton.None)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        if (gesture.Modifiers.HasFlag(KeyModifiers.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (gesture.Modifiers.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (gesture.Modifiers.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (gesture.Modifiers.HasFlag(KeyModifiers.Windows))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        var mouseButton = gesture.MouseButton switch
        {
            HotKeyMouseButton.XButton1 => MouseSideButton.XButton1,
            HotKeyMouseButton.XButton2 => MouseSideButton.XButton2,
            _ => MouseSideButton.None
        };
        hotkey = new Hotkey(modifiers, gesture.VirtualKey, mouseButton);
        return true;
    }
}
