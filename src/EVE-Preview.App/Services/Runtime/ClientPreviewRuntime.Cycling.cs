using EvePreview.Core;

namespace EvePreview.App.Services;

public sealed partial class ClientPreviewRuntime
{
    private async Task CycleAsync(int groupIndex, CycleDirection direction)
    {
        if (groupIndex < 0 || groupIndex >= _settings.CycleGroups.Count)
        {
            return;
        }

        var groupSettings = _settings.CycleGroups[groupIndex];
        var availableClients = _previews.Values.Select(preview => new CycleEntry(
            new ClientSnapshot(preview.Identity, preview.Client.MainWindowTitle),
            FindClientSettings(preview.Client.MainWindowTitle)?.ExcludeFromCycle == true));
        var group = CycleGroup.FromOrderedTitles(
            availableClients,
            groupSettings.OrderedClientTitles,
            groupSettings.ExcludedTitles);
        var next = group.SelectNext(_activeIdentity, direction);
        if (next is not null)
        {
            await ActivateClientCoreAsync(next, CancellationToken.None);
        }
    }
}
