using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Configuration;

public sealed partial class SqliteSettingsService(
    IApplicationPaths paths,
    JsonSettingsService jsonFallback,
    ILogger<SqliteSettingsService> logger) : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT value_json FROM settings WHERE setting_key = 'application';";
            object? stored = await command.ExecuteScalarAsync(cancellationToken);
            if (stored is string json)
            {
                return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
            }
        }
        catch (Exception exception) when (exception is SqliteException or JsonException)
        {
            LogSettingsDatabaseReadFailed(exception);
        }

        AppSettings fallback = await jsonFallback.LoadAsync(cancellationToken);
        await TrySaveDatabaseAsync(fallback, cancellationToken);
        return fallback;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await jsonFallback.SaveAsync(settings, cancellationToken);
        await TrySaveDatabaseAsync(settings, cancellationToken);
    }

    private async Task TrySaveDatabaseAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO settings(setting_key, value_json, updated_utc) VALUES('application', $json, $updated) " +
                "ON CONFLICT(setting_key) DO UPDATE SET value_json=excluded.value_json, updated_utc=excluded.updated_utc;";
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(settings, SerializerOptions));
            command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            LogSettingsDatabaseWriteFailed(exception);
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new($"Data Source={paths.DatabasePath};Mode=ReadWriteCreate;Cache=Shared;Pooling=False");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "SQLite settings could not be read; JSON fallback will be used.")]
    private partial void LogSettingsDatabaseReadFailed(Exception exception);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "SQLite settings could not be updated; JSON fallback remains available.")]
    private partial void LogSettingsDatabaseWriteFailed(Exception exception);
}
