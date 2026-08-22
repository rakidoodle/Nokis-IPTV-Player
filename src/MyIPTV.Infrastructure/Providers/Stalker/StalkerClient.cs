using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyIPTV.Infrastructure.Networking;
using MyIPTV.Infrastructure.Providers.Stalker.Dtos;

namespace MyIPTV.Infrastructure.Providers.Stalker;

public sealed class StalkerClient(IHttpClientFactory httpClientFactory) : IStalkerClient
{
    private const long MaximumResponseBytes = 64L * 1024 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<StalkerSession> AuthenticateAsync(
        string portalAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        Uri endpoint = BuildPortalEndpoint(portalAddress, "auth/token");
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password,
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("MyIPTV/1.0");

        using HttpResponseMessage response = await SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new StalkerClientException(
                StalkerClientError.Authentication,
                "The Ministra portal rejected the account credentials.");
        }

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            throw UnsupportedPortal();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new StalkerClientException(
                StalkerClientError.UnexpectedResponse,
                "The Ministra portal returned an unexpected authentication response.");
        }

        StalkerTokenDto token = await ReadJsonAsync<StalkerTokenDto>(response, cancellationToken);
        string userId = token.UserId.ValueKind switch
        {
            JsonValueKind.String => token.UserId.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => token.UserId.GetRawText(),
            _ => string.Empty,
        };
        if (string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(userId))
        {
            if (!string.IsNullOrWhiteSpace(token.Error))
            {
                throw new StalkerClientException(
                    StalkerClientError.Authentication,
                    "Authentication failed.");
            }

            throw UnsupportedPortal();
        }

        return new StalkerSession(token.AccessToken, userId, token.ExpiresIn);
    }

    public async Task<IReadOnlyList<StalkerChannelDto>> GetLiveChannelsAsync(
        string portalAddress,
        StalkerSession session,
        CancellationToken cancellationToken = default)
    {
        Uri endpoint = BuildPortalEndpoint(
            portalAddress,
            $"api/users/{Uri.EscapeDataString(session.UserId)}/tv-channels");
        using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("MyIPTV/1.0");

        using HttpResponseMessage response = await SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new StalkerClientException(
                StalkerClientError.Authentication,
                "The Ministra session was rejected.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw UnsupportedPortal();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new StalkerClientException(
                StalkerClientError.UnexpectedResponse,
                "The Ministra portal returned an unexpected channel response.");
        }

        using JsonDocument document = await ReadJsonAsync<JsonDocument>(response, cancellationToken);
        JsonElement channels = SelectResults(document.RootElement);
        if (channels.ValueKind != JsonValueKind.Array)
        {
            throw new StalkerClientException(
                StalkerClientError.UnexpectedResponse,
                "The Ministra portal returned malformed channel data.");
        }

        List<StalkerChannelDto> result = [];
        foreach (JsonElement item in channels.EnumerateArray())
        {
            string? id = Text(item, "id", "ch_id");
            string? name = Text(item, "name", "title");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            result.Add(new StalkerChannelDto(
                id.Trim(),
                name.Trim(),
                Text(item, "url", "stream_url", "cmd"),
                Text(item, "logo", "logo_url"),
                Text(item, "genre_name", "group", "category_name"),
                Text(item, "xmltv_id", "epg_id")));
        }

        return result;
    }

    internal static Uri BuildPortalEndpoint(string portalAddress, string relativePath)
    {
        Uri supplied = new(portalAddress, UriKind.Absolute);
        string path = supplied.AbsolutePath.Replace('\\', '/').TrimEnd('/');
        foreach (string suffix in new[] { "/server/load.php", "/portal.php", "/c/index.html", "/c" })
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                path = path[..^suffix.Length];
                break;
            }
        }

        UriBuilder rootBuilder = new(supplied)
        {
            Path = path + "/",
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return new Uri(rootBuilder.Uri, relativePath);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("Stalker");
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaximumResponseBytes)
        {
            throw new StalkerClientException(
                StalkerClientError.UnexpectedResponse,
                "The Ministra response is too large to process safely.");
        }

        try
        {
            await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using SizeLimitedReadStream limitedStream = new(responseStream, MaximumResponseBytes);
            T? value = await JsonSerializer.DeserializeAsync<T>(limitedStream, SerializerOptions, cancellationToken);
            return value ?? throw new JsonException("The response was empty.");
        }
        catch (JsonException exception)
        {
            throw new StalkerClientException(
                StalkerClientError.UnexpectedResponse,
                "The Ministra portal returned malformed data.",
                exception);
        }
        catch (InvalidDataException exception)
        {
            throw new StalkerClientException(
                StalkerClientError.UnexpectedResponse,
                "The Ministra response is too large to process safely.",
                exception);
        }
    }

    private static JsonElement SelectResults(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("results", out JsonElement results)
            ? results
            : default;
    }

    private static string? Text(JsonElement item, params string[] propertyNames)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (string name in propertyNames)
        {
            if (!item.TryGetProperty(name, out JsonElement value))
            {
                continue;
            }

            string? text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static StalkerClientException UnsupportedPortal() =>
        new(
            StalkerClientError.UnsupportedPortal,
            "This portal does not expose the supported username/password REST interface. " +
            "Device-bound or MAC-based portal access is not supported.");
}
