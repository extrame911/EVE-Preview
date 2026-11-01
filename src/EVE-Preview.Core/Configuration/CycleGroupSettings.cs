namespace EvePreview.Core;

public sealed class CycleGroupSettings
{
    public string ForwardHotKey { get; set; } = string.Empty;
    public string BackwardHotKey { get; set; } = string.Empty;
    public List<string> OrderedClientTitles { get; set; } = [];
    public List<string> ExcludedTitles { get; set; } = [];
}
