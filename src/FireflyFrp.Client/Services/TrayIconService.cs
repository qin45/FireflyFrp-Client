using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Wpf.Ui.Appearance;

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

        icon.ContextMenu = BuildMenu();
        icon.TrayLeftMouseDoubleClick += (_, _) => _showMain();
        icon.ForceCreate();
        _icon = icon;

        // The tray ContextMenu is created once and caches the theme brushes it
        // resolved at load time, so rebuild it whenever the app theme changes.
        ThemeService.Changed += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_icon is null)
        {
            return;
        }
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.BeginInvoke(() => RebuildMenu());
            return;
        }
        RebuildMenu();
    }

    private void RebuildMenu()
    {
        if (_icon is not null)
        {
            _icon.ContextMenu = BuildMenu();
        }
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        // A tray ContextMenu lives in its own popup root, so the WPF-UI theme
        // resources do not reliably reach it. Bind the surfaces to the app
        // palette explicitly so the menu follows light/dark mode.
        menu.SetResourceReference(FrameworkElement.StyleProperty, "UiContextMenu");
        // Use the same brushes as WPF-UI submenus so the first-level menu and
        // its submenus share one surface color.
        menu.SetResourceReference(Control.BackgroundProperty, "ContextMenuBackground");
        menu.SetResourceReference(Control.ForegroundProperty, "ContextMenuForeground");
        menu.SetResourceReference(Control.BorderBrushProperty, "ContextMenuBorderBrush");
        // Popups outside the window tree do not inherit the app theme scope.
        // Copy the current themed dictionaries into the menu so its template
        // picks up the dark/light brushes.
        ApplicationThemeManager.Apply(menu);
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
        return menu;
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
        ThemeService.Changed -= OnThemeChanged;
        _icon?.Dispose();
        _icon = null;
    }
}
