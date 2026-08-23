using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Tests;

internal sealed class FakeFavoriteRepository : IFavoriteRepository
{
    private readonly List<FavoriteItem> _items = [];

    public event EventHandler? FavoritesChanged;

    public Task<IReadOnlyList<FavoriteItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FavoriteItem>>(_items.ToArray());

    public Task<bool> ContainsAsync(
        Guid profileId,
        ContentKind contentKind,
        string contentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.Any(item => item.ProfileId == profileId &&
            item.ContentKind == contentKind && item.ContentId == contentId));

    public Task SetAsync(
        FavoriteItem favorite,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(item => item.ProfileId == favorite.ProfileId &&
            item.ContentKind == favorite.ContentKind && item.ContentId == favorite.ContentId);
        if (isFavorite)
        {
            _items.Add(favorite);
        }

        FavoritesChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task RemoveForProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(item => item.ProfileId == profileId);
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
