using System.Globalization;
using Microsoft.Data.Sqlite;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Data;

public sealed class SqliteEpgRepository(IApplicationPaths paths) : IEpgRepository
{
    public async Task<EpgCacheState?> GetCacheStateAsync(
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT fetched_utc, expires_utc, entity_tag, last_modified_utc, program_count
            FROM epg_cache WHERE source_key = $sourceKey;
            """;
        command.Parameters.AddWithValue("$sourceKey", sourceKey);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new(
            sourceKey,
            ParseTimestamp(reader.GetString(0)),
            ParseTimestamp(reader.GetString(1)),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : ParseTimestamp(reader.GetString(3)),
            reader.GetInt32(4));
    }

    public async Task ReplaceAsync(
        string sourceKey,
        EpgParseResult data,
        EpgCacheState cacheState,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await DeleteSourceAsync(connection, transaction, sourceKey, cancellationToken);

        foreach (EpgChannel channel in data.Channels)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO epg_channels (source_key, channel_id, display_name) VALUES ($sourceKey, $id, $name);";
            command.Parameters.AddWithValue("$sourceKey", sourceKey);
            command.Parameters.AddWithValue("$id", channel.Id);
            command.Parameters.AddWithValue("$name", channel.DisplayName);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (EpgProgram program in data.Programs)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT OR REPLACE INTO epg_programs (
                    source_key, channel_id, start_utc, end_utc, title, description)
                VALUES ($sourceKey, $channelId, $startUtc, $endUtc, $title, $description);
                """;
            command.Parameters.AddWithValue("$sourceKey", sourceKey);
            command.Parameters.AddWithValue("$channelId", program.ChannelId);
            command.Parameters.AddWithValue("$startUtc", FormatTimestamp(program.StartUtc));
            command.Parameters.AddWithValue("$endUtc", FormatTimestamp(program.EndUtc));
            command.Parameters.AddWithValue("$title", program.Title);
            command.Parameters.AddWithValue("$description", (object?)program.Description ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpsertCacheAsync(connection, transaction, cacheState, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task TouchAsync(
        string sourceKey,
        DateTimeOffset fetchedUtc,
        DateTimeOffset expiresUtc,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE epg_cache SET fetched_utc = $fetchedUtc, expires_utc = $expiresUtc
            WHERE source_key = $sourceKey;
            """;
        command.Parameters.AddWithValue("$sourceKey", sourceKey);
        command.Parameters.AddWithValue("$fetchedUtc", FormatTimestamp(fetchedUtc));
        command.Parameters.AddWithValue("$expiresUtc", FormatTimestamp(expiresUtc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EpgChannelSchedule>> GetGuideAsync(
        string sourceKey,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default)
    {
        List<EpgChannelSchedule> schedules = [];
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand channelCommand = connection.CreateCommand();
        channelCommand.CommandText =
            "SELECT channel_id, display_name FROM epg_channels WHERE source_key = $sourceKey ORDER BY display_name COLLATE NOCASE;";
        channelCommand.Parameters.AddWithValue("$sourceKey", sourceKey);
        List<EpgChannel> channels = [];
        await using (SqliteDataReader reader = await channelCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                channels.Add(new(reader.GetString(0), reader.GetString(1)));
            }
        }

        foreach (EpgChannel channel in channels)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT start_utc, end_utc, title, description FROM epg_programs
                WHERE source_key = $sourceKey AND channel_id = $channelId
                  AND end_utc > $startUtc AND start_utc < $endUtc
                ORDER BY start_utc;
                """;
            command.Parameters.AddWithValue("$sourceKey", sourceKey);
            command.Parameters.AddWithValue("$channelId", channel.Id);
            command.Parameters.AddWithValue("$startUtc", FormatTimestamp(startUtc));
            command.Parameters.AddWithValue("$endUtc", FormatTimestamp(endUtc));
            List<EpgProgram> programs = [];
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                programs.Add(ReadProgram(channel.Id, reader));
            }

            if (programs.Count > 0)
            {
                schedules.Add(new(channel, programs));
            }
        }

        return schedules;
    }

    public async Task<EpgNowNext> GetNowNextAsync(
        string sourceKey,
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        EpgProgram? current = await ReadSingleAsync(connection, sourceKey, channelId,
            "start_utc <= $nowUtc AND end_utc > $nowUtc", nowUtc, cancellationToken);
        EpgProgram? next = await ReadSingleAsync(connection, sourceKey, channelId,
            "start_utc > $nowUtc", nowUtc, cancellationToken);
        return new(current, next);
    }

    private static async Task<EpgProgram?> ReadSingleAsync(
        SqliteConnection connection,
        string sourceKey,
        string channelId,
        string predicate,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT start_utc, end_utc, title, description FROM epg_programs WHERE source_key = $sourceKey AND channel_id = $channelId AND {predicate} ORDER BY start_utc LIMIT 1;";
        command.Parameters.AddWithValue("$sourceKey", sourceKey);
        command.Parameters.AddWithValue("$channelId", channelId);
        command.Parameters.AddWithValue("$nowUtc", FormatTimestamp(nowUtc));
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProgram(channelId, reader) : null;
    }

    private static async Task DeleteSourceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourceKey,
        CancellationToken cancellationToken)
    {
        foreach (string table in new[] { "epg_programs", "epg_channels", "epg_cache" })
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table} WHERE source_key = $sourceKey;";
            command.Parameters.AddWithValue("$sourceKey", sourceKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpsertCacheAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        EpgCacheState state,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO epg_cache (
                source_key, fetched_utc, expires_utc, entity_tag, last_modified_utc, program_count)
            VALUES ($sourceKey, $fetchedUtc, $expiresUtc, $entityTag, $lastModifiedUtc, $programCount);
            """;
        command.Parameters.AddWithValue("$sourceKey", state.SourceKey);
        command.Parameters.AddWithValue("$fetchedUtc", FormatTimestamp(state.FetchedUtc));
        command.Parameters.AddWithValue("$expiresUtc", FormatTimestamp(state.ExpiresUtc));
        command.Parameters.AddWithValue("$entityTag", (object?)state.EntityTag ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastModifiedUtc", state.LastModifiedUtc is null ? DBNull.Value : FormatTimestamp(state.LastModifiedUtc.Value));
        command.Parameters.AddWithValue("$programCount", state.ProgramCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new($"Data Source={paths.DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static EpgProgram ReadProgram(string channelId, SqliteDataReader reader) =>
        new(channelId, ParseTimestamp(reader.GetString(0)), ParseTimestamp(reader.GetString(1)),
            reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3));

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
}
