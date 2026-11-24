using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace FireflyFrp.Client.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Action _showMain;
    private readonly Action _createTunnel;
    private readonly Func<Task> _stopAll;
    private readonly Action<string> _setCloseAction;
    private readonly Action _exit;
    private TaskbarIcon? _icon;

    public TrayIconService(
        Action showMain,
        Action createTunnel,
        Func<Task> stopAll,
        Action<string> setCloseAction,
        Action exit)
    {
        _showMain = showMain;
        _createTunnel = createTunnel;
        _stopAll = stopAll;
        _setCloseAction = setCloseAction;
        _exit = exit;
    }

    public void Create()
    {
        var icon = new TaskbarIcon
        {
            ToolTipText = "FireflyFrp",
            IconSource = new BitmapImage(
                new Uri("pack://application:,,,/Assets/Icons/app.ico", UriKind.Absolute)),
        };

        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("显示主窗口", _showMain));
        menu.Items.Add(MenuItem("新建隧道", _createTunnel));
        menu.Items.Add(MenuItem("停止所有隧道", _stopAll));

        var closeMenu = new MenuItem { Header = "关闭行为" };
        closeMenu.Items.Add(MenuItem("每次询问", () => _setCloseAction("ask")));
        closeMenu.Items.Add(MenuItem("最小化到托盘", () => _setCloseAction("tray")));
        closeMenu.Items.Add(MenuItem("直接退出", () => _setCloseAction("exit")));
        menu.Items.Add(closeMenu);

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("退出", _exit));

        icon.ContextMenu = menu;
        icon.TrayLeftMouseDoubleClick += (_, _) => _showMain();
        icon.ForceCreate();
        _icon = icon;
    }

    public void SetTooltip(string text)
    {
        if (_icon is not null)
        {
            _icon.ToolTipText = text;
        }
    }

    public void ShowInfo(string message)
    {
        try
        {
            _icon?.ShowNotification(
                "FireflyFrp",
                message,
                NotificationIcon.Info,
                null,
                true,
                true,
                false,
                false,
                null);
        }
        catch
        {
            // Notifications are optional.
        }
    }

    private static MenuItem MenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem MenuItem(string header, Func<Task> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += async (_, _) => await action();
        return item;
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
