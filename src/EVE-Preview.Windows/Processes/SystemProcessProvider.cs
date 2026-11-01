using System.Diagnostics;

namespace EvePreview.Windows;

internal sealed class SystemProcessProvider : IProcessProvider
{
    public IReadOnlyList<IProcessAdapter> GetProcesses(string processName) =>
        Process.GetProcessesByName(processName)
            .Select(process => (IProcessAdapter)new SystemProcessAdapter(process))
            .ToArray();
}

internal sealed class SystemProcessAdapter(Process process) : IProcessAdapter
{
    public int ProcessId => process.Id;
    public string ProcessName => process.ProcessName;
    public nint MainWindowHandle => process.MainWindowHandle;
    public string MainWindowTitle => process.MainWindowTitle;
    public DateTime ProcessStartTime => process.StartTime;
    public void Dispose() => process.Dispose();
}
