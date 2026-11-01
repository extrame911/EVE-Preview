using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class ClientWindowTitleTests
{
    [Theory]
    [InlineData("星战前夜：晨曦 [Infinity]", "星战前夜：晨曦 [Infinity]", null)]
    [InlineData("EVE Online", "EVE Online", null)]
    [InlineData("EVE Online - Main Character", "EVE Online", "Main Character")]
    [InlineData("星战前夜：晨曦 [Infinity] - 上不完的班", "星战前夜：晨曦 [Infinity]", "上不完的班")]
    [InlineData("EVE Online — Main Character", "EVE Online", "Main Character")]
    [InlineData("  EVE Online  -  Main Character  ", "EVE Online", "Main Character")]
    [InlineData("EVE Online - ", "EVE Online", null)]
    [InlineData("EVE-Preview Mock Client", "EVE-Preview Mock Client", null)]
    public void Splits_title_into_game_title_and_character_name(
        string title,
        string expectedGameTitle,
        string? expectedCharacterName)
    {
        var result = ClientWindowTitle.TrySplit(title, out var gameTitle, out var characterName);

        Assert.True(result);
        Assert.Equal(expectedGameTitle, gameTitle);
        Assert.Equal(expectedCharacterName, characterName);
        Assert.Equal(expectedCharacterName is null, ClientWindowTitle.IsLoginWindow(title));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" - Character")]
    public void Rejects_titles_without_a_game_title(string title)
    {
        Assert.False(ClientWindowTitle.TrySplit(title, out _, out _));
        Assert.False(ClientWindowTitle.IsLoginWindow(title));
    }
}
