using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Configuration;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Infrastructure.Providers.M3U;
using MyIPTV.Infrastructure.Providers;
using MyIPTV.Infrastructure.Providers.Xtream;
using MyIPTV.Infrastructure.Providers.Stalker;
using MyIPTV.Infrastructure.Security;
using MyIPTV.Infrastructure.Playback;
using MyIPTV.Infrastructure.Epg;

namespace MyIPTV.Infrastructure.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyIptvInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ApplicationOptions>()
            .Bind(configuration.GetSection(ApplicationOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Name) &&
                           !string.IsNullOrWhiteSpace(options.DataDirectoryName),
                "Application configuration is incomplete.")
            .ValidateOnStart();

        services
            .AddOptions<NetworkOptions>()
            .Bind(configuration.GetSection(NetworkOptions.SectionName))
            .Validate(
                options => options.TimeoutSeconds is >= 5 and <= 300,
                "Network timeout must be between 5 and 300 seconds.")
            .ValidateOnStart();

        services.AddSingleton<IApplicationConfiguration, ApplicationConfiguration>();
        services.AddSingleton<IApplicationPaths, ApplicationPaths>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IDatabaseService, SqliteDatabaseService>();
        services.AddSingleton<IProfileRepository, SqliteProfileRepository>();
        services.AddSingleton<IFavoriteRepository, SqliteFavoriteRepository>();
        services.AddSingleton<IWatchHistoryRepository, SqliteWatchHistoryRepository>();
        services.AddSingleton<IEpgRepository, SqliteEpgRepository>();
        services.AddSingleton<ICredentialService, WindowsCredentialService>();
        services.AddSingleton<IProfileValidator, ProfileValidator>();
        services.AddSingleton<IProfileConnectionTester, ProfileConnectionTester>();
        services.AddSingleton<IM3uPlaylistParser, M3uPlaylistParser>();
        services.AddSingleton<IChannelCatalog, InMemoryChannelCatalog>();
        services.AddSingleton<IMediaCatalog, InMemoryMediaCatalog>();
        services.AddSingleton<IPlaylistImportService, M3uPlaylistImportService>();
        services.AddSingleton<IXtreamClient, XtreamClient>();
        services.AddSingleton<IStalkerClient, StalkerClient>();
        services.AddSingleton<IContentProvider, M3uContentProvider>();
        services.AddSingleton<IContentProvider, XtreamProvider>();
        services.AddSingleton<IContentProvider, StalkerProvider>();
        services.AddSingleton<LibVlcPlaybackService>();
        services.AddSingleton<IPlaybackService>(provider => provider.GetRequiredService<LibVlcPlaybackService>());
        services.AddSingleton<IPlaybackVideoSource>(provider => provider.GetRequiredService<LibVlcPlaybackService>());
        services.AddSingleton<IActiveProfileService, ActiveProfileService>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<ISearchService, CatalogSearchService>();
        services.AddSingleton<IXmlTvParser, XmlTvParser>();
        services.AddSingleton<IEpgService, EpgService>();

        return services;
    }
}
