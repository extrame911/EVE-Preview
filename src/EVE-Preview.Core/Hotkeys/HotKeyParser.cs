namespace EvePreview.Core;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public enum HotKeyMouseButton
{
    None = 0,
    XButton1 = 1,
    XButton2 = 2
}

public readonly record struct HotKeyGesture(
    KeyModifiers Modifiers,
    int VirtualKey,
    HotKeyMouseButton MouseButton,
    string Canonical)
{
    public static HotKeyGesture None { get; } = new(KeyModifiers.None, 0, HotKeyMouseButton.None, string.Empty);
}

public static class HotKeyParser
{
    private static readonly IReadOnlyDictionary<string, (int VirtualKey, string Canonical)> NamedKeys =
        new Dictionary<string, (int VirtualKey, string Canonical)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Back"] = (0x08, "Back"),
            ["Backspace"] = (0x08, "Back"),
            ["Tab"] = (0x09, "Tab"),
            ["Clear"] = (0x0C, "Clear"),
            ["Enter"] = (0x0D, "Enter"),
            ["Return"] = (0x0D, "Enter"),
            ["Pause"] = (0x13, "Pause"),
            ["CapsLock"] = (0x14, "CapsLock"),
            ["Escape"] = (0x1B, "Escape"),
            ["Esc"] = (0x1B, "Escape"),
            ["Space"] = (0x20, "Space"),
            ["PageUp"] = (0x21, "PageUp"),
            ["Prior"] = (0x21, "PageUp"),
            ["PageDown"] = (0x22, "PageDown"),
            ["Next"] = (0x22, "PageDown"),
            ["End"] = (0x23, "End"),
            ["Home"] = (0x24, "Home"),
            ["Left"] = (0x25, "Left"),
            ["Up"] = (0x26, "Up"),
            ["Right"] = (0x27, "Right"),
            ["Down"] = (0x28, "Down"),
            ["Insert"] = (0x2D, "Insert"),
            ["Delete"] = (0x2E, "Delete"),
            ["Del"] = (0x2E, "Delete"),
            ["NumPad0"] = (0x60, "NumPad0"),
            ["NumPad1"] = (0x61, "NumPad1"),
            ["NumPad2"] = (0x62, "NumPad2"),
            ["NumPad3"] = (0x63, "NumPad3"),
            ["NumPad4"] = (0x64, "NumPad4"),
            ["NumPad5"] = (0x65, "NumPad5"),
            ["NumPad6"] = (0x66, "NumPad6"),
            ["NumPad7"] = (0x67, "NumPad7"),
            ["NumPad8"] = (0x68, "NumPad8"),
            ["NumPad9"] = (0x69, "NumPad9"),
            ["Multiply"] = (0x6A, "Multiply"),
            ["Add"] = (0x6B, "Add"),
            ["Subtract"] = (0x6D, "Subtract"),
            ["Decimal"] = (0x6E, "Decimal"),
            ["Divide"] = (0x6F, "Divide")
        };

    public static bool TryParse(string? value, out HotKeyGesture hotkey)
    {
        hotkey = HotKeyGesture.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var modifiers = KeyModifiers.None;
        string? keyText = null;
        foreach (var rawPart in value.Split('+'))
        {
            var part = rawPart.Trim();
            if (part.Length == 0)
            {
                return false;
            }

            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Control;
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Alt;
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Shift;
                continue;
            }

            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Windows;
                continue;
            }

            if (keyText is not null || !TryParseKey(part, out var virtualKey, out var canonical, out var mouseButton))
            {
                return false;
            }

            keyText = part;
            hotkey = new HotKeyGesture(modifiers, virtualKey, mouseButton, canonical);
        }

        if (keyText is null)
        {
            return false;
        }

        var canonicalBuilder = new List<string>(5);
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            canonicalBuilder.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            canonicalBuilder.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            canonicalBuilder.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Windows))
        {
            canonicalBuilder.Add("Win");
        }

        canonicalBuilder.Add(hotkey.Canonical);
        hotkey = hotkey with { Modifiers = modifiers, Canonical = string.Join('+', canonicalBuilder) };
        return true;
    }

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (!TryParse(value, out var hotkey))
        {
            return false;
        }

        normalized = hotkey.Canonical;
        return true;
    }

    private static bool TryParseKey(
        string value,
        out int virtualKey,
        out string canonical,
        out HotKeyMouseButton mouseButton)
    {
        virtualKey = 0;
        canonical = string.Empty;
        mouseButton = HotKeyMouseButton.None;

        if (value.Equals("XButton1", StringComparison.OrdinalIgnoreCase))
        {
            mouseButton = HotKeyMouseButton.XButton1;
            canonical = "XButton1";
            return true;
        }

        if (value.Equals("XButton2", StringComparison.OrdinalIgnoreCase))
        {
            mouseButton = HotKeyMouseButton.XButton2;
            canonical = "XButton2";
            return true;
        }

        if (NamedKeys.TryGetValue(value, out var named))
        {
            virtualKey = named.VirtualKey;
            canonical = named.Canonical;
            return true;
        }

        if (value.Length == 1 && char.IsAsciiLetterOrDigit(value[0]))
        {
            var upper = char.ToUpperInvariant(value[0]);
            virtualKey = upper;
            canonical = upper.ToString();
            return true;
        }

        if (value.Length is 2 or 3 &&
            value.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value.AsSpan(1), out var functionKey) &&
            functionKey is >= 1 and <= 24)
        {
            virtualKey = 0x6F + functionKey;
            canonical = $"F{functionKey}";
            return true;
        }

        return false;
    }
}
