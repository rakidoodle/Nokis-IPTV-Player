using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface ICatalogStateRepository
{
    Task ReplaceAllAsync(
        IReadOnlyList<IptvChannel> channels,
        IReadOnlyList<MovieItem> movies,
        IReadOnlyList<SeriesItem> series,
        IReadOnlyList<EpisodeItem> episodes,
        CancellationToken cancellationToken = default);

    Task<CatalogStateCounts> GetCountsAsync(CancellationToken cancellationToken = default);
}
