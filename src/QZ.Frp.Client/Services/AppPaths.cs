using System.Security.Cryptography;
using System.Text;

namespace QZ.Frp.Client.Services;

public static class AppPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QZ-FRP");

    public static string LocalDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QZ-FRP");

    public static string SettingsFile => Path.Combine(AppDataDirectory, "settings.json");

    public static string RuntimeConfigDirectory => Path.Combine(LocalDataDirectory, "run");

    public static string LogsDirectory => Path.Combine(LocalDataDirectory, "logs");

    public static string BundledFrpcPath =>
        Path.Combine(BaseDirectory, "Resources", "frpc", "frpc.exe");

    public static string ComputeAccountKey(string serverUrl, string username)
    {
        var normalized = $"{serverUrl.Trim().TrimEnd('/')}\n{username.Trim().ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    public static string AccountDirectory(string serverUrl, string username) =>
        Path.Combine(AppDataDirectory, "accounts", ComputeAccountKey(serverUrl, username));

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(LocalDataDirectory);
        Directory.CreateDirectory(RuntimeConfigDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
