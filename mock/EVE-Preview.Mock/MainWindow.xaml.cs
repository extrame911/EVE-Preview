using System.Windows;
using System.Windows.Threading;

namespace EvePreview.Mock;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();
        ProcessText.Text = $"Process ID: {Environment.ProcessId} | Process name: ExeFile | {DateTime.Now:yyyy-MM-dd}";
        UpdateClock(null, EventArgs.Empty);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += UpdateClock;
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void UpdateClock(object? sender, EventArgs e)
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss.fff");
    }
}
