using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers.Stalker.Dtos;

namespace MyIPTV.Infrastructure.Providers.Stalker;

public sealed partial class StalkerProvider(
    ICredentialService credentialService,
    IStalkerClient client,
    IChannelCatalog channelCatalog,
    IMediaCatalog mediaCatalog,
    ILogger<StalkerProvider> logger) : IContentProvider
{
    private readonly ILogger<StalkerProvider> _logger = logger;

    public ProfileConnectionType ConnectionType => ProfileConnectionType.StalkerPortal;

    public async Task<ProviderLoadResult> LoadCatalogAsync(
        IptvProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (profile.ConnectionType != ConnectionType)
        {
            return ProviderLoadResult.Failure("This provider can only load Stalker/Ministra profiles.");
        }

        ProfileCredentials? credentials = await credentialService.RetrieveAsync(profile.Id, cancellationToken);
        string? account = credentials?.Username ?? profile.Username;
        if (string.IsNullOrWhiteSpace(account))
        {
            return ProviderLoadResult.Failure("Enter the portal MAC address again.");
        }

        LogCatalogLoadStarted(profile.Id);
        try
        {
            StalkerSession session = await client.AuthenticateAsync(
                profile.ServerAddress,
                account,
                credentials?.Password ?? string.Empty,
                cancellationToken);
            IReadOnlyList<StalkerChannelDto> channelDtos =
                await client.GetLiveChannelsAsync(profile.ServerAddress, session, cancellationToken);
            List<IptvChannel> channels = channelDtos
                .Select(dto => MapChannel(profile.Id, dto))
                .Where(channel => channel is not null)
                .Cast<IptvChannel>()
                .GroupBy(channel => channel.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            if (channels.Count == 0)
            {
                LogCatalogLoadCompleted(profile.Id, 0, succeeded: false);
                return ProviderLoadResult.Failure("The portal returned no playable live channels.");
            }

            channelCatalog.ReplaceForProfile(profile.Id, channels);
            mediaCatalog.ReplaceForProfile(profile.Id, [], [], []);
            ProviderCatalog catalog = new([], channels, [], []);
            LogCatalogLoadCompleted(profile.Id, channels.Count, succeeded: true);
            return ProviderLoadResult.Success(
                $"Connected and loaded {channels.Count:N0} live channels from the supported Ministra REST interface.",
                catalog);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogCatalogLoadCompleted(profile.Id, 0, succeeded: false);
            return ProviderLoadResult.Failure("The Ministra connection timed out.");
        }
        catch (HttpRequestException)
        {
            LogCatalogLoadCompleted(profile.Id, 0, succeeded: false);
            return ProviderLoadResult.Failure("Unable to reach the Ministra portal.");
        }
        catch (StalkerClientException exception)
        {
            LogCatalogLoadCompleted(profile.Id, 0, succeeded: false);
            return ProviderLoadResult.Failure(
                exception.Error == StalkerClientError.Authentication ? "Authentication failed." : exception.Message);
        }
    }

    public Task<SeriesDetailsResult> LoadSeriesDetailsAsync(
        IptvProfile profile,
        string seriesId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SeriesDetailsResult.Failure(
            "Series are not available through the supported Ministra REST integration."));

    private static IptvChannel? MapChannel(
        Guid profileId,
        StalkerChannelDto dto)
    {
        if (!Uri.TryCreate(dto.StreamUrl, UriKind.Absolute, out Uri? streamUri) ||
            streamUri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return new IptvChannel(
            dto.Id,
            profileId,
            Limit(dto.Name, 512),
            streamUri.AbsoluteUri,
            LimitOptional(dto.LogoUrl, 2_048),
            Limit(dto.Group, 256, "Uncategorized"),
            LimitOptional(dto.EpgId, 512));
    }

    private static string Limit(string? value, int maximumLength, string fallback = "Channel")
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    private static string? LimitOptional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    [LoggerMessage(EventId = 5201, Level = LogLevel.Information, Message = "Loading Ministra catalog for profile {ProfileId}.")]
    private partial void LogCatalogLoadStarted(Guid profileId);

    [LoggerMessage(
        EventId = 5202,
        Level = LogLevel.Information,
        Message = "Ministra catalog load completed for profile {ProfileId}. Live: {LiveCount}; Success: {Succeeded}.")]
    private partial void LogCatalogLoadCompleted(Guid profileId, int liveCount, bool succeeded);
}
