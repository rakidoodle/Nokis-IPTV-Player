using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Configuration;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class CatalogStateRepositoryTests
{
    [TestMethod]
    public async Task PersistsCatalogMetadataWithoutPlaybackAddresses()
    {
        using TemporaryApplicationPaths paths = new();
        await new SqliteDatabaseService(paths, NullLogger<SqliteDatabaseService>.Instance).InitializeAsync();
        Guid profileId = Guid.NewGuid();
        await new SqliteProfileRepository(paths).UpsertAsync(new(
            profileId, "Demo", ProfileConnectionType.XtreamApi, "https://example.invalid", "demo",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        SqliteCatalogStateRepository repository = new(paths);
        const string sensitiveUrl = "https://example.invalid/live/demo/do-not-store/1.ts";

        await repository.ReplaceAllAsync(
            [new("channel-1", profileId, "Demo News", sensitiveUrl, null, "News", "demo.news")],
            [new("movie-1", profileId, "Demo Movie", "movies", sensitiveUrl, null, "8", "mp4")],
            [new("series-1", profileId, "Demo Series", "series", null, "Plot", "Drama", "9", "2026")],
            [new("episode-1", profileId, "series-1", 1, 1, "Pilot", sensitiveUrl, "mp4", "Plot", "42m")]);

        Assert.AreEqual(new CatalogStateCounts(1, 1, 1, 1), await repository.GetCountsAsync());
        await using SqliteConnection connection = new($"Data Source={paths.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT group_concat(name, ',') FROM pragma_table_info('channels');";
        string columns = (string)(await command.ExecuteScalarAsync() ?? string.Empty);
        Assert.DoesNotContain("url", columns, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", columns, StringComparison.OrdinalIgnoreCase);
        command.CommandText =
            "SELECT group_concat(value, '|') FROM (" +
            "SELECT name || ifnull(group_name,'') || ifnull(epg_id,'') AS value FROM channels UNION ALL " +
            "SELECT name || category_id || ifnull(description,'') FROM movies UNION ALL " +
            "SELECT name || category_id || ifnull(plot,'') FROM series UNION ALL " +
            "SELECT name || ifnull(plot,'') FROM episodes);";
        string storedValues = (string)(await command.ExecuteScalarAsync() ?? string.Empty);
        Assert.DoesNotContain("do-not-store", storedValues);
        Assert.DoesNotContain("https://", storedValues);
    }

    [TestMethod]
    public async Task ReplacingCatalogRemovesStaleMetadata()
    {
        using TemporaryApplicationPaths paths = new();
        await new SqliteDatabaseService(paths, NullLogger<SqliteDatabaseService>.Instance).InitializeAsync();
        Guid profileId = Guid.NewGuid();
        await new SqliteProfileRepository(paths).UpsertAsync(new(
            profileId, "Demo", ProfileConnectionType.M3uPlaylist, "C:\\demo.m3u", "demo",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        SqliteCatalogStateRepository repository = new(paths);
        await repository.ReplaceAllAsync(
            [new("one", profileId, "One", "https://example.invalid/one", null, "General", null)], [], [], []);

        await repository.ReplaceAllAsync([], [], [], []);

        Assert.AreEqual(new CatalogStateCounts(0, 0, 0, 0), await repository.GetCountsAsync());
    }
}
