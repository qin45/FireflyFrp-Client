using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireflyFrp.Client.Services;

namespace FireflyFrp.Client.ViewModels;

public sealed partial class ChangePasswordViewModel : ObservableObject
{
    private readonly ClientSession _session;

    public event EventHandler? Changed;

    [ObservableProperty] private string _oldPassword = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = "";

    public ChangePasswordViewModel(ClientSession session)
    {
        _session = session;
    }

    [RelayCommand]
    private async Task ChangeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = "";
        if (string.IsNullOrEmpty(OldPassword))
        {
            ErrorMessage = "请输入原密码。";
            return;
        }
        if (NewPassword.Length is < 8 or > 72)
        {
            ErrorMessage = "新密码长度需为 8-72 位。";
            return;
        }
        if (NewPassword == OldPassword)
        {
            ErrorMessage = "新密码不能与原密码相同。";
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "两次输入的新密码不一致。";
            return;
        }

        IsBusy = true;
        try
        {
            await _session.Api.ChangePasswordAsync(OldPassword, NewPassword);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
