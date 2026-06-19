using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace CoffeeManiaVPN.Helpers;

public sealed class TrayIconService : IDisposable
{
    private readonly Window _window;
    private readonly Action _onConnectToggle;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _connectMenuItem;
    private bool _isConnected;

    public bool IsExiting { get; private set; }

    public TrayIconService(Window window, Action onConnectToggle)
    {
        _window = window;
        _onConnectToggle = onConnectToggle;

        _connectMenuItem = new ToolStripMenuItem("Подключить", null, (_, _) => _onConnectToggle());

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem("Открыть", null, (_, _) => ShowWindow()));
        contextMenu.Items.Add(_connectMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Выход", null, (_, _) => RequestExit()));

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "КОФЕМАНИЯ ВПН — отключено",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    public void UpdateConnectionState(bool connected)
    {
        _isConnected = connected;
        _connectMenuItem.Text = connected ? "Отключить" : "Подключить";
        _notifyIcon.Text = connected
            ? "КОФЕМАНИЯ ВПН — подключено"
            : "КОФЕМАНИЯ ВПН — отключено";
    }

    public void HideToTray()
    {
        _window.Hide();
        _window.ShowInTaskbar = false;
        _window.WindowState = WindowState.Normal;
    }

    public void ShowWindow()
    {
        _window.ShowInTaskbar = true;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void RequestExit()
    {
        IsExiting = true;
        _window.Close();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Icon LoadTrayIcon()
    {
        var icoPath = Path.Combine(AppPaths.AssetsDirectory, "app.ico");
        if (!File.Exists(icoPath))
            return SystemIcons.Application;

        using var stream = File.OpenRead(icoPath);
        return new Icon(stream);
    }
}
