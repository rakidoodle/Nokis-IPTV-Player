using System.Globalization;
using Microsoft.Data.Sqlite;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Data;

public sealed class SqliteProfileRepository(IApplicationPaths paths) : IProfileRepository
{
    public event EventHandler? ProfilesChanged;

    public async Task<IReadOnlyList<IptvProfile>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        List<IptvProfile> profiles = [];
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, connection_type, server_address, username, created_utc, updated_utc
            FROM profiles
            ORDER BY name COLLATE NOCASE, created_utc;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(ReadProfile(reader));
        }

        return profiles;
    }

    public async Task<IptvProfile?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, connection_type, server_address, username, created_utc, updated_utc
            FROM profiles
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProfile(reader) : null;
    }

    public async Task UpsertAsync(
        IptvProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO profiles (
                id, name, connection_type, server_address, username, created_utc, updated_utc)
            VALUES (
                $id, $name, $connectionType, $serverAddress, $username, $createdUtc, $updatedUtc)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                connection_type = excluded.connection_type,
                server_address = excluded.server_address,
                username = excluded.username,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$id", profile.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$connectionType", (int)profile.ConnectionType);
        command.Parameters.AddWithValue("$serverAddress", profile.ServerAddress);
        command.Parameters.AddWithValue("$username", (object?)profile.Username ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdUtc", profile.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updatedUtc", profile.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM profiles WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new($"Data Source={paths.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static IptvProfile ReadProfile(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            (ProfileConnectionType)reader.GetInt32(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
}
