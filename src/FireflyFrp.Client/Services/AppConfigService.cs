using System.Text.Json;

namespace QZ.Frp.Client.Services;

public sealed class AppConfig
{
    public string ServerUrl { get; set; } = "";
    public bool LockServerUrl { get; set; }
    public string FrpcPath { get; set; } = "";
    public int PollIntervalSeconds { get; set; } = 3;
    public int RequestTimeoutSeconds { get; set; } = 10;
    public bool UseSystemProxy { get; set; } = true;
}

public sealed class AppConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public AppConfig Config { get; private set; } = new();
    public string? LoadError { get; private set; }

    public void Load()
    {
        var path = Path.Combine(AppPaths.BaseDirectory, "client.config.json");
        if (!File.Exists(path))
        {
            Config = new AppConfig();
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            Config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            Config.ServerUrl = Config.ServerUrl.Trim();
            Config.FrpcPath = Config.FrpcPath.Trim();
            if (Config.PollIntervalSeconds < 1)
            {
                Config.PollIntervalSeconds = 3;
            }
            if (Config.RequestTimeoutSeconds < 1)
            {
                Config.RequestTimeoutSeconds = 10;
            }
            if (Config.LockServerUrl)
            {
                if (string.IsNullOrWhiteSpace(Config.ServerUrl))
                {
                    LoadError = "配置了 lockServerUrl，但 serverUrl 为空。";
                }
                else if (!IsValidServerUrl(Config.ServerUrl))
                {
                    LoadError = "serverUrl 必须是 http:// 或 https:// 地址。";
                }
            }
        }
        catch (Exception ex)
        {
            Config = new AppConfig();
            LoadError = $"读取 client.config.json 失败：{ex.Message}";
        }
    }

    public string ResolveFrpcPath()
    {
        if (!string.IsNullOrWhiteSpace(Config.FrpcPath))
        {
            var configured = Config.FrpcPath.Trim();
            if (!Path.IsPathRooted(configured))
            {
                configured = Path.Combine(AppPaths.BaseDirectory, configured);
            }
            if (File.Exists(configured))
            {
                return configured;
            }
            // A copied config may still contain an absolute path from another
            // computer. Fall through to the bundled copy instead of failing.
        }
        if (File.Exists(AppPaths.BundledFrpcPath))
        {
            return AppPaths.BundledFrpcPath;
        }
        if (File.Exists(AppPaths.FrpcNextToExePath))
        {
            return AppPaths.FrpcNextToExePath;
        }
        return AppPaths.ExtractBundledFrpc();
    }

    public bool IsServerUrlVisible =>
        !(Config.LockServerUrl && !string.IsNullOrWhiteSpace(Config.ServerUrl));

    public string DefaultServerUrl => Config.LockServerUrl ? Config.ServerUrl : "";

    public static bool IsValidServerUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }
        return uri.Scheme is "http" or "https";
    }
}
