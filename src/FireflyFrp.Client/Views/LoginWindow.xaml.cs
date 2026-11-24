using System.ComponentModel;
using System.Windows;
using QZ.Frp.Client.ViewModels;
using Wpf.Ui.Controls;

namespace QZ.Frp.Client.Views;

public partial class LoginWindow : FluentWindow
{
    public bool SuppressCloseExit { get; set; }

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        PasswordInput.PasswordChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel viewModel)
            {
                viewModel.Password = PasswordInput.Password;
            }
        };
        ConfirmPasswordInput.PasswordChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel viewModel)
            {
                viewModel.ConfirmPassword = ConfirmPasswordInput.Password;
            }
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!SuppressCloseExit)
        {
            Application.Current.Shutdown();
        }
    }
}
