namespace EvePreview.Core;

public enum CycleDirection
{
    Forward,
    Backward
}

public sealed record CycleEntry(ClientSnapshot Client, bool IsExcluded = false);

public sealed class CycleGroup
{
    private readonly IReadOnlyList<CycleEntry> _entries;

    public CycleGroup(
        IEnumerable<CycleEntry> entries,
        IEnumerable<string>? excludedTitles = null)
    {
        var titleExclusions = new HashSet<string>(
            excludedTitles ?? [], StringComparer.OrdinalIgnoreCase);
        _entries = entries
            .Where(entry => !entry.IsExcluded && !titleExclusions.Contains(entry.Client.Title))
            .GroupBy(entry => entry.Client.Identity)
            .Select(group => group.First())
            .ToArray();
    }

    public static CycleGroup FromOrderedTitles(
        IEnumerable<CycleEntry> availableClients,
        IEnumerable<string> orderedTitles,
        IEnumerable<string>? excludedTitles = null)
    {
        var byTitle = availableClients
            .GroupBy(entry => entry.Client.Title)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var ordered = new List<CycleEntry>();
        foreach (var title in orderedTitles)
        {
            if (byTitle.Remove(title, out var entry))
            {
                ordered.Add(entry);
            }
        }

        return new CycleGroup(ordered, excludedTitles);
    }

    public ClientIdentity? SelectNext(ClientIdentity? current, CycleDirection direction)
    {
        if (_entries.Count == 0)
        {
            return null;
        }

        var currentIndex = -1;
        for (var index = 0; index < _entries.Count; index++)
        {
            if (_entries[index].Client.Identity == current)
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return _entries[direction == CycleDirection.Forward ? 0 : _entries.Count - 1].Client.Identity;
        }

        var step = direction == CycleDirection.Forward ? 1 : -1;
        var nextIndex = ((currentIndex + step) % _entries.Count + _entries.Count) % _entries.Count;
        return _entries[nextIndex].Client.Identity;
    }
}
