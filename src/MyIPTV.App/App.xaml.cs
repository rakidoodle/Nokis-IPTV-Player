using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyIPTV.App.Services;
using MyIPTV.App.ViewModels;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Services;

namespace MyIPTV.App;

public partial class App : Application
{
    private static readonly Action<ILogger, Exception?> LogApplicationStarted = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(1, nameof(LogApplicationStarted)),
        "MyIPTV started successfully.");

    private static readonly Action<ILogger, string, Exception?> LogCriticalError = LoggerMessage.Define<string>(
        LogLevel.Critical,
        new EventId(2, nameof(LogCriticalError)),
        "{ErrorMessage}");

    private readonly IHost _host;

    public App()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddDebug();

        builder.Services.AddMyIptvInfrastructure(builder.Configuration);
        builder.Services.AddHttpClient("Iptv", (serviceProvider, client) =>
        {
            IApplicationConfiguration configuration =
                serviceProvider.GetRequiredService<IApplicationConfiguration>();
            client.Timeout = TimeSpan.FromSeconds(configuration.Network.TimeoutSeconds);
        });
        builder.Services
            .AddHttpClient("Xtream", (serviceProvider, client) =>
            {
                IApplicationConfiguration configuration =
                    serviceProvider.GetRequiredService<IApplicationConfiguration>();
                client.Timeout = TimeSpan.FromSeconds(configuration.Network.TimeoutSeconds);
            })
            .RemoveAllLoggers();

        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IUserNotificationService, UserNotificationService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<IPlaylistFilePicker, PlaylistFilePicker>();
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddSingleton<ProfilesViewModel>();
        builder.Services.AddSingleton<LiveTvViewModel>();
        builder.Services.AddSingleton<MoviesViewModel>();
        builder.Services.AddSingleton<SeriesViewModel>();
        builder.Services.AddSingleton<FavoritesViewModel>();
        builder.Services.AddSingleton<GuideViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterExceptionHandlers();

        try
        {
            await _host.StartAsync();

            IDatabaseService databaseService = _host.Services.GetRequiredService<IDatabaseService>();
            await databaseService.InitializeAsync();

            ISettingsService settingsService = _host.Services.GetRequiredService<ISettingsService>();
            AppSettings settings = await settingsService.LoadAsync();
            IThemeService themeService = _host.Services.GetRequiredService<IThemeService>();
            AppTheme initialTheme = Enum.TryParse(settings.Theme, ignoreCase: true, out AppTheme savedTheme)
                ? savedTheme
                : AppTheme.Dark;
            themeService.ApplyTheme(initialTheme);

            MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
            LogApplicationStarted(logger, null);
        }
        catch (Exception exception)
        {
            HandleUnexpectedException(
                exception,
                "MyIPTV could not finish starting. Check the application logs for details.");
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception)
        {
            TryLogCritical(exception, "An error occurred while MyIPTV was stopping.");
        }
        finally
        {
            _host.Dispose();
            base.OnExit(e);
        }
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        HandleUnexpectedException(
            e.Exception,
            "Something unexpected happened. You can continue using MyIPTV.");
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TryLogCritical(e.Exception, "An unobserved background task failed.");
        e.SetObserved();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            TryLogCritical(exception, "An unhandled application error occurred.");
        }
    }

    private void HandleUnexpectedException(Exception exception, string userMessage)
    {
        TryLogCritical(exception, userMessage);

        IUserNotificationService? notifications =
            _host.Services.GetService<IUserNotificationService>();
        if (notifications is not null)
        {
            notifications.ShowError("MyIPTV", userMessage);
        }
        else
        {
            MessageBox.Show(userMessage, "MyIPTV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TryLogCritical(Exception exception, string message)
    {
        ILogger<App>? logger = _host.Services.GetService<ILogger<App>>();
        if (logger is not null)
        {
            LogCriticalError(logger, message, exception);
        }
    }
}
