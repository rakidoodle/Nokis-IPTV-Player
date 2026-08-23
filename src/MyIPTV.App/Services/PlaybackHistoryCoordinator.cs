using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.Services;

public sealed partial class PlaybackHistoryCoordinator(
    IPlaybackService playbackService,
    IWatchHistoryRepository historyRepository,
    ILogger<PlaybackHistoryCoordinator> logger) : IHostedService, IDisposable
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private CancellationTokenSource? _timerCancellation;
    private Task? _timerTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        playbackService.PlaybackChanged += OnPlaybackChanged;
        _timerCancellation = new CancellationTokenSource();
        _timerTask = RunTimerAsync(_timerCancellation.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        playbackService.PlaybackChanged -= OnPlaybackChanged;
        _timerCancellation?.Cancel();
        if (_timerTask is not null)
        {
            try
            {
                await _timerTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await SaveCurrentAsync(cancellationToken);
    }

    public void Dispose()
    {
        _timerCancellation?.Dispose();
        _saveGate.Dispose();
    }

    private async void OnPlaybackChanged(object? sender, EventArgs e)
    {
        if (playbackService.State is MediaPlaybackState.Opening or MediaPlaybackState.Playing or MediaPlaybackState.Paused or
            MediaPlaybackState.Stopped or MediaPlaybackState.Ended or MediaPlaybackState.Error)
        {
            await SaveSafelyAsync(CancellationToken.None);
        }
    }

    private async Task RunTimerAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (playbackService.State is MediaPlaybackState.Playing or MediaPlaybackState.Buffering)
            {
                await SaveSafelyAsync(cancellationToken);
            }
        }
    }

    private async Task SaveSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveCurrentAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogHistorySaveFailed(exception);
        }
    }

    private async Task SaveCurrentAsync(CancellationToken cancellationToken)
    {
        PlaybackRequest? request = playbackService.CurrentItem;
        if (request is null)
        {
            return;
        }

        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            TimeSpan position = request.ContentKind == ContentKind.LiveTv
                ? TimeSpan.Zero
                : playbackService.State == MediaPlaybackState.Opening && request.StartPosition.HasValue
                    ? request.StartPosition.Value
                    : playbackService.Position;
            await historyRepository.UpsertAsync(
                new WatchHistoryItem(
                    request.ProfileId ?? Guid.Empty,
                    request.ContentKind,
                    request.ContentId,
                    request.Title,
                    DateTimeOffset.UtcNow,
                    position,
                    request.ContentKind == ContentKind.LiveTv ? null : playbackService.Duration),
                cancellationToken);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    [LoggerMessage(EventId = 7101, Level = LogLevel.Error, Message = "Unable to save playback history.")]
    private partial void LogHistorySaveFailed(Exception exception);
}
