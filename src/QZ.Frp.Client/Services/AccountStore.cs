using System.Text.Json;
using QZ.Frp.Client.Models;

namespace QZ.Frp.Client.Services;

public sealed class AccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string ServerUrl { get; }
    public string Username { get; }
    public string Directory { get; }
    public string AuthFile { get; }
    public string TunnelsDirectory { get; }

    public AccountStore(string serverUrl, string username)
    {
        ServerUrl = serverUrl.Trim().TrimEnd('/');
        Username = username.Trim();
        Directory = AppPaths.AccountDirectory(ServerUrl, Username);
        AuthFile = Path.Combine(Directory, "auth.dat");
        TunnelsDirectory = Path.Combine(Directory, "tunnels");
        System.IO.Directory.CreateDirectory(TunnelsDirectory);
    }

    public void SaveAuth(AuthSession session)
    {
        var json = JsonSerializer.Serialize(session);
        SecureStore.Save(AuthFile, json, "auth");
    }

    public AuthSession? LoadAuth()
    {
        var json = SecureStore.Load(AuthFile, "auth");
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<AuthSession>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void ClearAuth()
    {
        try
        {
            if (File.Exists(AuthFile))
            {
                File.Delete(AuthFile);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
