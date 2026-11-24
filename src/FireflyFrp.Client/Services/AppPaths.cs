using System.Security.Cryptography;
using System.Text;

namespace FireflyFrp.Client.Services;

public static class AppPaths
{
    private const string FrpcResourceName = "FireflyFrp.Client.Resources.frpc.exe";

    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FireflyFrp");

    public static string LocalDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FireflyFrp");

    public static string SettingsFile => Path.Combine(AppDataDirectory, "settings.json");

    public static string RuntimeConfigDirectory => Path.Combine(LocalDataDirectory, "run");

    public static string LogsDirectory => Path.Combine(LocalDataDirectory, "logs");

    public static string BundledFrpcPath =>
        Path.Combine(BaseDirectory, "Resources", "frpc", "frpc.exe");

    public static string FrpcNextToExePath => Path.Combine(BaseDirectory, "frpc.exe");

    /// <summary>
    /// Extracts the embedded frpc.exe to LocalAppData so the client remains
    /// usable when only FireflyFrp.exe is copied to another folder or machine.
    /// </summary>
    public static string ExtractBundledFrpc()
    {
        try
        {
            using var resource = typeof(AppPaths).Assembly
                .GetManifestResourceStream(FrpcResourceName);
            if (resource is null)
            {
                return BundledFrpcPath;
            }

            var targetDirectory = Path.Combine(LocalDataDirectory, "bin");
            Directory.CreateDirectory(targetDirectory);
            var target = Path.Combine(targetDirectory, "frpc.exe");
            if (File.Exists(target) && new FileInfo(target).Length == resource.Length)
            {
                return target;
            }

            var temp = $"{target}.{Environment.ProcessId}.tmp";
            using (var file = File.Create(temp))
            {
                resource.CopyTo(file);
            }
            try
            {
                File.Move(temp, target, overwrite: true);
            }
            catch
            {
                // Another client may already have the file open.
                if (File.Exists(target))
                {
                    return target;
                }
                throw;
            }
            return target;
        }
        catch
        {
            return BundledFrpcPath;
        }
    }

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
