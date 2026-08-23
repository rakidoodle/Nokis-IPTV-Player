using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IWatchHistoryRepository
{
    event EventHandler? HistoryChanged;

    Task<IReadOnlyList<WatchHistoryItem>> GetRecentAsync(
        int maximumItems = 50,
        CancellationToken cancellationToken = default);

    Task<WatchHistoryItem?> GetAsync(
        Guid profileId,
        ContentKind contentKind,
        string contentId,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(WatchHistoryItem item, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    Task RemoveForProfileAsync(Guid profileId, CancellationToken cancellationToken = default);
}
