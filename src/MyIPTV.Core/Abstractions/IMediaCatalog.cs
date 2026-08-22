using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IMediaCatalog
{
    event EventHandler? CatalogChanged;

    IReadOnlyList<ContentCategory> GetCategories();

    IReadOnlyList<MovieItem> GetMovies();

    IReadOnlyList<SeriesItem> GetSeries();

    void ReplaceForProfile(
        Guid profileId,
        IReadOnlyList<ContentCategory> categories,
        IReadOnlyList<MovieItem> movies,
        IReadOnlyList<SeriesItem> series);

    void RemoveProfile(Guid profileId);
}
