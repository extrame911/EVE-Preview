using EvePreview.App.Services;
using EvePreview.Windows;

namespace EvePreview.App.Tests;

public sealed class HotkeyMapperTests
{
    [Fact]
    public void Maps_keyboard_combination_to_native_registration_values()
    {
        Assert.True(HotkeyMapper.TryMap("Ctrl+Shift+F4", out var hotkey));

        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, hotkey.Modifiers);
        Assert.Equal(0x73, hotkey.VirtualKey);
        Assert.Equal(MouseSideButton.None, hotkey.MouseButton);
    }

    [Fact]
    public void Mouse_side_button_is_not_registrable()
    {
        Assert.False(HotkeyMapper.TryMap("Alt+XButton2", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ctrl+Shift")]
    [InlineData("NotAKey")]
    public void Empty_and_invalid_text_are_not_registrable(string? text)
    {
        Assert.False(HotkeyMapper.TryMap(text, out _));
    }
}
