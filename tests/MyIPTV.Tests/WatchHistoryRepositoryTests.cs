using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class WatchHistoryRepositoryTests
{
    [TestMethod]
    public async Task WatchHistoryPersistsPositionAndOrdersMostRecentFirst()
    {
        using TemporaryApplicationPaths paths = new();
        await new SqliteDatabaseService(paths, NullLogger<SqliteDatabaseService>.Instance).InitializeAsync();
        SqliteWatchHistoryRepository repository = new(paths);
        Guid profileId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await repository.UpsertAsync(new(
            profileId, ContentKind.Movie, "older", "Older Movie", now.AddMinutes(-5),
            TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(90)));
        await repository.UpsertAsync(new(
            profileId, ContentKind.Series, "newer", "Newer Episode", now,
            TimeSpan.FromMinutes(7), TimeSpan.FromMinutes(45)));

        SqliteWatchHistoryRepository restarted = new(paths);
        IReadOnlyList<WatchHistoryItem> recent = await restarted.GetRecentAsync();

        Assert.HasCount(2, recent);
        Assert.AreEqual("newer", recent[0].ContentId);
        Assert.AreEqual(TimeSpan.FromMinutes(12), recent[1].Position);
        Assert.AreEqual(TimeSpan.FromMinutes(90), recent[1].Duration);
        SqliteConnection.ClearAllPools();
    }

    [TestMethod]
    public async Task ClearRemovesAllHistory()
    {
        using TemporaryApplicationPaths paths = new();
        await new SqliteDatabaseService(paths, NullLogger<SqliteDatabaseService>.Instance).InitializeAsync();
        SqliteWatchHistoryRepository repository = new(paths);
        await repository.UpsertAsync(new(
            Guid.NewGuid(), ContentKind.LiveTv, "channel", "Demo Channel",
            DateTimeOffset.UtcNow, TimeSpan.Zero, null));

        await repository.ClearAsync();

        Assert.IsEmpty(await repository.GetRecentAsync());
        SqliteConnection.ClearAllPools();
    }
}
