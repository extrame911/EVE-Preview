namespace EvePreview.Core;

public sealed record ClientSnapshot(ClientIdentity Identity, string Title);

public sealed record ClientSnapshotDiff(
    IReadOnlyList<ClientSnapshot> Added,
    IReadOnlyList<ClientSnapshot> Removed,
    IReadOnlyList<ClientSnapshot> Updated);

public sealed class ClientSnapshotDiffer
{
    public ClientSnapshotDiff Compare(
        IEnumerable<ClientSnapshot> previous,
        IEnumerable<ClientSnapshot> current)
    {
        var oldByIdentity = previous.ToDictionary(snapshot => snapshot.Identity);
        var newByIdentity = current.ToDictionary(snapshot => snapshot.Identity);

        return new ClientSnapshotDiff(
            current.Where(snapshot => !oldByIdentity.ContainsKey(snapshot.Identity)).ToArray(),
            previous.Where(snapshot => !newByIdentity.ContainsKey(snapshot.Identity)).ToArray(),
            current.Where(snapshot =>
                oldByIdentity.TryGetValue(snapshot.Identity, out var oldSnapshot) &&
                oldSnapshot != snapshot).ToArray());
    }
}
