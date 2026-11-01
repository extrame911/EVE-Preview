namespace EvePreview.Windows;

public sealed record ProcessSnapshot(
    int ProcessId,
    string ProcessName,
    nint MainWindowHandle,
    string MainWindowTitle,
    DateTime ProcessStartTime);

public interface IProcessSnapshotMonitor
{
    IReadOnlyList<ProcessSnapshot> GetSnapshot(IEnumerable<string> configuredProcessNames);
}

internal interface IProcessProvider
{
    IReadOnlyList<IProcessAdapter> GetProcesses(string processName);
}

internal interface IProcessAdapter : IDisposable
{
    int ProcessId { get; }
    string ProcessName { get; }
    nint MainWindowHandle { get; }
    string MainWindowTitle { get; }
    DateTime ProcessStartTime { get; }
}
