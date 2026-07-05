using System.Windows;

namespace DiskManager.Services;

public enum Theme { Light, Dark }

public interface IThemeService
{
    Theme CurrentTheme { get; }
    event EventHandler<Theme> ThemeChanged;
    void Apply(ResourceDictionary resources);
}
