using System.Text.Json.Serialization;
using System.Text.Json;

namespace MyIPTV.Infrastructure.Providers.Stalker.Dtos;

public sealed record StalkerTokenDto
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("user_id")]
    public JsonElement UserId { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

public sealed record StalkerChannelDto(
    string Id,
    string Name,
    string? StreamUrl,
    string? LogoUrl,
    string? Group,
    string? EpgId);
