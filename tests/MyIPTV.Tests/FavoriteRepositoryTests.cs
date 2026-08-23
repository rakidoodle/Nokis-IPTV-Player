using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class FavoriteRepositoryTests
{
    [TestMethod]
    public async Task FavoritePersistsAcrossRepositoryInstancesAndCanBeRemoved()
    {
        using TemporaryApplicationPaths paths = new();
        SqliteDatabaseService database = new(paths, NullLogger<SqliteDatabaseService>.Instance);
        await database.InitializeAsync();
        FavoriteItem favorite = new(
            Guid.NewGuid(), ContentKind.LiveTv, "demo-news", "Demo News", DateTimeOffset.UtcNow);

        SqliteFavoriteRepository first = new(paths);
        await first.SetAsync(favorite, true);
        SqliteFavoriteRepository second = new(paths);

        Assert.IsTrue(await second.ContainsAsync(favorite.ProfileId, favorite.ContentKind, favorite.ContentId));
        IReadOnlyList<FavoriteItem> loaded = await second.GetAllAsync();
        Assert.HasCount(1, loaded);
        Assert.AreEqual("Demo News", loaded[0].Title);

        await second.SetAsync(favorite, false);
        Assert.IsFalse(await first.ContainsAsync(favorite.ProfileId, favorite.ContentKind, favorite.ContentId));
        SqliteConnection.ClearAllPools();
    }

    [TestMethod]
    public async Task FavoriteUpdateUsesStableCompositeIdWithoutDuplicates()
    {
        using TemporaryApplicationPaths paths = new();
        SqliteDatabaseService database = new(paths, NullLogger<SqliteDatabaseService>.Instance);
        await database.InitializeAsync();
        Guid profileId = Guid.NewGuid();
        SqliteFavoriteRepository repository = new(paths);

        await repository.SetAsync(new(profileId, ContentKind.Movie, "movie-1", "Old title", DateTimeOffset.UtcNow), true);
        await repository.SetAsync(new(profileId, ContentKind.Movie, "movie-1", "New title", DateTimeOffset.UtcNow), true);

        IReadOnlyList<FavoriteItem> loaded = await repository.GetAllAsync();
        Assert.HasCount(1, loaded);
        Assert.AreEqual("New title", loaded[0].Title);
        SqliteConnection.ClearAllPools();
    }
}
