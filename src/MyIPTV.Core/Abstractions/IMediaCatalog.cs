using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IMediaCatalog
{
    event EventHandler? CatalogChanged;

    IReadOnlyList<ContentCategory> GetCategories();

    IReadOnlyList<MovieItem> GetMovies();

    IReadOnlyList<SeriesItem> GetSeries();

    IReadOnlyList<EpisodeItem> GetEpisodes();

    void ReplaceForProfile(
        Guid profileId,
        IReadOnlyList<ContentCategory> categories,
        IReadOnlyList<MovieItem> movies,
        IReadOnlyList<SeriesItem> series);

    void ReplaceSeriesEpisodes(
        Guid profileId,
        string seriesId,
        IReadOnlyList<EpisodeItem> episodes);

    void RemoveProfile(Guid profileId);
}
