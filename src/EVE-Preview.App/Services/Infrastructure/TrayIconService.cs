using System.Drawing;
using System.Windows.Forms;

namespace EvePreview.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _togglePreviewItem;
    private readonly Func<Task> _togglePreview;
    private readonly Icon _icon;
    private readonly bool _ownsIcon;
    private bool _disposed;

    public TrayIconService(Action showMainWindow, Func<Task> togglePreview, Action exit, Icon? configuredIcon = null)
    {
        ArgumentNullException.ThrowIfNull(showMainWindow);
        ArgumentNullException.ThrowIfNull(togglePreview);
        ArgumentNullException.ThrowIfNull(exit);
        _togglePreview = togglePreview;
        _icon = configuredIcon ?? SystemIcons.Application;
        _ownsIcon = !ReferenceEquals(_icon, SystemIcons.Application);

        var contextMenu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("显示主窗口");
        showItem.Click += (_, _) => showMainWindow();
        _togglePreviewItem = new ToolStripMenuItem("启动预览");
        _togglePreviewItem.Click += (_, _) => _ = TogglePreviewSafelyAsync();
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => exit();
        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(_togglePreviewItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "EVE-Preview",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
        _notifyIcon.DoubleClick += (_, _) => showMainWindow();
    }

    public void SetPreviewRunning(bool running)
    {
        _togglePreviewItem.Text = running ? "停止预览" : "启动预览";
    }

    private async Task TogglePreviewSafelyAsync()
    {
        try
        {
            await _togglePreview().ConfigureAwait(true);
        }
        catch
        {
            // The main view model owns error reporting; the tray must remain usable.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        if (_ownsIcon)
        {
            _icon.Dispose();
        }
    }
}
