using MyIPTV.App.ViewModels;
using MyIPTV.Core.Models;

namespace MyIPTV.Tests;

[TestClass]
public sealed class PlayerViewModelTests
{
    [TestMethod]
    public async Task PlaybackStateUpdatesControlsAndMetadata()
    {
        FakePlaybackService playback = new();
        PlayerViewModel viewModel = new(playback, playback);
        PlaybackRequest request = new(
            "demo-news",
            ContentKind.LiveTv,
            "Demo News",
            "https://example.invalid/live.m3u8",
            CurrentProgram: "Morning News",
            NextProgram: "Weather");

        await playback.PlayAsync(request);

        Assert.AreEqual("Demo News", viewModel.Title);
        Assert.AreEqual("Morning News", viewModel.CurrentProgram);
        Assert.AreEqual("Weather", viewModel.NextProgram);
        Assert.IsTrue(viewModel.IsPlaying);
        Assert.AreEqual("Pause", viewModel.PlayPauseLabel);

        viewModel.PlayPauseCommand.Execute(null);
        Assert.IsFalse(viewModel.IsPlaying);
        Assert.AreEqual("Resume", viewModel.PlayPauseLabel);
    }

    [TestMethod]
    public void VolumeMuteAspectAndTracksStaySynchronized()
    {
        FakePlaybackService playback = new();
        PlayerViewModel viewModel = new(playback, playback);

        viewModel.Volume = 125;
        viewModel.ToggleMuteCommand.Execute(null);
        viewModel.SelectedAspectRatio = "16:9";
        playback.SetTracks(
            [new PlaybackTrack(1, "English")],
            [new PlaybackTrack(-1, "Disabled"), new PlaybackTrack(2, "English CC")]);

        Assert.AreEqual(100, viewModel.Volume);
        Assert.AreEqual(100, playback.Volume);
        Assert.IsTrue(viewModel.IsMuted);
        Assert.AreEqual("Unmute", viewModel.MuteLabel);
        Assert.AreEqual("16:9", playback.AspectRatio);
        Assert.HasCount(1, viewModel.AudioTracks);
        Assert.HasCount(2, viewModel.SubtitleTracks);
    }

    [TestMethod]
    public void PlaybackRequestTextRedactsStreamUrl()
    {
        const string secretUrl = "https://example.invalid/live/user/synthetic-secret/7.m3u8";
        PlaybackRequest request = new("7", ContentKind.LiveTv, "Demo", secretUrl);

        Assert.IsFalse(request.ToString().Contains(secretUrl, StringComparison.Ordinal));
        StringAssert.Contains(request.ToString(), "[REDACTED]");
    }
}
