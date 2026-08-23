using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.Services;

public sealed partial class CatalogPersistenceCoordinator(
    IChannelCatalog channelCatalog,
    IMediaCatalog mediaCatalog,
    ICatalogStateRepository repository,
    ILogger<CatalogPersistenceCoordinator> logger) : IHostedService
{
    private readonly object _gate = new();
    private Task _pendingWrite = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        channelCatalog.ChannelsChanged += OnCatalogChanged;
        mediaCatalog.CatalogChanged += OnCatalogChanged;
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        channelCatalog.ChannelsChanged -= OnCatalogChanged;
        mediaCatalog.CatalogChanged -= OnCatalogChanged;
        Task pending;
        lock (_gate) pending = _pendingWrite;
        await pending.WaitAsync(cancellationToken);
    }

    private void OnCatalogChanged(object? sender, EventArgs e) => ScheduleWrite();

    private void ScheduleWrite()
    {
        lock (_gate)
        {
            _pendingWrite = _pendingWrite.ContinueWith(
                static async (_, state) => await ((CatalogPersistenceCoordinator)state!).PersistAsync(),
                this,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default).Unwrap();
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            await repository.ReplaceAllAsync(
                channelCatalog.GetAll(),
                mediaCatalog.GetMovies(),
                mediaCatalog.GetSeries(),
                mediaCatalog.GetEpisodes());
            LogCatalogPersisted();
        }
        catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            LogCatalogPersistenceFailed(exception);
        }
    }

    [LoggerMessage(EventId = 7301, Level = LogLevel.Debug, Message = "Catalog metadata persisted.")]
    private partial void LogCatalogPersisted();

    [LoggerMessage(EventId = 7302, Level = LogLevel.Error, Message = "Catalog metadata could not be persisted.")]
    private partial void LogCatalogPersistenceFailed(Exception exception);
}
