using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using DiskManager.Services;
using DiskManager.ViewModels;

namespace DiskManager;

public partial class App : Application
{
    private IHost _host = null!;

    public T? GetService<T>() where T : class
        => _host.Services.GetService<T>();

    // Shadows Application.Current to return typed App — only valid after OnStartup
    public new static App Current => (App)Application.Current;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IThemeService, ThemeService>();
                    services.AddSingleton<IFileSystemService, FileSystemService>();
                    services.AddSingleton<IDiskAnalyzerService, DiskAnalyzerService>();
                    services.AddSingleton<IDuplicateFinderService, DuplicateFinderService>();

                    services.AddTransient<ExplorerViewModel>();
                    services.AddTransient<DiskAnalyzerViewModel>();
                    services.AddTransient<DuplicateFinderViewModel>();

                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await _host.StartAsync();

            var themeService = _host.Services.GetRequiredService<IThemeService>();
            themeService.Apply(Resources);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al iniciar la aplicación:\n{ex.Message}",
                "Error de inicio", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        _host.Dispose();
        base.OnExit(e);
    }
}
