using System.Reflection;

namespace EvePreview.App.Services;

public static class BuildInfo
{
    public static string BuildDate { get; } = ResolveBuildDate();

    public static string Version { get; } =
        typeof(BuildInfo).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    private static string ResolveBuildDate() =>
        typeof(BuildInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "BuildDate", StringComparison.Ordinal))
            ?.Value ?? "unknown";
}
