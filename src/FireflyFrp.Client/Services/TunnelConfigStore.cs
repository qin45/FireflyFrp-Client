namespace FireflyFrp.Client.Services;

public sealed class TunnelConfigStore
{
    private readonly string _directory;

    public TunnelConfigStore(AccountStore account)
    {
        _directory = account.TunnelsDirectory;
        System.IO.Directory.CreateDirectory(_directory);
    }

    public void Save(string tunnelId, string frpcConfig)
    {
        var path = PathFor(tunnelId);
        SecureStore.Save(path, frpcConfig, $"tunnel:{tunnelId}");
    }

    public string? Load(string tunnelId) =>
        SecureStore.Load(PathFor(tunnelId), $"tunnel:{tunnelId}");

    public void Delete(string tunnelId)
    {
        try
        {
            var path = PathFor(tunnelId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private string PathFor(string tunnelId) =>
        Path.Combine(_directory, $"{tunnelId}.dat");
}
