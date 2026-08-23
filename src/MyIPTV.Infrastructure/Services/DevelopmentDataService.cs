using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Services;

public sealed class DevelopmentDataService(
    IProfileRepository profiles,
    IChannelCatalog channels,
    IMediaCatalog media) : IDevelopmentDataService
{
    public static Guid DemoProfileId { get; } = Guid.Parse("de6d0a11-75c9-4e0e-9f6f-1c13cb1d2480");

    public bool IsLoaded => channels.GetForProfile(DemoProfileId).Count > 0 ||
                            media.GetMovies().Any(item => item.ProfileId == DemoProfileId);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await profiles.UpsertAsync(new(
            DemoProfileId,
            "Development Demo",
            ProfileConnectionType.M3uPlaylist,
            "https://example.invalid/myiptv-development.m3u",
            string.Empty,
            now,
            now), cancellationToken);

        channels.ReplaceForProfile(DemoProfileId,
        [
            new("demo-news", DemoProfileId, "Demo News", "https://example.invalid/live/news.m3u8", null, "Demo News", "demo.news"),
            new("demo-sports", DemoProfileId, "Demo Sports", "https://example.invalid/live/sports.m3u8", null, "Demo Sports", "demo.sports"),
            new("demo-culture", DemoProfileId, "Demo Culture", "https://example.invalid/live/culture.m3u8", null, "Demo General", "demo.culture"),
        ]);
        media.ReplaceForProfile(DemoProfileId,
        [
            new("demo-movies", DemoProfileId, "Demo Movies", ContentKind.Movie),
            new("demo-series", DemoProfileId, "Demo Series", ContentKind.Series),
        ],
        [
            new("demo-movie-voyage", DemoProfileId, "Demo Voyage", "demo-movies",
                "https://example.invalid/vod/voyage.mp4", null, "8.2", "mp4",
                "A synthetic journey used to preview the movie browser.", "2026", "1h 32m"),
            new("demo-movie-nature", DemoProfileId, "Demo Nature", "demo-movies",
                "https://example.invalid/vod/nature.mp4", null, "7.9", "mp4",
                "Non-functional development metadata for UI testing.", "2025", "58m"),
        ],
        [
            new("demo-series-explorers", DemoProfileId, "Demo Explorers", "demo-series", null,
                "A safe synthetic show for season and episode layouts.", "Documentary", "8.7", "2026"),
        ]);
        media.ReplaceSeriesEpisodes(DemoProfileId, "demo-series-explorers",
        [
            new("demo-episode-1", DemoProfileId, "demo-series-explorers", 1, 1, "A New Map",
                "https://example.invalid/series/explorers/s01e01.mp4", "mp4", "The demo journey begins.", "42m"),
            new("demo-episode-2", DemoProfileId, "demo-series-explorers", 1, 2, "The Long Route",
                "https://example.invalid/series/explorers/s01e02.mp4", "mp4", "A second non-functional episode.", "44m"),
        ]);
    }

    public async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        channels.RemoveProfile(DemoProfileId);
        media.RemoveProfile(DemoProfileId);
        await profiles.DeleteAsync(DemoProfileId, cancellationToken);
    }
}
