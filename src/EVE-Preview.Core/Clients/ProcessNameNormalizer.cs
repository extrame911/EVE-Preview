namespace EvePreview.Core;

public static class ProcessNameNormalizer
{
    public static string Normalize(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        normalized = normalized[(normalized.LastIndexOf('/') + 1)..];
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized.ToLowerInvariant();
    }
}
