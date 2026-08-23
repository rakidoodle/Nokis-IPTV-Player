using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Providers;

public sealed class InMemoryMediaCatalog : IMediaCatalog
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, ProfileMedia> _mediaByProfile = [];

    public event EventHandler? CatalogChanged;

    public IReadOnlyList<ContentCategory> GetCategories()
    {
        lock (_syncRoot)
        {
            return _mediaByProfile.Values.SelectMany(media => media.Categories).ToArray();
        }
    }

    public IReadOnlyList<MovieItem> GetMovies()
    {
        lock (_syncRoot)
        {
            return _mediaByProfile.Values.SelectMany(media => media.Movies).ToArray();
        }
    }

    public IReadOnlyList<SeriesItem> GetSeries()
    {
        lock (_syncRoot)
        {
            return _mediaByProfile.Values.SelectMany(media => media.Series).ToArray();
        }
    }

    public IReadOnlyList<EpisodeItem> GetEpisodes()
    {
        lock (_syncRoot)
        {
            return _mediaByProfile.Values.SelectMany(media => media.Episodes).ToArray();
        }
    }

    public void ReplaceForProfile(
        Guid profileId,
        IReadOnlyList<ContentCategory> categories,
        IReadOnlyList<MovieItem> movies,
        IReadOnlyList<SeriesItem> series)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(movies);
        ArgumentNullException.ThrowIfNull(series);
        if (categories.Any(item => item.ProfileId != profileId) ||
            movies.Any(item => item.ProfileId != profileId) ||
            series.Any(item => item.ProfileId != profileId))
        {
            throw new ArgumentException("Every catalog item must belong to the supplied profile.");
        }

        lock (_syncRoot)
        {
            _mediaByProfile[profileId] = new(
                categories.ToArray(),
                movies.ToArray(),
                series.ToArray(),
                []);
        }

        CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReplaceSeriesEpisodes(
        Guid profileId,
        string seriesId,
        IReadOnlyList<EpisodeItem> episodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesId);
        ArgumentNullException.ThrowIfNull(episodes);
        if (episodes.Any(item => item.ProfileId != profileId || item.SeriesId != seriesId))
        {
            throw new ArgumentException("Every episode must belong to the supplied profile and series.", nameof(episodes));
        }

        lock (_syncRoot)
        {
            ProfileMedia existing = _mediaByProfile.TryGetValue(profileId, out ProfileMedia? media)
                ? media
                : new([], [], [], []);
            EpisodeItem[] merged = existing.Episodes
                .Where(item => item.SeriesId != seriesId)
                .Concat(episodes)
                .ToArray();
            _mediaByProfile[profileId] = existing with { Episodes = merged };
        }

        CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveProfile(Guid profileId)
    {
        bool removed;
        lock (_syncRoot)
        {
            removed = _mediaByProfile.Remove(profileId);
        }

        if (removed)
        {
            CatalogChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed record ProfileMedia(
        ContentCategory[] Categories,
        MovieItem[] Movies,
        SeriesItem[] Series,
        EpisodeItem[] Episodes);
}
