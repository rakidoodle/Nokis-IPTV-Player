using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Configuration;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class SqliteSettingsServiceTests
{
    [TestMethod]
    public async Task SavesSettingsToSqliteAndJsonFallback()
    {
        using TemporaryApplicationPaths paths = new();
        await new SqliteDatabaseService(paths, NullLogger<SqliteDatabaseService>.Instance).InitializeAsync();
        JsonSettingsService json = new(paths, NullLogger<JsonSettingsService>.Instance);
        SqliteSettingsService service = new(paths, json, NullLogger<SqliteSettingsService>.Instance);
        AppSettings expected = new() { Theme = "Light", DefaultVolume = 55, EpgRefreshHours = 12 };

        await service.SaveAsync(expected);
        await File.WriteAllTextAsync(paths.SettingsPath, "{ malformed fallback");

        Assert.AreEqual(expected, await service.LoadAsync());
    }
}
