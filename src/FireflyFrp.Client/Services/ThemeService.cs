using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FireflyFrp.Client.Services;

/// <summary>
/// Applies the light/dark palette and keeps the custom resource dictionary in
/// sync with the effective WPF-UI theme.
/// </summary>
public static class ThemeService
{
    private const string LightUri = "pack://application:,,,/Styles/LightTheme.xaml";
    private const string DarkUri = "pack://application:,,,/Styles/DarkTheme.xaml";

    private static string _preference = "system";
    private static ApplicationTheme _currentTheme = ApplicationTheme.Light;
    private static bool _watchingSystem;

    public static event EventHandler? Changed;

    public static ApplicationTheme CurrentTheme => _currentTheme;
    public static bool IsDark => _currentTheme == ApplicationTheme.Dark;

    public static void Apply(string preference)
    {
        _preference = NormalizePreference(preference);
        ApplyTheme(ResolveTheme(_preference));
    }

    public static string GetToggleLabel() =>
        IsDark ? "浅色模式" : "深色模式";

    public static string GetOppositePreference() =>
        IsDark ? "light" : "dark";

    public static void StartSystemWatcher()
    {
        if (_watchingSystem)
        {
            return;
        }
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _watchingSystem = true;
    }

    public static void StopSystemWatcher()
    {
        if (!_watchingSystem)
        {
            return;
        }
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _watchingSystem = false;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_preference != "system")
        {
            return;
        }
        var app = Application.Current;
        if (app is null)
        {
            return;
        }
        app.Dispatcher.BeginInvoke(() =>
        {
            if (_preference == "system")
            {
                ApplyTheme(ResolveTheme("system"));
            }
        });
    }

    private static void ApplyTheme(ApplicationTheme theme)
    {
        var app = Application.Current;
        if (app is null)
        {
            _currentTheme = theme;
            return;
        }

        var dictionaries = app.Resources.MergedDictionaries;
        var index = -1;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (source is null)
            {
                continue;
            }
            if (source.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                source.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(theme == ApplicationTheme.Dark ? DarkUri : LightUri),
        };
        if (index >= 0)
        {
            dictionaries[index] = dictionary;
        }
        else
        {
            dictionaries.Insert(0, dictionary);
        }

        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, true);
        _currentTheme = theme;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static ApplicationTheme ResolveTheme(string preference)
    {
        if (preference == "light")
        {
            return ApplicationTheme.Light;
        }
        if (preference == "dark")
        {
            return ApplicationTheme.Dark;
        }
        return ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }

    private static string NormalizePreference(string? preference) =>
        preference is "light" or "dark" or "system" ? preference : "system";
}
