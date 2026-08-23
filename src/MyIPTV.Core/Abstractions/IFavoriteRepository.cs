using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IFavoriteRepository
{
    event EventHandler? FavoritesChanged;

    Task<IReadOnlyList<FavoriteItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ContainsAsync(
        Guid profileId,
        ContentKind contentKind,
        string contentId,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        FavoriteItem favorite,
        bool isFavorite,
        CancellationToken cancellationToken = default);

    Task RemoveForProfileAsync(Guid profileId, CancellationToken cancellationToken = default);
}
