using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyIPTV.Infrastructure.Networking;
using MyIPTV.Infrastructure.Providers.Xtream.Dtos;

namespace MyIPTV.Infrastructure.Providers.Xtream;

public sealed class XtreamClient(IHttpClientFactory httpClientFactory) : IXtreamClient
{
    private const long MaximumResponseBytes = 64L * 1024 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters =
        {
            new FlexibleStringConverter(),
            new FlexibleStringArrayConverter(),
            new FlexibleListConverterFactory(),
        },
    };

    public Task<XtreamAuthenticationDto> AuthenticateAsync(
        string serverAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default) =>
        GetAsync<XtreamAuthenticationDto>(
            serverAddress,
            username,
            password,
            action: null,
            parameters: null,
            cancellationToken);

    public async Task<IReadOnlyList<XtreamCategoryDto>> GetCategoriesAsync(
        string serverAddress,
        string username,
        string password,
        string action,
        CancellationToken cancellationToken = default) =>
        await GetAsync<List<XtreamCategoryDto>>(
            serverAddress,
            username,
            password,
            action,
            parameters: null,
            cancellationToken);

    public async Task<IReadOnlyList<XtreamLiveStreamDto>> GetLiveStreamsAsync(
        string serverAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default) =>
        await GetAsync<List<XtreamLiveStreamDto>>(
            serverAddress,
            username,
            password,
            "get_live_streams",
            parameters: null,
            cancellationToken);

    public async Task<IReadOnlyList<XtreamVodStreamDto>> GetVodStreamsAsync(
        string serverAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default) =>
        await GetAsync<List<XtreamVodStreamDto>>(
            serverAddress,
            username,
            password,
            "get_vod_streams",
            parameters: null,
            cancellationToken);

    public async Task<IReadOnlyList<XtreamSeriesDto>> GetSeriesAsync(
        string serverAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default) =>
        await GetAsync<List<XtreamSeriesDto>>(
            serverAddress,
            username,
            password,
            "get_series",
            parameters: null,
            cancellationToken);

    public Task<XtreamSeriesInfoDto> GetSeriesInfoAsync(
        string serverAddress,
        string username,
        string password,
        string seriesId,
        CancellationToken cancellationToken = default) =>
        GetAsync<XtreamSeriesInfoDto>(
            serverAddress,
            username,
            password,
            "get_series_info",
            new Dictionary<string, string> { ["series_id"] = seriesId },
            cancellationToken);

    public Task<XtreamEpgResponseDto> GetShortEpgAsync(
        string serverAddress,
        string username,
        string password,
        string streamId,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetAsync<XtreamEpgResponseDto>(
            serverAddress,
            username,
            password,
            "get_short_epg",
            new Dictionary<string, string>
            {
                ["stream_id"] = streamId,
                ["limit"] = Math.Clamp(limit, 1, 100).ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            cancellationToken);

    private async Task<T> GetAsync<T>(
        string serverAddress,
        string username,
        string password,
        string? action,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        Uri requestUri = BuildApiUri(serverAddress, username, password, action, parameters);
        HttpClient client = httpClientFactory.CreateClient("Xtream");
        using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.ParseAdd("MyIPTV/1.1");
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new XtreamClientException(
                XtreamClientError.Authentication,
                "The Xtream server rejected the account credentials.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new XtreamClientException(
                XtreamClientError.UnexpectedResponse,
                "The Xtream server returned an unexpected response.");
        }

        if (response.Content.Headers.ContentLength > MaximumResponseBytes)
        {
            throw new XtreamClientException(
                XtreamClientError.UnexpectedResponse,
                "The Xtream response is too large to process safely.");
        }

        try
        {
            await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using SizeLimitedReadStream limitedStream = new(responseStream, MaximumResponseBytes);
            T? value = await JsonSerializer.DeserializeAsync<T>(
                limitedStream,
                SerializerOptions,
                cancellationToken);
            return value ?? throw new JsonException("The response was empty.");
        }
        catch (JsonException exception)
        {
            throw new XtreamClientException(
                XtreamClientError.UnexpectedResponse,
                "The Xtream server returned malformed data.",
                exception);
        }
        catch (InvalidDataException exception)
        {
            throw new XtreamClientException(
                XtreamClientError.UnexpectedResponse,
                "The Xtream response is too large to process safely.",
                exception);
        }
    }

    private static Uri BuildApiUri(
        string serverAddress,
        string username,
        string password,
        string? action,
        IReadOnlyDictionary<string, string>? parameters)
    {
        Uri suppliedServer = new(serverAddress, UriKind.Absolute);
        Uri endpoint = suppliedServer.AbsolutePath.EndsWith("player_api.php", StringComparison.OrdinalIgnoreCase)
            ? suppliedServer
            : new Uri(new Uri(AppendDirectorySeparator(serverAddress), UriKind.Absolute), "player_api.php");
        List<string> query =
        [
            Pair("username", username),
            Pair("password", password),
        ];
        if (!string.IsNullOrEmpty(action))
        {
            query.Add(Pair("action", action));
        }

        if (parameters is not null)
        {
            query.AddRange(parameters.Select(pair => Pair(pair.Key, pair.Value)));
        }

        UriBuilder builder = new(endpoint) { Query = string.Join('&', query) };
        return builder.Uri;
    }

    internal static Uri BuildStreamUri(
        string serverAddress,
        string contentPath,
        string username,
        string password,
        string streamId,
        string extension)
    {
        Uri server = new(AppendDirectorySeparator(serverAddress), UriKind.Absolute);
        string relativePath = string.Join(
            '/',
            contentPath,
            Uri.EscapeDataString(username),
            Uri.EscapeDataString(password),
            $"{Uri.EscapeDataString(streamId)}.{NormalizeExtension(extension)}");
        return new Uri(server, relativePath);
    }

    internal static string NormalizeExtension(string? extension)
    {
        string normalized = extension?.Trim().TrimStart('.').ToLowerInvariant() ?? string.Empty;
        return normalized.Length is >= 1 and <= 10 && normalized.All(char.IsLetterOrDigit)
            ? normalized
            : "ts";
    }

    private static string AppendDirectorySeparator(string serverAddress) =>
        serverAddress.EndsWith('/') ? serverAddress : serverAddress + "/";

    private static string Pair(string name, string value) =>
        $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
}
