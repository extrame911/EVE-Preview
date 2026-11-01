using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class HotKeyParserTests
{
    [Fact]
    public void ParsesCanonicalKeyboardCombinationToVirtualKey()
    {
        Assert.True(HotKeyParser.TryParse("shift+ctrl+f4", out var hotkey));

        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, hotkey.Modifiers);
        Assert.Equal(0x73, hotkey.VirtualKey);
        Assert.Equal(HotKeyMouseButton.None, hotkey.MouseButton);
        Assert.Equal("Ctrl+Shift+F4", hotkey.Canonical);
    }

    [Fact]
    public void ParsesMouseSideButtonWithoutVirtualKey()
    {
        Assert.True(HotKeyParser.TryParse("alt+XButton2", out var hotkey));

        Assert.Equal(KeyModifiers.Alt, hotkey.Modifiers);
        Assert.Equal(0, hotkey.VirtualKey);
        Assert.Equal(HotKeyMouseButton.XButton2, hotkey.MouseButton);
        Assert.Equal("Alt+XButton2", hotkey.Canonical);
    }

    [Theory]
    [InlineData("Ctrl+Shift+Back", 0x08, "Ctrl+Shift+Back")]
    [InlineData("Ctrl+Enter", 0x0D, "Ctrl+Enter")]
    [InlineData("Alt+Space", 0x20, "Alt+Space")]
    [InlineData("Ctrl+Left", 0x25, "Ctrl+Left")]
    [InlineData("Escape", 0x1B, "Escape")]
    [InlineData("Ctrl+5", 0x35, "Ctrl+5")]
    public void ParsesNamedKeysUsedByDefaultBindings(string text, int virtualKey, string canonical)
    {
        Assert.True(HotKeyParser.TryParse(text, out var hotkey));

        Assert.Equal(virtualKey, hotkey.VirtualKey);
        Assert.Equal(canonical, hotkey.Canonical);
    }

    [Fact]
    public void EmptyValueIsDisabledRatherThanInvalid()
    {
        Assert.True(HotKeyParser.TryParse(" ", out var hotkey));

        Assert.Equal(HotKeyGesture.None, hotkey);
    }

    [Theory]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+NotAKey")]
    [InlineData("A+B")]
    [InlineData("XButton3")]
    public void RejectsMalformedCombinations(string text)
    {
        Assert.False(HotKeyParser.TryParse(text, out _));
    }
}
