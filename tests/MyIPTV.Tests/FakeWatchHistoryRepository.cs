using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Tests;

internal sealed class FakeWatchHistoryRepository : IWatchHistoryRepository
{
    private readonly List<WatchHistoryItem> _items = [];

    public event EventHandler? HistoryChanged;

    public Task<IReadOnlyList<WatchHistoryItem>> GetRecentAsync(
        int maximumItems = 50,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WatchHistoryItem>>(_items
            .OrderByDescending(item => item.LastWatchedUtc)
            .Take(maximumItems)
            .ToArray());

    public Task<WatchHistoryItem?> GetAsync(
        Guid profileId,
        ContentKind contentKind,
        string contentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.FirstOrDefault(item => item.ProfileId == profileId &&
            item.ContentKind == contentKind && item.ContentId == contentId));

    public Task UpsertAsync(WatchHistoryItem item, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(existing => existing.ProfileId == item.ProfileId &&
            existing.ContentKind == item.ContentKind && existing.ContentId == item.ContentId);
        _items.Add(item);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _items.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task RemoveForProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(item => item.ProfileId == profileId);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
