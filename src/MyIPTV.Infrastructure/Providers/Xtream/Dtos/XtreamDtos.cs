using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyIPTV.Infrastructure.Providers.Xtream.Dtos;

public sealed class XtreamAuthenticationDto
{
    [JsonPropertyName("user_info")]
    public XtreamUserInfoDto? UserInfo { get; init; }
}

public sealed class XtreamUserInfoDto
{
    [JsonPropertyName("auth")]
    public JsonElement Auth { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("allowed_output_formats")]
    public string[]? AllowedOutputFormats { get; init; }
}

public sealed class XtreamCategoryDto
{
    [JsonPropertyName("category_id")]
    public string? CategoryId { get; init; }

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; init; }
}

public sealed class XtreamLiveStreamDto
{
    [JsonPropertyName("stream_id")]
    public int StreamId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("stream_icon")]
    public string? StreamIcon { get; init; }

    [JsonPropertyName("epg_channel_id")]
    public string? EpgChannelId { get; init; }

    [JsonPropertyName("category_id")]
    public string? CategoryId { get; init; }
}

public sealed class XtreamVodStreamDto
{
    [JsonPropertyName("stream_id")]
    public int StreamId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("stream_icon")]
    public string? StreamIcon { get; init; }

    [JsonPropertyName("rating")]
    public string? Rating { get; init; }

    [JsonPropertyName("category_id")]
    public string? CategoryId { get; init; }

    [JsonPropertyName("container_extension")]
    public string? ContainerExtension { get; init; }

    [JsonPropertyName("plot")]
    public string? Plot { get; init; }

    [JsonPropertyName("year")]
    public JsonElement Year { get; init; }

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; init; }

    [JsonPropertyName("duration")]
    public string? Duration { get; init; }
}

public sealed class XtreamSeriesDto
{
    [JsonPropertyName("series_id")]
    public int SeriesId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("cover")]
    public string? Cover { get; init; }

    [JsonPropertyName("plot")]
    public string? Plot { get; init; }

    [JsonPropertyName("genre")]
    public string? Genre { get; init; }

    [JsonPropertyName("rating")]
    public string? Rating { get; init; }

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; init; }

    [JsonPropertyName("category_id")]
    public string? CategoryId { get; init; }
}

public sealed class XtreamSeriesInfoDto
{
    [JsonPropertyName("episodes")]
    public Dictionary<string, List<XtreamEpisodeDto>>? Episodes { get; init; }
}

public sealed class XtreamEpisodeDto
{
    [JsonPropertyName("id")]
    public JsonElement Id { get; init; }

    [JsonPropertyName("episode_num")]
    public int EpisodeNumber { get; init; }

    [JsonPropertyName("season")]
    public int SeasonNumber { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("container_extension")]
    public string? ContainerExtension { get; init; }

    [JsonPropertyName("info")]
    public XtreamEpisodeInfoDto? Info { get; init; }
}

public sealed class XtreamEpisodeInfoDto
{
    [JsonPropertyName("plot")]
    public string? Plot { get; init; }

    [JsonPropertyName("duration")]
    public string? Duration { get; init; }
}

public sealed class XtreamEpgResponseDto
{
    [JsonPropertyName("epg_listings")]
    public List<XtreamEpgListingDto>? Listings { get; init; }
}

public sealed class XtreamEpgListingDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("epg_id")]
    public string? EpgId { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("start_timestamp")]
    public long StartTimestamp { get; init; }

    [JsonPropertyName("stop_timestamp")]
    public long StopTimestamp { get; init; }
}
