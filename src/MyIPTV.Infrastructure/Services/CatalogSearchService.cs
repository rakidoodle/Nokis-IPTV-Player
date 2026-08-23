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
        return Task.Run<IReadOnlyList<SearchResult>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<IptvChannel> channels = channelCatalog.GetAll();
            IReadOnlyList<ContentCategory> categories = mediaCatalog.GetCategories();
            IReadOnlyList<MovieItem> movies = mediaCatalog.GetMovies();
            IReadOnlyList<SeriesItem> series = mediaCatalog.GetSeries();
            IReadOnlyList<EpisodeItem> episodes = mediaCatalog.GetEpisodes();
            BoundedResultSet matches = new(limit);
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

            return matches.Results;
        }, cancellationToken);
    }

    private static void Add<T>(
        BoundedResultSet results,
        IEnumerable<T> source,
        Func<T, string> title,
        Func<T, SearchResult> map,
        string query,
        CancellationToken cancellationToken)
    {
        int examined = 0;
        foreach (T item in source)
        {
            if ((examined++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            string candidate = title(item);
            int index = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                results.Add(new(index == 0 ? 0 : 1, map(item)));
            }
        }
    }

    private sealed record RankedResult(int Rank, SearchResult Result);

    private sealed class BoundedResultSet
    {
        private readonly int _limit;
        private readonly SortedSet<RankedResult> _items = new(RankedResultComparer.Instance);

        public BoundedResultSet(int limit) => _limit = limit;

        public IReadOnlyList<SearchResult> Results => _items.Select(item => item.Result).ToArray();

        public void Add(RankedResult item)
        {
            _items.Add(item);
            if (_items.Count > _limit && _items.Max is { } last) _items.Remove(last);
        }
    }

    private sealed class RankedResultComparer : IComparer<RankedResult>
    {
        public static RankedResultComparer Instance { get; } = new();

        public int Compare(RankedResult? left, RankedResult? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            int comparison = left.Rank.CompareTo(right.Rank);
            if (comparison != 0) return comparison;
            comparison = StringComparer.OrdinalIgnoreCase.Compare(left.Result.Title, right.Result.Title);
            if (comparison != 0) return comparison;
            comparison = left.Result.Kind.CompareTo(right.Result.Kind);
            if (comparison != 0) return comparison;
            comparison = left.Result.ProfileId.CompareTo(right.Result.ProfileId);
            if (comparison != 0) return comparison;
            return StringComparer.Ordinal.Compare(left.Result.Id, right.Result.Id);
        }
    }
}
