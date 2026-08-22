using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class ProfileRepositoryTests
{
    [TestMethod]
    public async Task RepositoryPersistsMetadataWithoutPasswordColumn()
    {
        using TemporaryApplicationPaths paths = new();
        SqliteDatabaseService database = new(paths, NullLogger<SqliteDatabaseService>.Instance);
        await database.InitializeAsync();
        SqliteProfileRepository repository = new(paths);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IptvProfile expected = new(
            Guid.NewGuid(),
            "Demo Profile",
            ProfileConnectionType.XtreamApi,
            "https://example.invalid",
            "demo-user",
            now,
            now);

        await repository.UpsertAsync(expected);
        IptvProfile? actual = await repository.GetByIdAsync(expected.Id);

        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.Id, actual.Id);
        Assert.AreEqual(expected.Name, actual.Name);
        Assert.AreEqual(expected.ServerAddress, actual.ServerAddress);

        await using (SqliteConnection connection = new($"Data Source={paths.DatabasePath}"))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(profiles);";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync();
            List<string> columns = [];
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            Assert.IsFalse(columns.Any(column => column.Contains("password", StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(columns.Any(column => column.Contains("token", StringComparison.OrdinalIgnoreCase)));
        }

        SqliteConnection.ClearAllPools();
    }
}
