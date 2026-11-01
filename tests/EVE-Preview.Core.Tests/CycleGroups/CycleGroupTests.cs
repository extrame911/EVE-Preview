using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class CycleGroupTests
{
    private static ClientSnapshot Client(int id, string title) =>
        new(new ClientIdentity(id, id + 100, DateTime.UnixEpoch), title);

    [Fact]
    public void OrderedGroupNeverSelectsOnlineClientsOutsideItsConfiguredList()
    {
        var a = Client(1, "EVE - A");
        var outside = Client(2, "EVE - Outside");
        var c = Client(3, "EVE - C");

        var group = CycleGroup.FromOrderedTitles(
            [new CycleEntry(a), new CycleEntry(outside), new CycleEntry(c)],
            ["EVE - A", "EVE - C"]);

        Assert.Equal(c.Identity, group.SelectNext(a.Identity, CycleDirection.Forward));
        Assert.Equal(c.Identity, group.SelectNext(a.Identity, CycleDirection.Backward));
        Assert.Equal(a.Identity, group.SelectNext(c.Identity, CycleDirection.Forward));
    }

    [Fact]
    public void ForwardSelectionFollowsConfiguredOrder()
    {
        var a = Client(1, "EVE - A");
        var b = Client(2, "EVE - B");
        var c = Client(3, "EVE - C");
        var group = new CycleGroup([new CycleEntry(a), new CycleEntry(b), new CycleEntry(c)]);

        var next = group.SelectNext(a.Identity, CycleDirection.Forward);

        Assert.Equal(b.Identity, next);
    }

    [Fact]
    public void BackwardSelectionFromFirstWrapsToLast()
    {
        var a = Client(1, "EVE - A");
        var b = Client(2, "EVE - B");
        var c = Client(3, "EVE - C");
        var group = new CycleGroup([new CycleEntry(a), new CycleEntry(b), new CycleEntry(c)]);

        var next = group.SelectNext(a.Identity, CycleDirection.Backward);

        Assert.Equal(c.Identity, next);
    }

    [Fact]
    public void DuplicateClientKeepsOnlyItsFirstPosition()
    {
        var a = Client(1, "EVE - A");
        var b = Client(2, "EVE - B");
        var c = Client(3, "EVE - C");
        var group = new CycleGroup([new CycleEntry(a), new CycleEntry(b), new CycleEntry(a), new CycleEntry(c)]);

        var next = group.SelectNext(b.Identity, CycleDirection.Forward);

        Assert.Equal(c.Identity, next);
    }

    [Fact]
    public void ExplicitlyExcludedClientIsSkipped()
    {
        var a = Client(1, "EVE - A");
        var b = Client(2, "EVE - B");
        var c = Client(3, "EVE - C");
        var group = new CycleGroup([
            new CycleEntry(a), new CycleEntry(b, IsExcluded: true), new CycleEntry(c)]);

        var next = group.SelectNext(a.Identity, CycleDirection.Forward);

        Assert.Equal(c.Identity, next);
    }

    [Fact]
    public void TitleExcludedClientIsSkippedCaseInsensitively()
    {
        var a = Client(1, "EVE - A");
        var login = Client(2, "EVE - Login");
        var c = Client(3, "EVE - C");
        var group = new CycleGroup(
            [new CycleEntry(a), new CycleEntry(login), new CycleEntry(c)],
            ["eve - login"]);

        var next = group.SelectNext(a.Identity, CycleDirection.Forward);

        Assert.Equal(c.Identity, next);
    }

    [Fact]
    public void BackwardSelectionWithoutCurrentClientStartsAtLastEntry()
    {
        var a = Client(1, "EVE - A");
        var b = Client(2, "EVE - B");
        var c = Client(3, "EVE - C");
        var group = new CycleGroup([new CycleEntry(a), new CycleEntry(b), new CycleEntry(c)]);

        var next = group.SelectNext(null, CycleDirection.Backward);

        Assert.Equal(c.Identity, next);
    }

    [Fact]
    public void BackwardSelectionFromOutsideGroupStartsAtLastEntry()
    {
        var a = Client(1, "EVE - A");
        var b = Client(2, "EVE - B");
        var c = Client(3, "EVE - C");
        var group = new CycleGroup([new CycleEntry(a), new CycleEntry(b), new CycleEntry(c)]);
        var outside = Client(9, "EVE - Outside");

        var next = group.SelectNext(outside.Identity, CycleDirection.Backward);

        Assert.Equal(c.Identity, next);
    }
}
