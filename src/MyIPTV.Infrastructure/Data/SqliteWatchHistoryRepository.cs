using System.Globalization;
using Microsoft.Data.Sqlite;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Data;

public sealed class SqliteWatchHistoryRepository(IApplicationPaths paths) : IWatchHistoryRepository
{
    public event EventHandler? HistoryChanged;

    public async Task<IReadOnlyList<WatchHistoryItem>> GetRecentAsync(
        int maximumItems = 50,
        CancellationToken cancellationToken = default)
    {
        int limit = Math.Clamp(maximumItems, 1, 500);
        List<WatchHistoryItem> items = [];
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT profile_id, content_type, content_id, title, last_watched_utc,
                   position_ticks, duration_ticks
            FROM watch_history
            ORDER BY last_watched_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    public async Task<WatchHistoryItem?> GetAsync(
        Guid profileId,
        ContentKind contentKind,
        string contentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT profile_id, content_type, content_id, title, last_watched_utc,
                   position_ticks, duration_ticks
            FROM watch_history
            WHERE profile_id = $profileId AND content_type = $contentType AND content_id = $contentId;
            """;
        AddKeyParameters(command, profileId, contentKind, contentId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadItem(reader) : null;
    }

    public async Task UpsertAsync(WatchHistoryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.ContentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Title);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO watch_history (
                profile_id, content_type, content_id, title, last_watched_utc, position_ticks, duration_ticks)
            VALUES (
                $profileId, $contentType, $contentId, $title, $lastWatchedUtc, $positionTicks, $durationTicks)
            ON CONFLICT(profile_id, content_type, content_id) DO UPDATE SET
                title = excluded.title,
                last_watched_utc = excluded.last_watched_utc,
                position_ticks = excluded.position_ticks,
                duration_ticks = excluded.duration_ticks;
            """;
        AddKeyParameters(command, item.ProfileId, item.ContentKind, item.ContentId);
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$lastWatchedUtc", item.LastWatchedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$positionTicks", Math.Max(0L, item.Position.Ticks));
        command.Parameters.AddWithValue("$durationTicks", item.Duration is null ? DBNull.Value : Math.Max(0L, item.Duration.Value.Ticks));
        await command.ExecuteNonQueryAsync(cancellationToken);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        DeleteAsync("DELETE FROM watch_history;", null, cancellationToken);

    public Task RemoveForProfileAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        DeleteAsync("DELETE FROM watch_history WHERE profile_id = $profileId;", profileId, cancellationToken);

    private async Task DeleteAsync(string sql, Guid? profileId, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (profileId.HasValue)
        {
            command.Parameters.AddWithValue("$profileId", profileId.Value.ToString("D"));
        }

        int changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (changed > 0)
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new($"Data Source={paths.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddKeyParameters(SqliteCommand command, Guid profileId, ContentKind kind, string contentId)
    {
        command.Parameters.AddWithValue("$profileId", profileId.ToString("D"));
        command.Parameters.AddWithValue("$contentType", (int)kind);
        command.Parameters.AddWithValue("$contentId", contentId);
    }

    private static WatchHistoryItem ReadItem(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            (ContentKind)reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            TimeSpan.FromTicks(reader.GetInt64(5)),
            reader.IsDBNull(6) ? null : TimeSpan.FromTicks(reader.GetInt64(6)));
}
