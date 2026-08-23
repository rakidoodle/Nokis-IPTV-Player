using Microsoft.Data.Sqlite;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Data;

public sealed class SqliteCatalogStateRepository(IApplicationPaths paths) : ICatalogStateRepository
{
    public async Task ReplaceAllAsync(
        IReadOnlyList<IptvChannel> channels,
        IReadOnlyList<MovieItem> movies,
        IReadOnlyList<SeriesItem> series,
        IReadOnlyList<EpisodeItem> episodes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(movies);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(episodes);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, "DELETE FROM episodes; DELETE FROM series; DELETE FROM movies; DELETE FROM channels;", cancellationToken);

        foreach (IptvChannel item in channels)
        {
            await InsertAsync(connection, transaction,
                "INSERT INTO channels(profile_id, channel_id, name, group_name, epg_id) VALUES($p,$id,$name,$category,$extra);",
                cancellationToken,
                ("$p", item.ProfileId.ToString("D")), ("$id", item.Id), ("$name", item.Name),
                ("$category", item.Group), ("$extra", Db(item.EpgId)));
        }
        foreach (MovieItem item in movies)
        {
            await InsertAsync(connection, transaction,
                "INSERT INTO movies(profile_id, movie_id, name, category_id, rating, container_extension, description, year, duration) VALUES($p,$id,$name,$category,$extra,$container,$description,$year,$duration);",
                cancellationToken,
                ("$p", item.ProfileId.ToString("D")), ("$id", item.Id), ("$name", item.Name),
                ("$category", item.CategoryId), ("$extra", Db(item.Rating)), ("$container", Db(item.ContainerExtension)),
                ("$description", Db(item.Description)), ("$year", Db(item.Year)), ("$duration", Db(item.Duration)));
        }
        foreach (SeriesItem item in series)
        {
            await InsertAsync(connection, transaction,
                "INSERT INTO series(profile_id, series_id, name, category_id, plot, genre, rating, release_date) VALUES($p,$id,$name,$category,$extra,$container,$description,$year);",
                cancellationToken,
                ("$p", item.ProfileId.ToString("D")), ("$id", item.Id), ("$name", item.Name),
                ("$category", item.CategoryId), ("$extra", Db(item.Plot)), ("$container", Db(item.Genre)),
                ("$description", Db(item.Rating)), ("$year", Db(item.ReleaseDate)));
        }
        foreach (EpisodeItem item in episodes)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO episodes(profile_id, episode_id, series_id, season_number, episode_number, name, container_extension, plot, duration) VALUES($p,$id,$series,$season,$episode,$name,$container,$plot,$duration);";
            command.Parameters.AddWithValue("$p", item.ProfileId.ToString("D"));
            command.Parameters.AddWithValue("$id", item.Id);
            command.Parameters.AddWithValue("$series", item.SeriesId);
            command.Parameters.AddWithValue("$season", item.SeasonNumber);
            command.Parameters.AddWithValue("$episode", item.EpisodeNumber);
            command.Parameters.AddWithValue("$name", item.Name);
            command.Parameters.AddWithValue("$container", Db(item.ContainerExtension));
            command.Parameters.AddWithValue("$plot", Db(item.Plot));
            command.Parameters.AddWithValue("$duration", Db(item.Duration));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CatalogStateCounts> GetCountsAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return new(
            await CountAsync(connection, "channels", cancellationToken),
            await CountAsync(connection, "movies", cancellationToken),
            await CountAsync(connection, "series", cancellationToken),
            await CountAsync(connection, "episodes", cancellationToken));
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new($"Data Source={paths.DatabasePath};Mode=ReadWriteCreate;Cache=Shared;Pooling=False");
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "PRAGMA foreign_keys = ON;", cancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task InsertAsync(
        SqliteConnection connection, SqliteTransaction transaction, string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static object Db(string? value) => value is null ? DBNull.Value : value;
}
