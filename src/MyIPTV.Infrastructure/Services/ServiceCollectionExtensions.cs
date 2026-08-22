using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Configuration;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Infrastructure.Security;

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
        services.AddSingleton<ICredentialService, WindowsCredentialService>();
        services.AddSingleton<IProfileValidator, ProfileValidator>();
        services.AddSingleton<IProfileConnectionTester, ProfileConnectionTester>();
        services.AddSingleton<IActiveProfileService, ActiveProfileService>();
        services.AddSingleton<IProfileService, ProfileService>();

        return services;
    }
}
