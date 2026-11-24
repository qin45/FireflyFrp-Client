using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using FireflyFrp.Client.Services;
using FireflyFrp.Client.ViewModels;
using Wpf.Ui.Controls;

namespace FireflyFrp.Client.Views;

public partial class MainWindow : FluentWindow
{
    private const string ReleasesUrl = "https://github.com/qin45/FireflyFrp-Client/releases";
    private const string LicenseUrl = "https://github.com/qin45/FireflyFrp-Client/blob/master/LICENSE";

    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        UpdateThemeMenuItem();
        ThemeService.Changed += OnThemeChanged;
        Closed += (_, _) => ThemeService.Changed -= OnThemeChanged;
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        var app = (App)Application.Current;
        if (app.IsExiting)
        {
            _allowClose = true;
            return;
        }

        e.Cancel = true;
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var action = viewModel.Session.Settings.CloseAction;
        if (action == "ask")
        {
            var dialog = new CloseActionDialog { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Choice == CloseActionChoice.Cancel)
            {
                return;
            }
            action = dialog.Choice == CloseActionChoice.Exit ? "exit" : "tray";
            if (dialog.Remember)
            {
                viewModel.Session.Settings.CloseAction = action;
                viewModel.Session.SettingsStore.Save();
            }
        }

        if (action == "tray")
        {
            Hide();
            app.NotifyMinimizedToTray();
            return;
        }

        _allowClose = true;
        await app.ExitAppAsync();
    }

    private void NewTunnel_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ShowCreateTunnelDialog();
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (MoreButton.Flyout is ContextMenu menu)
        {
            menu.PlacementTarget = MoreButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        CloseMoreMenu();
        ((App)Application.Current).ShowChangePasswordDialog();
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        CloseMoreMenu();
        ((App)Application.Current).ToggleTheme();
        UpdateThemeMenuItem();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateThemeMenuItem();
    }

    private void UpdateThemeMenuItem()
    {
        if (ThemeMenuItem is not null)
        {
            ThemeMenuItem.Header = ThemeService.GetToggleLabel();
        }
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CloseMoreMenu();
        OpenUrl(ReleasesUrl);
    }

    private void OpenLicenses_Click(object sender, RoutedEventArgs e)
    {
        CloseMoreMenu();
        OpenUrl(LicenseUrl);
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        CloseMoreMenu();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.LogoutCommand.Execute(null);
        }
    }

    private void CloseMoreMenu()
    {
        if (MoreButton.Flyout is ContextMenu menu)
        {
            menu.IsOpen = false;
        }
        MoreButton.IsDropDownOpen = false;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"无法打开浏览器：{ex.Message}",
                "FireflyFrp",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }
}
