using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class ClientSnapshotDifferTests
{
    [Fact]
    public void HandleReuseProducesRemovalAndAdditionInsteadOfUpdate()
    {
        var start = DateTime.Parse("2026-01-01T00:00:00Z");
        var before = new ClientSnapshot(new ClientIdentity(42, 1234, start), "EVE - One");
        var after = before with { Identity = before.Identity with { ProcessStartTimeUtc = start.AddMinutes(1) } };

        var diff = new ClientSnapshotDiffer().Compare([before], [after]);

        Assert.Equal([before], diff.Removed);
        Assert.Equal([after], diff.Added);
        Assert.Empty(diff.Updated);
    }

    [Fact]
    public void TitleChangeProducesUpdateForSameIdentity()
    {
        var identity = new ClientIdentity(42, 1234, DateTime.Parse("2026-01-01T00:00:00Z"));
        var before = new ClientSnapshot(identity, "EVE - One");
        var after = before with { Title = "EVE - Renamed" };

        var diff = new ClientSnapshotDiffer().Compare([before], [after]);

        Assert.Equal([after], diff.Updated);
        Assert.Empty(diff.Added);
        Assert.Empty(diff.Removed);
    }
}
