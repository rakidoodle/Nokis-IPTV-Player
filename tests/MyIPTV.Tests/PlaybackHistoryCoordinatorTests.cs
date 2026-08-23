using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.App.Services;
using MyIPTV.Core.Models;

namespace MyIPTV.Tests;

[TestClass]
public sealed class PlaybackHistoryCoordinatorTests
{
    [TestMethod]
    public async Task PlaybackEventsRecordAndUpdateVodProgress()
    {
        FakePlaybackService playback = new();
        FakeWatchHistoryRepository history = new();
        using PlaybackHistoryCoordinator coordinator = new(
            playback, history, NullLogger<PlaybackHistoryCoordinator>.Instance);
        await coordinator.StartAsync(CancellationToken.None);
        Guid profileId = Guid.NewGuid();

        await playback.PlayAsync(new(
            "movie-1", ContentKind.Movie, "Demo Movie", "https://example.invalid/movie.mp4",
            StartPosition: TimeSpan.FromMinutes(3), ProfileId: profileId));
        playback.Position = TimeSpan.FromMinutes(9);
        playback.Duration = TimeSpan.FromMinutes(90);
        playback.Pause();

        WatchHistoryItem? saved = await history.GetAsync(profileId, ContentKind.Movie, "movie-1");
        Assert.IsNotNull(saved);
        Assert.AreEqual(TimeSpan.FromMinutes(9), saved.Position);
        Assert.AreEqual(TimeSpan.FromMinutes(90), saved.Duration);

        await coordinator.StopAsync(CancellationToken.None);
    }
}
