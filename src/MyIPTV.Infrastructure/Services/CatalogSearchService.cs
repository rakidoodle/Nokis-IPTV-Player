using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Services;

public sealed class CatalogSearchService(
    IChannelCatalog channelCatalog,
    IMediaCatalog mediaCatalog) : ISearchService
{
    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int maximumResults = 50,
        CancellationToken cancellationToken = default)
    {
        string normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>([]);
        }

        int limit = Math.Clamp(maximumResults, 1, 200);
        IReadOnlyList<IptvChannel> channels = channelCatalog.GetAll();
        IReadOnlyList<ContentCategory> categories = mediaCatalog.GetCategories();
        IReadOnlyList<MovieItem> movies = mediaCatalog.GetMovies();
        IReadOnlyList<SeriesItem> series = mediaCatalog.GetSeries();
        IReadOnlyList<EpisodeItem> episodes = mediaCatalog.GetEpisodes();

        return Task.Run<IReadOnlyList<SearchResult>>(() =>
        {
            List<RankedResult> matches = [];
            Add(matches, channels, item => item.Name, item => new(
                SearchResultKind.LiveChannel,
                item.Id,
                item.ProfileId,
                item.Name,
                $"Live TV · {item.Group}"), normalizedQuery, cancellationToken);
            Add(matches, movies, item => item.Name, item => new(
                SearchResultKind.Movie,
                item.Id,
                item.ProfileId,
                item.Name,
                "Movie"), normalizedQuery, cancellationToken);
            Add(matches, series, item => item.Name, item => new(
                SearchResultKind.Series,
                item.Id,
                item.ProfileId,
                item.Name,
                "Series"), normalizedQuery, cancellationToken);
            Add(matches, episodes, item => item.Name, item => new(
                SearchResultKind.Episode,
                item.Id,
                item.ProfileId,
                item.Name,
                $"Episode · S{item.SeasonNumber:N0} E{item.EpisodeNumber:N0}"), normalizedQuery, cancellationToken);
            Add(matches, categories, item => item.Name, item => new(
                SearchResultKind.Category,
                item.Id,
                item.ProfileId,
                item.Name,
                $"{item.Kind} category",
                item.Kind), normalizedQuery, cancellationToken);

            return matches
                .OrderBy(match => match.Rank)
                .ThenBy(match => match.Result.Title, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(match => match.Result)
                .ToArray();
        }, cancellationToken);
    }

    private static void Add<T>(
        ICollection<RankedResult> results,
        IEnumerable<T> source,
        Func<T, string> title,
        Func<T, SearchResult> map,
        string query,
        CancellationToken cancellationToken)
    {
        foreach (T item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string candidate = title(item);
            int index = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                results.Add(new(index == 0 ? 0 : 1, map(item)));
            }
        }
    }

    private sealed record RankedResult(int Rank, SearchResult Result);
}
