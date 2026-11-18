namespace ActivityNotifier.App.Ui;

/// <summary>
/// The tray part of the app: an icon in the notification area with a right-click menu.
/// <c>Application.Run</c> keeps the UI thread alive until <see cref="ApplicationContext.ExitThread"/> is called.
/// </summary>
/// <remarks>M3: only an Exit item. M4 adds Pause, the tooltip, and toasts.</remarks>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _trayIcon;

    /// <summary>Creates the tray icon and its menu. Must be called on the UI thread.</summary>
    public TrayApplicationContext()
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Exit", image: null, OnExitClicked);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Activity Notifier",
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void ExitThreadCore()
    {
        // Hide the icon now; otherwise Windows can show a "ghost" icon until the mouse passes over it.
        _trayIcon.Visible = false;
        base.ExitThreadCore();
    }

    // UI event handler: runs on the UI thread. Ending the message loop makes Application.Run return in Program.Main.
    private void OnExitClicked(object? sender, EventArgs e) => ExitThread();
}
