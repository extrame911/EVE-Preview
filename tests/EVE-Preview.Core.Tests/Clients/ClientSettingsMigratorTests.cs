using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class ClientSettingsMigratorTests
{
    [Fact]
    public void Renames_client_and_updates_cycle_group_references()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - Old", Alias = "Main" });
        settings.CycleGroups[0].OrderedClientTitles = ["EVE - Old", "EVE - Other"];
        settings.CycleGroups[1].ExcludedTitles = ["EVE - Old"];

        var migrated = ClientSettingsMigrator.MigrateTitle(settings, "EVE - Old", "EVE - New");

        Assert.True(migrated);
        Assert.Equal("EVE - New", Assert.Single(settings.Clients).OriginalTitle);
        Assert.Equal("Main", settings.Clients[0].Alias);
        Assert.Equal(["EVE - New", "EVE - Other"], settings.CycleGroups[0].OrderedClientTitles);
        Assert.Equal(["EVE - New"], settings.CycleGroups[1].ExcludedTitles);
    }

    [Fact]
    public void Removes_stale_old_entry_when_new_title_already_exists()
    {
        var settings = new AppSettings();
        settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - Old" });
        settings.Clients.Add(new ClientSettings { OriginalTitle = "EVE - New", Alias = "Keep" });

        var migrated = ClientSettingsMigrator.MigrateTitle(settings, "EVE - Old", "EVE - New");

        Assert.True(migrated);
        var client = Assert.Single(settings.Clients);
        Assert.Equal("EVE - New", client.OriginalTitle);
        Assert.Equal("Keep", client.Alias);
    }

    [Fact]
    public void Unknown_title_is_a_no_op()
    {
        var settings = new AppSettings();

        Assert.False(ClientSettingsMigrator.MigrateTitle(settings, "EVE - Old", "EVE - New"));
        Assert.Empty(settings.Clients);
    }
}
