using EvePreview.Core;

namespace EvePreview.Windows;

public sealed class ProcessSnapshotMonitor : IProcessSnapshotMonitor
{
    private readonly IProcessProvider _provider;

    public ProcessSnapshotMonitor() : this(new SystemProcessProvider())
    {
    }

    internal ProcessSnapshotMonitor(IProcessProvider provider)
    {
        _provider = provider;
    }

    public IReadOnlyList<ProcessSnapshot> GetSnapshot(IEnumerable<string> configuredProcessNames)
    {
        ArgumentNullException.ThrowIfNull(configuredProcessNames);
        var snapshots = new List<ProcessSnapshot>();
        var processNames = configuredProcessNames
            .Select(NormalizeProcessName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var processName in processNames)
        {
            IReadOnlyList<IProcessAdapter> processes;
            try
            {
                processes = _provider.GetProcesses(processName);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                continue;
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    nint mainWindowHandle;
                    try
                    {
                        mainWindowHandle = process.MainWindowHandle;
                        if (mainWindowHandle == 0 ||
                            !string.Equals(NormalizeProcessName(process.ProcessName), processName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        snapshots.Add(new ProcessSnapshot(
                            process.ProcessId,
                            process.ProcessName,
                            mainWindowHandle,
                            process.MainWindowTitle,
                            process.ProcessStartTime));
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        // A process may exit, deny access, or fail between enumeration and property access.
                        // Each process is isolated so the rest of the snapshot still succeeds.
                    }
                }
            }
        }

        return snapshots.OrderBy(snapshot => snapshot.ProcessId).ToArray();
    }

    private static string NormalizeProcessName(string? configuredName) =>
        string.IsNullOrWhiteSpace(configuredName)
            ? string.Empty
            : ProcessNameNormalizer.Normalize(configuredName);
}
