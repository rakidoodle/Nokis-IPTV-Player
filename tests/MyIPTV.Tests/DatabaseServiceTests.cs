using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class DatabaseServiceTests
{
    [TestMethod]
    public async Task InitializeAsyncCreatesSchemaAndRecordsMigration()
    {
        using TemporaryApplicationPaths paths = new();
        SqliteDatabaseService service = new(paths, NullLogger<SqliteDatabaseService>.Instance);

        await service.InitializeAsync();
        await service.InitializeAsync();

        await using (SqliteConnection connection = new($"Data Source={paths.DatabasePath}"))
        {
            await connection.OpenAsync();

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT (SELECT COUNT(*) FROM schema_migrations), " +
                "(SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'app_metadata');";

            await using SqliteDataReader reader = await command.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual(5L, reader.GetInt64(0));
            Assert.AreEqual(1L, reader.GetInt64(1));
        }

        SqliteConnection.ClearAllPools();
    }
}
