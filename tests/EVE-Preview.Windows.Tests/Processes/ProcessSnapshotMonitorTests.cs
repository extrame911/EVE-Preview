using System.ComponentModel;

namespace EvePreview.Windows.Tests;

public sealed class ProcessSnapshotMonitorTests
{
    [Fact]
    public void GetSnapshot_process_exit_does_not_abort_the_round()
    {
        var started = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        using var first = new FakeProcess(11, "exefile", (nint)101, "EVE - One", started);
        using var exited = new FakeProcess(12, "exefile", 0, "", started)
        {
            MainWindowHandleError = new InvalidOperationException("Process has exited.")
        };
        using var last = new FakeProcess(13, "exefile", (nint)103, "EVE - Two", started);
        var monitor = new ProcessSnapshotMonitor(new FakeProcessProvider(
            ("exefile", [first, exited, last])));

        var snapshots = monitor.GetSnapshot(["ExeFile.exe"]);

        Assert.Equal([11, 13], snapshots.Select(snapshot => snapshot.ProcessId));
        Assert.Equal([(nint)101, (nint)103], snapshots.Select(snapshot => snapshot.MainWindowHandle));
        Assert.All(snapshots, snapshot => Assert.Equal(started, snapshot.ProcessStartTime));
    }

    [Fact]
    public void GetSnapshot_returns_only_configured_processes_with_main_windows()
    {
        var started = DateTime.UtcNow;
        using var match = new FakeProcess(21, "exefile", (nint)201, "EVE", started);
        using var other = new FakeProcess(22, "other", (nint)202, "Other", started);
        using var noWindow = new FakeProcess(23, "exefile", 0, "", started);
        var monitor = new ProcessSnapshotMonitor(new FakeProcessProvider(
            ("exefile", [match, other, noWindow])));

        var snapshot = Assert.Single(monitor.GetSnapshot(["exefile"]));

        Assert.Equal(21, snapshot.ProcessId);
    }

    [Fact]
    public void GetSnapshot_access_denied_is_isolated_and_processes_are_disposed()
    {
        var started = DateTime.UtcNow;
        var denied = new FakeProcess(31, "exefile", 0, "", started)
        {
            MainWindowHandleError = new Win32Exception(5, "Access is denied.")
        };
        var match = new FakeProcess(32, "exefile", (nint)302, "EVE", started);
        var monitor = new ProcessSnapshotMonitor(new FakeProcessProvider(
            ("exefile", [denied, match])));

        var snapshot = Assert.Single(monitor.GetSnapshot(["exefile"]));

        Assert.Equal(32, snapshot.ProcessId);
        Assert.True(denied.IsDisposed);
        Assert.True(match.IsDisposed);
    }

    [Fact]
    public void GetSnapshot_provider_failure_for_one_process_name_does_not_abort_other_names()
    {
        var started = DateTime.UtcNow;
        using var match = new FakeProcess(41, "other", (nint)401, "Other client", started);
        var monitor = new ProcessSnapshotMonitor(new FaultingProcessProvider(
            ("exefile", new InvalidOperationException("enumeration failed")),
            ("other", (IReadOnlyList<IProcessAdapter>)[match])));

        var snapshot = Assert.Single(monitor.GetSnapshot(["exefile", "other"]));

        Assert.Equal(41, snapshot.ProcessId);
    }

    private sealed class FaultingProcessProvider(
        params (string Name, object Result)[] entries) : IProcessProvider
    {
        public IReadOnlyList<IProcessAdapter> GetProcesses(string processName)
        {
            var result = entries.Single(entry => entry.Name == processName).Result;
            return result is Exception exception
                ? throw exception
                : (IReadOnlyList<IProcessAdapter>)result;
        }
    }

    private sealed class FakeProcessProvider(params (string Name, IReadOnlyList<IProcessAdapter> Processes)[] entries)
        : IProcessProvider
    {
        public IReadOnlyList<IProcessAdapter> GetProcesses(string processName) =>
            entries.Single(entry => string.Equals(entry.Name, processName, StringComparison.OrdinalIgnoreCase)).Processes;
    }

    private sealed class FakeProcess(
        int processId,
        string processName,
        nint mainWindowHandle,
        string mainWindowTitle,
        DateTime processStartTime) : IProcessAdapter
    {
        public Exception? MainWindowHandleError { get; init; }

        public int ProcessId => processId;
        public string ProcessName => processName;
        public nint MainWindowHandle => MainWindowHandleError is null
            ? mainWindowHandle
            : throw MainWindowHandleError;
        public string MainWindowTitle => mainWindowTitle;
        public DateTime ProcessStartTime => processStartTime;
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }
}
