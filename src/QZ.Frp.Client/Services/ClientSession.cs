using QZ.Frp.Client.Models;

namespace QZ.Frp.Client.Services;

public sealed class ClientSession
{
    public AppConfig Config { get; }
    public AppSettings Settings { get; }
    public AppSettingsStore SettingsStore { get; }
    public AccountStore Account { get; }
    public TunnelConfigStore TunnelConfigs { get; }
    public ApiClient Api { get; }
    public FrpcProcessManager Frpc { get; }
    public UserInfo? CurrentUser { get; set; }

    public string ServerUrl => Account.ServerUrl;
    public string Username => Account.Username;

    public ClientSession(
        AppConfig config,
        AppSettingsStore settingsStore,
        AccountStore account,
        ApiClient api,
        FrpcProcessManager frpc)
    {
        Config = config;
        SettingsStore = settingsStore;
        Settings = settingsStore.Settings;
        Account = account;
        TunnelConfigs = new TunnelConfigStore(account);
        Api = api;
        Frpc = frpc;
    }

    public bool HasTraffic => CurrentUser is { TrafficRemainingBytes: > 0 };
}
