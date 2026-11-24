using System.Text.Json;

namespace FireflyFrp.Client.Services;

public sealed class AppSettings
{
    public string LastServerUrl { get; set; } = "";
    public string LastUsername { get; set; } = "";
    public string CloseAction { get; set; } = "ask";
}

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsFile))
            {
                Settings = new AppSettings();
                return;
            }
            var json = File.ReadAllText(AppPaths.SettingsFile);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            if (Settings.CloseAction is not ("ask" or "exit" or "tray"))
            {
                Settings.CloseAction = "ask";
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureDirectories();
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            var temp = AppPaths.SettingsFile + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, AppPaths.SettingsFile, overwrite: true);
        }
        catch
        {
            // Preferences are best-effort; never block the UI for this.
        }
    }
}
