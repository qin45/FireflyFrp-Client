using System.Security.Cryptography;
using System.Text;

namespace FireflyFrp.Client.Services;

/// <summary>
/// DPAPI (CurrentUser) wrapper. The protected blob can only be read by the
/// same Windows user, and the purpose-specific entropy keeps app data isolated.
/// </summary>
public static class SecureStore
{
    private const string EntropyPrefix = "FireflyFrp.Client.v1|";

    public static void Save(string path, string plaintext, string purpose)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var protectedBytes = Protect(plaintext, purpose);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, protectedBytes);
        File.Move(temp, path, overwrite: true);
    }

    public static string? Load(string path, string purpose)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            return Unprotect(File.ReadAllBytes(path), purpose);
        }
        catch
        {
            return null;
        }
    }

    public static byte[] Protect(string plaintext, string purpose)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);
        var entropy = Entropy(purpose);
        return ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
    }

    public static string Unprotect(byte[] protectedBytes, string purpose)
    {
        var entropy = Entropy(purpose);
        var data = ProtectedData.Unprotect(
            protectedBytes, entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(data);
    }

    private static byte[] Entropy(string purpose) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(EntropyPrefix + purpose));
}
