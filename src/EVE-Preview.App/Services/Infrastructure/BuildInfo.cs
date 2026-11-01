using System.Reflection;

namespace EvePreview.App.Services;

public static class BuildInfo
{
    public static string BuildDate { get; } = ResolveBuildDate();

    private static string ResolveBuildDate() =>
        typeof(BuildInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "BuildDate", StringComparison.Ordinal))
            ?.Value ?? "unknown";
}
