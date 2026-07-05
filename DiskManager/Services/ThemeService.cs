using Microsoft.Win32;
using System.Windows;

namespace DiskManager.Services;

public class ThemeService : IThemeService, IDisposable
{
    public Theme CurrentTheme { get; private set; }
    public event EventHandler<Theme>? ThemeChanged;

    public ThemeService()
    {
        CurrentTheme = ReadWindowsTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Apply(ResourceDictionary resources)
    {
        var uri = CurrentTheme == Theme.Dark
            ? new Uri("Themes/Dark.xaml", UriKind.Relative)
            : new Uri("Themes/Light.xaml", UriKind.Relative);

        var dict = resources.MergedDictionaries.FirstOrDefault();
        if (dict is not null)
            resources.MergedDictionaries.Remove(dict);

        resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = uri });
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        var newTheme = ReadWindowsTheme();
        if (newTheme == CurrentTheme) return;
        CurrentTheme = newTheme;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Apply(Application.Current.Resources);
            ThemeChanged?.Invoke(this, CurrentTheme);
        });
    }

    private static Theme ReadWindowsTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var value = key?.GetValue("AppsUseLightTheme");
        return value is int i && i == 0 ? Theme.Dark : Theme.Light;
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
