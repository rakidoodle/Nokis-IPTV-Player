using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.Infrastructure.Data;

public sealed partial class SqliteDatabaseService(
    IApplicationPaths paths,
    ILogger<SqliteDatabaseService> logger) : IDatabaseService
{
    private const string MigrationResourceMarker = ".Data.Migrations.";

    private readonly ILogger<SqliteDatabaseService> _logger = logger;

    public string DatabasePath => paths.DatabasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeCoreAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            LogDatabaseInitializationFailed(exception);
            throw;
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectoriesExist();

        SqliteConnectionStringBuilder connectionString = new()
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
        };

        await using SqliteConnection connection = new(connectionString.ToString());
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken);
        await EnsureMigrationTableAsync(connection, cancellationToken);
        await ApplyMigrationsAsync(connection, cancellationToken);

        LogDatabaseInitialized(paths.DatabasePath);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Task EnsureMigrationTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                applied_utc TEXT NOT NULL
            );
            """,
            cancellationToken);

    private async Task ApplyMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        Assembly assembly = typeof(SqliteDatabaseService).Assembly;
        string[] migrationResources = assembly
            .GetManifestResourceNames()
            .Where(name => name.Contains(MigrationResourceMarker, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (string resourceName in migrationResources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = resourceName[(resourceName.LastIndexOf(MigrationResourceMarker, StringComparison.Ordinal) + MigrationResourceMarker.Length)..];
            int separatorIndex = fileName.IndexOf('_');
            if (separatorIndex <= 0 ||
                !int.TryParse(fileName.AsSpan(0, separatorIndex), NumberStyles.None, CultureInfo.InvariantCulture, out int version))
            {
                throw new InvalidOperationException($"Invalid migration resource name: {resourceName}");
            }

            if (await IsMigrationAppliedAsync(connection, version, cancellationToken))
            {
                continue;
            }

            await using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Migration resource could not be opened: {resourceName}");
            using StreamReader reader = new(stream);
            string migrationSql = await reader.ReadToEndAsync(cancellationToken);

            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await using SqliteCommand migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = transaction;
            migrationCommand.CommandText = migrationSql;
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);

            await using SqliteCommand recordCommand = connection.CreateCommand();
            recordCommand.Transaction = transaction;
            recordCommand.CommandText =
                "INSERT INTO schema_migrations (version, name, applied_utc) VALUES ($version, $name, $appliedUtc);";
            recordCommand.Parameters.AddWithValue("$version", version);
            recordCommand.Parameters.AddWithValue("$name", fileName);
            recordCommand.Parameters.AddWithValue("$appliedUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await recordCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            LogMigrationApplied(version, fileName);
        }
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        SqliteConnection connection,
        int version,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = $version;";
        command.Parameters.AddWithValue("$version", version);
        long count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return count > 0;
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Database initialized at {DatabasePath}.")]
    private partial void LogDatabaseInitialized(string databasePath);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Applied database migration {Version}: {MigrationName}.")]
    private partial void LogMigrationApplied(int version, string migrationName);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Database initialization or migration failed.")]
    private partial void LogDatabaseInitializationFailed(Exception exception);
}
