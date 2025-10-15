using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MusicPlayer.Interfaces;
using MusicPlayer.Services;
using MusicPlayer.Services.DiscordRpc;

namespace MusicPlayer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private ServiceProvider? serviceProvider;

    public App()
    {
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ISettingsService, SqliteSettingsService>();
        services.AddSingleton<IMusicLoaderService, MusicLoaderService>();
        services.AddSingleton<IDurationExtractorService, DurationExtractorService>();
        services.AddSingleton<IShuffleService, ShuffleService>();
        services.AddSingleton<IDiscordRpcService, DiscordRpcService>();
        services.AddSingleton<DiscordPresenceUpdater>();
 
        services.AddTransient<IAudioService, AudioService>();
        services.AddTransient<MainWindow>();

        serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = serviceProvider?.GetRequiredService<MainWindow>();
        mainWindow?.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            e.Handled = true;
        }
    }

    private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var message = e.ExceptionObject is Exception ex ? ($"{ex.Message}\n\n{ex.StackTrace}") : e.ExceptionObject.ToString();
        MessageBox.Show(
            $"A fatal error occurred:\n\n{message}",
            "Fatal Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

