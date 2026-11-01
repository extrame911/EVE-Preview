namespace EvePreview.Core;

/// <summary>Moves per-client configuration when a client window title changes.</summary>
public static class ClientSettingsMigrator
{
    /// <summary>
    /// Renames the matching client entry and updates cycle-group references. If an entry for the
    /// new title already exists, the old entry is removed instead of leaving a stale offline item.
    /// </summary>
    public static bool MigrateTitle(AppSettings settings, string previousTitle, string currentTitle)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(previousTitle) ||
            string.IsNullOrWhiteSpace(currentTitle) ||
            string.Equals(previousTitle, currentTitle, StringComparison.Ordinal))
        {
            return false;
        }

        var previousSettings = FindClient(settings, previousTitle);
        if (previousSettings is null)
        {
            return false;
        }

        var currentSettings = FindClient(settings, currentTitle);
        if (currentSettings is not null && !ReferenceEquals(previousSettings, currentSettings))
        {
            settings.Clients.Remove(previousSettings);
        }
        else
        {
            previousSettings.OriginalTitle = currentTitle;
        }

        foreach (var group in settings.CycleGroups)
        {
            ReplaceTitle(group.OrderedClientTitles, previousTitle, currentTitle);
            ReplaceTitle(group.ExcludedTitles, previousTitle, currentTitle);
        }

        return true;
    }

    private static ClientSettings? FindClient(AppSettings settings, string title) =>
        settings.Clients.FirstOrDefault(client =>
            string.Equals(client.OriginalTitle, title, StringComparison.OrdinalIgnoreCase));

    private static void ReplaceTitle(List<string> titles, string previousTitle, string currentTitle)
    {
        var changed = false;
        for (var index = 0; index < titles.Count; index++)
        {
            if (string.Equals(titles[index], previousTitle, StringComparison.OrdinalIgnoreCase))
            {
                titles[index] = currentTitle;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        var unique = titles
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Select(title => title.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        titles.Clear();
        titles.AddRange(unique);
    }
}
