using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int maximumResults = 50,
        CancellationToken cancellationToken = default);
}
