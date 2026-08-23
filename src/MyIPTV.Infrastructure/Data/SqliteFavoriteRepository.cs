using System.Globalization;
using Microsoft.Data.Sqlite;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Data;

public sealed class SqliteFavoriteRepository(IApplicationPaths paths) : IFavoriteRepository
{
    public event EventHandler? FavoritesChanged;

    public async Task<IReadOnlyList<FavoriteItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        List<FavoriteItem> favorites = [];
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT profile_id, content_type, content_id, title, added_utc
            FROM favorites
            ORDER BY added_utc DESC, title COLLATE NOCASE;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            favorites.Add(new(
                Guid.Parse(reader.GetString(0)),
                (ContentKind)reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(
                    reader.GetString(4),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind)));
        }

        return favorites;
    }

    public async Task<bool> ContainsAsync(
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
            SELECT EXISTS(
                SELECT 1 FROM favorites
                WHERE profile_id = $profileId
                  AND content_type = $contentType
                  AND content_id = $contentId);
            """;
        AddKeyParameters(command, profileId, contentKind, contentId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1L;
    }

    public async Task SetAsync(
        FavoriteItem favorite,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        ArgumentException.ThrowIfNullOrWhiteSpace(favorite.ContentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(favorite.Title);

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        if (isFavorite)
        {
            command.CommandText =
                """
                INSERT INTO favorites (profile_id, content_type, content_id, title, added_utc)
                VALUES ($profileId, $contentType, $contentId, $title, $addedUtc)
                ON CONFLICT(profile_id, content_type, content_id) DO UPDATE SET
                    title = excluded.title;
                """;
            command.Parameters.AddWithValue("$title", favorite.Title);
            command.Parameters.AddWithValue(
                "$addedUtc",
                favorite.AddedUtc.ToString("O", CultureInfo.InvariantCulture));
        }
        else
        {
            command.CommandText =
                """
                DELETE FROM favorites
                WHERE profile_id = $profileId
                  AND content_type = $contentType
                  AND content_id = $contentId;
                """;
        }

        AddKeyParameters(command, favorite.ProfileId, favorite.ContentKind, favorite.ContentId);
        int changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (changed > 0)
        {
            FavoritesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RemoveForProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM favorites WHERE profile_id = $profileId;";
        command.Parameters.AddWithValue("$profileId", profileId.ToString("D"));
        int changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (changed > 0)
        {
            FavoritesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new($"Data Source={paths.DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddKeyParameters(
        SqliteCommand command,
        Guid profileId,
        ContentKind contentKind,
        string contentId)
    {
        command.Parameters.AddWithValue("$profileId", profileId.ToString("D"));
        command.Parameters.AddWithValue("$contentType", (int)contentKind);
        command.Parameters.AddWithValue("$contentId", contentId);
    }
}
