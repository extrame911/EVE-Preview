namespace EvePreview.Core;

/// <summary>Splits EVE client window titles into the server/game title and the character name.</summary>
public static class ClientWindowTitle
{
    private static readonly char[] Separators = ['-', '–', '—'];

    /// <summary>
    /// A window title is "game title - character name". The game title differs between Chinese
    /// and English clients, so it is never hard-coded. No character name means login state.
    /// Whitespace around both parts is removed.
    /// </summary>
    public static bool TrySplit(string? title, out string gameTitle, out string? characterName)
    {
        gameTitle = string.Empty;
        characterName = null;
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var separatorIndex = FindSeparator(title);
        if (separatorIndex < 0)
        {
            gameTitle = title.Trim();
            return gameTitle.Length > 0;
        }

        gameTitle = title[..separatorIndex].Trim();
        var character = title[(separatorIndex + 1)..].Trim();
        characterName = character.Length == 0 ? null : character;
        return gameTitle.Length > 0;
    }

    public static bool IsLoginWindow(string? title) =>
        TrySplit(title, out _, out var characterName) && characterName is null;

    /// <summary>
    /// Finds a dash used as a title separator. A dash without whitespace on either side is part
    /// of the game title or character name, not a separator.
    /// </summary>
    private static int FindSeparator(string title)
    {
        for (var index = 0; index < title.Length; index++)
        {
            if (Array.IndexOf(Separators, title[index]) < 0)
            {
                continue;
            }

            var hasLeftWhitespace = index == 0 || char.IsWhiteSpace(title[index - 1]);
            var hasRightWhitespace = index == title.Length - 1 || char.IsWhiteSpace(title[index + 1]);
            if (hasLeftWhitespace || hasRightWhitespace)
            {
                return index;
            }
        }

        return -1;
    }
}
