using System.ComponentModel;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows;
using FireflyFrp.Client.Services;
using FireflyFrp.Client.ViewModels;
using FireflyFrp.Client.Views;

namespace FireflyFrp.Client;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppConfigService? _configService;
    private AppSettingsStore? _settingsStore;
    private ApiClient? _api;
    private CertificateTrustStore? _trustStore;
    private ClientSession? _session;
    private LoginWindow? _loginWindow;
    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;
    private TrayIconService? _tray;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrash(args.Exception);
            System.Windows.MessageBox.Show(
                args.Exception.Message,
                "FireflyFrp 启动错误",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrash(args.ExceptionObject as Exception);

        _singleInstance = new Mutex(true, "FireflyFrp-Client-SingleInstance", out var created);
        if (!created)
        {
            System.Windows.MessageBox.Show(
                "FireflyFrp 客户端已经在运行。",
                "FireflyFrp",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        AppPaths.EnsureDirectories();

        _configService = new AppConfigService();
        _configService.Load();
        _settingsStore = new AppSettingsStore();
        _settingsStore.Load();
        ThemeService.Apply(_settingsStore.Settings.Theme);
        ThemeService.StartSystemWatcher();
        _trustStore = new CertificateTrustStore(_configService, ShowCertificateTrustDialog);
        _api = new ApiClient(
            new HttpClient(new HttpClientHandler
            {
                UseProxy = _configService.Config.UseSystemProxy,
                ServerCertificateCustomValidationCallback = ValidateServerCertificate,
            }),
            TimeSpan.FromSeconds(_configService.Config.RequestTimeoutSeconds));

        ShowLogin();
    }

    private static void WriteCrash(Exception? exception)
    {
        try
        {
            AppPaths.EnsureDirectories();
            File.AppendAllText(
                Path.Combine(AppPaths.LogsDirectory, "crash.log"),
                $"[{DateTime.Now:O}] {exception?.ToString() ?? "unknown"}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Nothing else we can do.
        }
    }

    private bool ValidateServerCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        if (request.RequestUri?.Scheme != Uri.UriSchemeHttps)
        {
            return true;
        }
        return _trustStore?.Validate(request.RequestUri.Host, certificate, errors) ?? false;
    }

    private bool ShowCertificateTrustDialog(CertificateTrustRequest request)
    {
        var app = Application.Current;
        if (app is null)
        {
            return false;
        }
        try
        {
            var dispatcher = app.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                return ShowCertificateTrustDialogCore(request);
            }
            return dispatcher.Invoke(() => ShowCertificateTrustDialogCore(request));
        }
        catch
        {
            return false;
        }
    }

    private bool ShowCertificateTrustDialogCore(CertificateTrustRequest request)
    {
        if (_exiting)
        {
            return false;
        }
        Window? owner = _loginWindow;
        owner ??= _mainWindow;
        var dialog = new CertificateTrustDialog(request) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    public void ToggleTheme()
    {
        if (_settingsStore is null)
        {
            return;
        }
        var next = ThemeService.GetOppositePreference();
        _settingsStore.Settings.Theme = next;
        _settingsStore.Save();
        ThemeService.Apply(next);
    }

    public void ShowLogin(string? message = null)
    {
        if (_configService is null || _settingsStore is null || _api is null)
        {
            return;
        }

        var viewModel = new LoginViewModel(_configService, _settingsStore, _api);
        viewModel.LoginSucceeded += OnLoginSucceeded;
        if (!string.IsNullOrWhiteSpace(message))
        {
            viewModel.ErrorMessage = message;
        }

        _loginWindow = new LoginWindow { DataContext = viewModel };
        MainWindow = _loginWindow;
        _loginWindow.Show();
    }

    private void OnLoginSucceeded(object? sender, FireflyFrp.Client.Models.LoginResult result)
    {
        _ = HandleLoginSucceededAsync(result);
    }

    private async Task HandleLoginSucceededAsync(FireflyFrp.Client.Models.LoginResult result)
    {
        if (_session is not null || _mainWindow is not null)
        {
            return;
        }
        if (_configService is null || _settingsStore is null || _api is null)
        {
            return;
        }

        _api.SetSession(result.ServerUrl, result.Token);
        var account = new AccountStore(result.ServerUrl, result.Username);
        var frpc = new FrpcProcessManager(() => _configService.ResolveFrpcPath());
        _session = new ClientSession(_configService.Config, _settingsStore, account, _api, frpc);
        _mainViewModel = new MainViewModel(_session);
        _mainViewModel.LogoutRequested += (_, _) => _ = LogoutAsync();
        _mainViewModel.SessionExpired += (_, _) => _ = HandleSessionExpiredAsync();
        _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        _mainViewModel.TunnelsChanged += (_, _) => UpdateTrayTooltip();

        if (_loginWindow is not null)
        {
            _loginWindow.SuppressCloseExit = true;
            _loginWindow.Close();
            _loginWindow = null;
        }

        _mainWindow = new MainWindow { DataContext = _mainViewModel };
        MainWindow = _mainWindow;
        _mainWindow.Show();

        _tray = new TrayIconService(
            showMain: ShowMainWindow,
            createTunnel: ShowCreateTunnelDialog,
            stopAll: StopAllFromTrayAsync,
            setCloseAction: SetCloseAction,
            exit: () => _ = ExitAppAsync());
        _tray.Create();
        UpdateTrayTooltip();

        await _mainViewModel.InitializeAsync();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }
        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
    }

    public void ShowCreateTunnelDialog()
    {
        if (_session is null || _mainViewModel is null)
        {
            return;
        }
        ShowMainWindow();
        var viewModel = new CreateTunnelViewModel(
            _session, _mainViewModel.EnabledTunnelTypes);
        var window = new CreateTunnelWindow
        {
            DataContext = viewModel,
            Owner = _mainWindow,
        };
        viewModel.Created += (_, _) => window.DialogResult = true;
        if (window.ShowDialog() == true)
        {
            _ = _mainViewModel.ReloadAsync();
        }
    }

    public void ShowChangePasswordDialog()
    {
        if (_session is null)
        {
            return;
        }

        var viewModel = new ChangePasswordViewModel(_session);
        var window = new ChangePasswordWindow
        {
            DataContext = viewModel,
            Owner = _mainWindow,
        };
        viewModel.Changed += (_, _) => window.DialogResult = true;
        if (window.ShowDialog() == true)
        {
            _ = HandlePasswordChangedAsync();
        }
    }

    private async Task HandlePasswordChangedAsync()
    {
        await TearDownSessionAsync();
        ShowLogin("密码已修改，请重新登录。");
    }

    public bool IsExiting => _exiting;

    public void NotifyMinimizedToTray()
    {
        _tray?.ShowInfo("已最小化到托盘，隧道继续运行。双击托盘图标可恢复窗口。");
    }

    public void SetCloseAction(string action)
    {
        if (_settingsStore is null)
        {
            return;
        }
        if (action is not ("ask" or "exit" or "tray"))
        {
            action = "ask";
        }
        _settingsStore.Settings.CloseAction = action;
        _settingsStore.Save();
        _tray?.ShowInfo(action switch
        {
            "exit" => "关闭行为已设置为：直接退出。",
            "tray" => "关闭行为已设置为：最小化到托盘。",
            _ => "关闭行为已设置为：每次询问。",
        });
    }

    private async Task StopAllFromTrayAsync()
    {
        if (_mainViewModel is null)
        {
            return;
        }
        await _mainViewModel.StopAllTunnelsAsync();
        _tray?.ShowInfo("已停止所有隧道。");
        UpdateTrayTooltip();
    }

    private void UpdateTrayTooltip()
    {
        if (_mainViewModel is null)
        {
            return;
        }
        _tray?.SetTooltip($"{_mainViewModel.Username}  剩余 {_mainViewModel.RemainingText}");
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.RemainingText) or nameof(MainViewModel.Username))
        {
            UpdateTrayTooltip();
        }
    }

    private async Task LogoutAsync()
    {
        await TearDownSessionAsync();
        ShowLogin("已退出登录。");
    }

    private async Task HandleSessionExpiredAsync()
    {
        await TearDownSessionAsync();
        ShowLogin("登录已失效，请重新登录。");
    }

    private async Task TearDownSessionAsync()
    {
        if (_mainViewModel is not null)
        {
            await _mainViewModel.StopAllForShutdownAsync();
            _mainViewModel.Dispose();
        }
        _tray?.Dispose();
        _tray = null;
        _session?.Account.ClearAuth();
        _api?.ClearSession();
        _session?.Frpc.Dispose();
        _session = null;

        var window = _mainWindow;
        _mainWindow = null;
        _mainViewModel = null;
        window?.ForceClose();
    }

    public async Task ExitAppAsync()
    {
        if (_exiting)
        {
            return;
        }
        _exiting = true;

        try
        {
            if (_mainViewModel is not null)
            {
                await _mainViewModel.StopAllForShutdownAsync();
                _mainViewModel.Dispose();
                _mainViewModel = null;
            }
            _tray?.Dispose();
            _tray = null;

            if (_loginWindow is not null)
            {
                _loginWindow.SuppressCloseExit = true;
                _loginWindow.Close();
                _loginWindow = null;
            }
            // Do not call Close() on the main window from its own Closing
            // handler. Application.Shutdown closes remaining windows; the
            // Closing handler sees IsExiting and does not cancel.
            _mainWindow = null;
        }
        finally
        {
            _tray?.Dispose();
            _tray = null;
            Shutdown();
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _session?.Frpc.Dispose();
        _tray?.Dispose();
        _tray = null;
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeService.StopSystemWatcher();
        _tray?.Dispose();
        _session?.Frpc.Dispose();
        try
        {
            _singleInstance?.ReleaseMutex();
        }
        catch
        {
            // Mutex may not be owned if startup failed.
        }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
