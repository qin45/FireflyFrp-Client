using System.Windows;
using QZ.Frp.Client.ViewModels;
using Wpf.Ui.Controls;

namespace QZ.Frp.Client.Views;

public partial class ChangePasswordWindow : FluentWindow
{
    public ChangePasswordWindow()
    {
        InitializeComponent();
        OldPasswordInput.PasswordChanged += (_, _) =>
        {
            if (DataContext is ChangePasswordViewModel viewModel)
            {
                viewModel.OldPassword = OldPasswordInput.Password;
            }
        };
        NewPasswordInput.PasswordChanged += (_, _) =>
        {
            if (DataContext is ChangePasswordViewModel viewModel)
            {
                viewModel.NewPassword = NewPasswordInput.Password;
            }
        };
        ConfirmPasswordInput.PasswordChanged += (_, _) =>
        {
            if (DataContext is ChangePasswordViewModel viewModel)
            {
                viewModel.ConfirmPassword = ConfirmPasswordInput.Password;
            }
        };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
