using System.ComponentModel;
using System.Windows;
using QZ.Frp.Client.ViewModels;
using Wpf.Ui.Controls;

namespace QZ.Frp.Client.Views;

public partial class MainWindow : FluentWindow
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
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

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ShowChangePasswordDialog();
    }
}
