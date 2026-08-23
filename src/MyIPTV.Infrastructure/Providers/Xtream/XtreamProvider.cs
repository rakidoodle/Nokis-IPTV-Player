using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers.Xtream.Dtos;

namespace MyIPTV.Infrastructure.Providers.Xtream;

public sealed partial class XtreamProvider(
    ICredentialService credentialService,
    IXtreamClient client,
    IChannelCatalog channelCatalog,
    IMediaCatalog mediaCatalog,
    ILogger<XtreamProvider> logger) : IContentProvider
{
    private readonly ILogger<XtreamProvider> _logger = logger;

    public ProfileConnectionType ConnectionType => ProfileConnectionType.XtreamApi;

    public async Task<ProviderLoadResult> LoadCatalogAsync(
        IptvProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (profile.ConnectionType != ConnectionType)
        {
            return ProviderLoadResult.Failure("This provider can only load Xtream profiles.");
        }

        ProfileCredentials? credentials = await credentialService.RetrieveAsync(profile.Id, cancellationToken);
        if (credentials is null || string.IsNullOrWhiteSpace(credentials.Username))
        {
            return ProviderLoadResult.Failure("Enter the Xtream username and password again.");
        }

        LogCatalogLoadStarted(profile.Id);
        try
        {
            XtreamAuthenticationDto authentication = await client.AuthenticateAsync(
                profile.ServerAddress,
                credentials.Username,
                credentials.Password,
                cancellationToken);
            if (!IsAuthenticated(authentication.UserInfo))
            {
                LogCatalogLoadCompleted(profile.Id, 0, 0, 0, succeeded: false);
                return ProviderLoadResult.Failure("Authentication failed.");
            }

            if (!IsActive(authentication.UserInfo?.Status))
            {
                LogCatalogLoadCompleted(profile.Id, 0, 0, 0, succeeded: false);
                return ProviderLoadResult.Failure("The Xtream account is not active.");
            }

            IReadOnlyList<XtreamCategoryDto> liveCategoryDtos = await client.GetCategoriesAsync(
                profile.ServerAddress, credentials.Username, credentials.Password, "get_live_categories", cancellationToken);
            IReadOnlyList<XtreamLiveStreamDto> liveDtos = await client.GetLiveStreamsAsync(
                profile.ServerAddress, credentials.Username, credentials.Password, cancellationToken);
            IReadOnlyList<XtreamCategoryDto> vodCategoryDtos = await client.GetCategoriesAsync(
                profile.ServerAddress, credentials.Username, credentials.Password, "get_vod_categories", cancellationToken);
            IReadOnlyList<XtreamVodStreamDto> vodDtos = await client.GetVodStreamsAsync(
                profile.ServerAddress, credentials.Username, credentials.Password, cancellationToken);
            IReadOnlyList<XtreamCategoryDto> seriesCategoryDtos = await client.GetCategoriesAsync(
                profile.ServerAddress, credentials.Username, credentials.Password, "get_series_categories", cancellationToken);
            IReadOnlyList<XtreamSeriesDto> seriesDtos = await client.GetSeriesAsync(
                profile.ServerAddress, credentials.Username, credentials.Password, cancellationToken);

            List<ContentCategory> categories = [];
            categories.AddRange(MapCategories(profile.Id, liveCategoryDtos, ContentKind.LiveTv));
            categories.AddRange(MapCategories(profile.Id, vodCategoryDtos, ContentKind.Movie));
            categories.AddRange(MapCategories(profile.Id, seriesCategoryDtos, ContentKind.Series));
            Dictionary<string, string> liveGroups = CategoryNames(liveCategoryDtos);
            string liveExtension = SelectLiveExtension(authentication.UserInfo?.AllowedOutputFormats);
            List<IptvChannel> liveChannels = liveDtos
                .Where(item => item.StreamId > 0)
                .Select(item => MapLiveChannel(profile, credentials, item, liveGroups, liveExtension))
                .ToList();
            List<MovieItem> movies = vodDtos
                .Where(item => item.StreamId > 0)
                .Select(item => MapMovie(profile, credentials, item))
                .ToList();
            List<SeriesItem> series = seriesDtos
                .Where(item => item.SeriesId > 0)
                .Select(item => MapSeries(profile, item))
                .ToList();

            channelCatalog.ReplaceForProfile(profile.Id, liveChannels);
            mediaCatalog.ReplaceForProfile(profile.Id, categories, movies, series);
            ProviderCatalog catalog = new(categories, liveChannels, movies, series);
            LogCatalogLoadCompleted(profile.Id, liveChannels.Count, movies.Count, series.Count, succeeded: true);
            return ProviderLoadResult.Success(
                $"Connected and loaded {liveChannels.Count:N0} live channels, {movies.Count:N0} movies, and {series.Count:N0} series.",
                catalog);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogCatalogLoadCompleted(profile.Id, 0, 0, 0, succeeded: false);
            return ProviderLoadResult.Failure("Xtream connection timed out.");
        }
        catch (HttpRequestException)
        {
            LogCatalogLoadCompleted(profile.Id, 0, 0, 0, succeeded: false);
            return ProviderLoadResult.Failure("Unable to reach the Xtream server.");
        }
        catch (XtreamClientException exception)
        {
            LogCatalogLoadCompleted(profile.Id, 0, 0, 0, succeeded: false);
            return ProviderLoadResult.Failure(
                exception.Error == XtreamClientError.Authentication
                    ? "Authentication failed."
                    : exception.Message);
        }
    }

    public async Task<SeriesDetailsResult> LoadSeriesDetailsAsync(
        IptvProfile profile,
        string seriesId,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(seriesId, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedSeriesId) ||
            parsedSeriesId <= 0)
        {
            return SeriesDetailsResult.Failure("The series identifier is invalid.");
        }

        ProfileCredentials? credentials = await credentialService.RetrieveAsync(profile.Id, cancellationToken);
        if (credentials is null || string.IsNullOrWhiteSpace(credentials.Username))
        {
            return SeriesDetailsResult.Failure("Enter the Xtream username and password again.");
        }

        try
        {
            XtreamSeriesInfoDto details = await client.GetSeriesInfoAsync(
                profile.ServerAddress,
                credentials.Username,
                credentials.Password,
                seriesId,
                cancellationToken);
            List<EpisodeItem> episodes = [];
            foreach ((string seasonKey, List<XtreamEpisodeDto> seasonEpisodes) in details.Episodes ?? [])
            {
                int.TryParse(seasonKey, NumberStyles.None, CultureInfo.InvariantCulture, out int seasonFromKey);
                foreach (XtreamEpisodeDto episode in seasonEpisodes)
                {
                    string? episodeId = JsonId(episode.Id);
                    if (string.IsNullOrEmpty(episodeId))
                    {
                        continue;
                    }

                    int seasonNumber = episode.SeasonNumber > 0 ? episode.SeasonNumber : seasonFromKey;
                    string extension = XtreamClient.NormalizeExtension(episode.ContainerExtension);
                    string streamUrl = XtreamClient.BuildStreamUri(
                        profile.ServerAddress,
                        "series",
                        credentials.Username,
                        credentials.Password,
                        episodeId,
                        extension).AbsoluteUri;
                    episodes.Add(new EpisodeItem(
                        episodeId,
                        profile.Id,
                        seriesId,
                        seasonNumber,
                        episode.EpisodeNumber,
                        Normalize(episode.Title, $"Episode {episode.EpisodeNumber:N0}", 512),
                        streamUrl,
                        extension,
                        NormalizeOptional(episode.Info?.Plot, 4_096),
                        NormalizeOptional(episode.Info?.Duration, 64)));
                }
            }

            mediaCatalog.ReplaceSeriesEpisodes(profile.Id, seriesId, episodes);
            return SeriesDetailsResult.Success(episodes);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SeriesDetailsResult.Failure("Series details request timed out.");
        }
        catch (HttpRequestException)
        {
            return SeriesDetailsResult.Failure("Unable to reach the Xtream server.");
        }
        catch (XtreamClientException exception)
        {
            return SeriesDetailsResult.Failure(
                exception.Error == XtreamClientError.Authentication
                    ? "Authentication failed."
                    : exception.Message);
        }
    }

    private static IEnumerable<ContentCategory> MapCategories(
        Guid profileId,
        IEnumerable<XtreamCategoryDto> categories,
        ContentKind kind) =>
        categories
            .Where(item => !string.IsNullOrWhiteSpace(item.CategoryId))
            .Select(item => new ContentCategory(
                CategoryId(profileId, kind, item.CategoryId!),
                profileId,
                Normalize(item.CategoryName, "Uncategorized", 256),
                kind));

    private static IptvChannel MapLiveChannel(
        IptvProfile profile,
        ProfileCredentials credentials,
        XtreamLiveStreamDto item,
        Dictionary<string, string> groups,
        string extension)
    {
        string rawCategoryId = item.CategoryId?.Trim() ?? string.Empty;
        string group = groups.TryGetValue(rawCategoryId, out string? categoryName)
            ? categoryName
            : "Uncategorized";
        string streamUrl = XtreamClient.BuildStreamUri(
            profile.ServerAddress,
            "live",
            credentials.Username!,
            credentials.Password,
            item.StreamId.ToString(CultureInfo.InvariantCulture),
            extension).AbsoluteUri;
        return new IptvChannel(
            item.StreamId.ToString(CultureInfo.InvariantCulture),
            profile.Id,
            Normalize(item.Name, $"Channel {item.StreamId}", 512),
            streamUrl,
            NormalizeOptional(item.StreamIcon, 2_048),
            group,
            NormalizeOptional(item.EpgChannelId, 512));
    }

    private static MovieItem MapMovie(
        IptvProfile profile,
        ProfileCredentials credentials,
        XtreamVodStreamDto item)
    {
        string extension = XtreamClient.NormalizeExtension(item.ContainerExtension);
        string streamUrl = XtreamClient.BuildStreamUri(
            profile.ServerAddress,
            "movie",
            credentials.Username!,
            credentials.Password,
            item.StreamId.ToString(CultureInfo.InvariantCulture),
            extension).AbsoluteUri;
        return new MovieItem(
            item.StreamId.ToString(CultureInfo.InvariantCulture),
            profile.Id,
            Normalize(item.Name, $"Movie {item.StreamId}", 512),
            CategoryId(profile.Id, ContentKind.Movie, item.CategoryId),
            streamUrl,
            NormalizeOptional(item.StreamIcon, 2_048),
            NormalizeOptional(item.Rating, 32),
            extension,
            NormalizeOptional(item.Plot, 4_096),
            NormalizeOptional(JsonScalar(item.Year) ?? item.ReleaseDate, 64),
            NormalizeOptional(item.Duration, 64));
    }

    private static SeriesItem MapSeries(IptvProfile profile, XtreamSeriesDto item) =>
        new(
            item.SeriesId.ToString(CultureInfo.InvariantCulture),
            profile.Id,
            Normalize(item.Name, $"Series {item.SeriesId}", 512),
            CategoryId(profile.Id, ContentKind.Series, item.CategoryId),
            NormalizeOptional(item.Cover, 2_048),
            NormalizeOptional(item.Plot, 4_096),
            NormalizeOptional(item.Genre, 512),
            NormalizeOptional(item.Rating, 32),
            NormalizeOptional(item.ReleaseDate, 64));

    private static Dictionary<string, string> CategoryNames(IEnumerable<XtreamCategoryDto> categories) =>
        categories
            .Where(item => !string.IsNullOrWhiteSpace(item.CategoryId))
            .GroupBy(item => item.CategoryId!.Trim(), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => Normalize(group.First().CategoryName, "Uncategorized", 256),
                StringComparer.Ordinal);

    private static string CategoryId(Guid profileId, ContentKind kind, string? rawId) =>
        $"xtream:{profileId:N}:{kind.ToString().ToLowerInvariant()}:{Normalize(rawId, "0", 128)}";

    private static bool IsAuthenticated(XtreamUserInfoDto? userInfo)
    {
        if (userInfo is null)
        {
            return false;
        }

        return userInfo.Auth.ValueKind switch
        {
            JsonValueKind.Number => userInfo.Auth.TryGetInt32(out int value) && value == 1,
            JsonValueKind.String => userInfo.Auth.GetString() == "1",
            JsonValueKind.True => true,
            _ => false,
        };
    }

    private static bool IsActive(string? status) =>
        string.IsNullOrWhiteSpace(status) || status.Equals("Active", StringComparison.OrdinalIgnoreCase);

    private static string SelectLiveExtension(IEnumerable<string>? formats) =>
        formats?.Any(format => format.Equals("m3u8", StringComparison.OrdinalIgnoreCase)) == true
            ? "m3u8"
            : "ts";

    private static string? JsonId(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };

    private static string? JsonScalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null,
    };

    private static string Normalize(string? value, string fallback, int maximumLength)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    private static string? NormalizeOptional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    [LoggerMessage(EventId = 5101, Level = LogLevel.Information, Message = "Loading Xtream catalog for profile {ProfileId}.")]
    private partial void LogCatalogLoadStarted(Guid profileId);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Information,
        Message = "Xtream catalog load completed for profile {ProfileId}. Live: {LiveCount}; Movies: {MovieCount}; Series: {SeriesCount}; Success: {Succeeded}.")]
    private partial void LogCatalogLoadCompleted(
        Guid profileId,
        int liveCount,
        int movieCount,
        int seriesCount,
        bool succeeded);
}
