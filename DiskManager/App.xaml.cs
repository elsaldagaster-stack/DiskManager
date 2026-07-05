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

    public new static App Current => (App)Application.Current;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
