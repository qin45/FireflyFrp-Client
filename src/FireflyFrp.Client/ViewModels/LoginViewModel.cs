using System.Net;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireflyFrp.Client.Models;
using FireflyFrp.Client.Services;

namespace FireflyFrp.Client.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly AppConfigService _configService;
    private readonly AppSettingsStore _settingsStore;
    private readonly ApiClient _api;

    public event EventHandler<LoginResult>? LoginSucceeded;

    [ObservableProperty] private string _serverUrl = "";
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private bool _rememberLogin = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRegisterMode;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _statusMessage = "";

    private static readonly Regex UsernameRegex =
        new("^[A-Za-z0-9_-]{3,32}$", RegexOptions.Compiled);

    public bool IsLoginMode => !IsRegisterMode;
    public bool IsServerUrlVisible => _configService.IsServerUrlVisible;

    partial void OnIsRegisterModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLoginMode));
    }

    public LoginViewModel(
        AppConfigService configService,
        AppSettingsStore settingsStore,
        ApiClient api)
    {
        _configService = configService;
        _settingsStore = settingsStore;
        _api = api;
    }

    public async Task InitializeAsync()
    {
        AppPaths.EnsureDirectories();
        var settings = _settingsStore.Settings;
        var config = _configService.Config;

        ServerUrl = _configService.IsServerUrlVisible
            ? (string.IsNullOrWhiteSpace(settings.LastServerUrl)
                ? config.ServerUrl
                : settings.LastServerUrl)
            : config.ServerUrl;
        Username = settings.LastUsername;

        if (!string.IsNullOrWhiteSpace(_configService.LoadError))
        {
            ErrorMessage = _configService.LoadError;
            return;
        }

        if (!string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(Username))
        {
            await TryAutoLoginAsync();
        }
    }

    private async Task TryAutoLoginAsync()
    {
        var account = new AccountStore(ServerUrl, Username);
        var auth = account.LoadAuth();
        if (auth is null)
        {
            return;
        }
        if (!string.Equals(auth.ServerUrl.TrimEnd('/'), ServerUrl.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(auth.Username, Username, StringComparison.OrdinalIgnoreCase))
        {
            account.ClearAuth();
            return;
        }
        if (auth.ExpiresAt <= DateTime.UtcNow)
        {
            account.ClearAuth();
            ErrorMessage = "保存的登录状态已过期，请重新登录。";
            return;
        }

        RememberLogin = true;
        IsBusy = true;
        StatusMessage = "正在恢复登录…";
        try
        {
            _api.SetSession(auth.ServerUrl, auth.Token);
            await _api.GetUserAsync();
            LoginSucceeded?.Invoke(this, new LoginResult
            {
                ServerUrl = auth.ServerUrl,
                Username = auth.Username,
                Token = auth.Token,
                ExpiresAt = auth.ExpiresAt,
                Remember = true,
            });
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            account.ClearAuth();
            _api.ClearSession();
            ErrorMessage = "登录已过期，请重新登录。";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            account.ClearAuth();
            _api.ClearSession();
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            _api.ClearSession();
            StatusMessage = "";
            ErrorMessage = "服务器 HTTPS 证书不受信任，已取消连接。";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _api.ClearSession();
            StatusMessage = "";
            ErrorMessage = "无法连接服务器，请检查网络或服务端地址。";
        }
        catch (Exception ex)
        {
            _api.ClearSession();
            StatusMessage = "";
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            if (ErrorMessage.Length == 0)
            {
                StatusMessage = "";
            }
        }
    }

    [RelayCommand]
    private void ShowRegister()
    {
        IsRegisterMode = true;
        ErrorMessage = "";
        StatusMessage = "";
        ConfirmPassword = "";
    }

    [RelayCommand]
    private void ShowLogin()
    {
        IsRegisterMode = false;
        ErrorMessage = "";
        StatusMessage = "";
        ConfirmPassword = "";
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = "";
        StatusMessage = "";
        if (_configService.Config.LockServerUrl)
        {
            ServerUrl = _configService.Config.ServerUrl;
        }
        if (string.IsNullOrWhiteSpace(ServerUrl) ||
            !AppConfigService.IsValidServerUrl(ServerUrl))
        {
            ErrorMessage = "请输入有效的服务端地址（http:// 或 https://）。";
            return;
        }
        if (string.IsNullOrWhiteSpace(Username) || !UsernameRegex.IsMatch(Username.Trim()))
        {
            ErrorMessage = "用户名需为 3-32 位字母、数字、下划线或短横线。";
            return;
        }
        if (Password.Length is < 8 or > 72)
        {
            ErrorMessage = "密码长度需为 8-72 位。";
            return;
        }
        if (Password != ConfirmPassword)
        {
            ErrorMessage = "两次输入的密码不一致。";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在注册…";
        try
        {
            await _api.RegisterAsync(ServerUrl, Username.Trim(), Password);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            ErrorMessage = "用户名已存在，请换一个用户名。";
            return;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            ErrorMessage = "服务器 HTTPS 证书不受信任，已取消连接。";
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "无法连接服务器，请检查网络或服务端地址。";
            return;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        // Registration succeeded; sign in with the same credentials. If the
        // login fails, the view switches back to the login form with the error.
        IsRegisterMode = false;
        StatusMessage = "注册成功，请登录";
        await LoginAsync();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = "";
        StatusMessage = "";
        if (_configService.Config.LockServerUrl)
        {
            ServerUrl = _configService.Config.ServerUrl;
        }
        if (string.IsNullOrWhiteSpace(ServerUrl) ||
            !AppConfigService.IsValidServerUrl(ServerUrl))
        {
            ErrorMessage = "请输入有效的服务端地址（http:// 或 https://）。";
            return;
        }
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "请输入用户名。";
            return;
        }
        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "请输入密码。";
            return;
        }

        var username = Username.Trim();
        Username = username;
        IsBusy = true;
        StatusMessage = "正在登录…";
        try
        {
            var response = await _api.LoginAsync(ServerUrl, username, Password);
            _api.SetSession(ServerUrl, response.AccessToken);

            var account = new AccountStore(ServerUrl, username);
            if (RememberLogin)
            {
                account.SaveAuth(new AuthSession
                {
                    ServerUrl = ServerUrl.Trim().TrimEnd('/'),
                    Username = username,
                    Token = response.AccessToken,
                    ExpiresAt = response.ExpiresAt,
                });
            }
            else
            {
                account.ClearAuth();
            }

            _settingsStore.Settings.LastServerUrl = ServerUrl.Trim().TrimEnd('/');
            _settingsStore.Settings.LastUsername = username;
            _settingsStore.Save();

            LoginSucceeded?.Invoke(this, new LoginResult
            {
                ServerUrl = ServerUrl.Trim().TrimEnd('/'),
                Username = username,
                Token = response.AccessToken,
                ExpiresAt = response.ExpiresAt,
                Remember = RememberLogin,
            });
        }
        catch (ApiException ex)
        {
            _api.ClearSession();
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            _api.ClearSession();
            ErrorMessage = "服务器 HTTPS 证书不受信任，已取消连接。";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _api.ClearSession();
            ErrorMessage = "无法连接服务器，请检查网络或服务端地址。";
        }
        catch (Exception ex)
        {
            _api.ClearSession();
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = "";
        }
    }
}
