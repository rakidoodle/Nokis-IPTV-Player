using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Tests;

internal sealed class FakePlaybackService : IPlaybackService, IPlaybackVideoSource
{
    public event EventHandler? PlaybackChanged;

    public object NativeMediaPlayer { get; } = new();

    public MediaPlaybackState State { get; private set; }

    public PlaybackRequest? CurrentItem { get; private set; }

    public string StatusMessage { get; private set; } = "Player idle";

    public int Volume { get; private set; } = 80;

    public bool IsMuted { get; private set; }

    public string AspectRatio { get; private set; } = "Default";

    public IReadOnlyList<PlaybackTrack> AudioTracks { get; private set; } = [];

    public IReadOnlyList<PlaybackTrack> SubtitleTracks { get; private set; } = [];

    public Task PlayAsync(PlaybackRequest request, CancellationToken cancellationToken = default)
    {
        CurrentItem = request;
        State = MediaPlaybackState.Playing;
        StatusMessage = $"Playing {request.Title}";
        RaiseChanged();
        return Task.CompletedTask;
    }

    public void Pause()
    {
        State = State == MediaPlaybackState.Playing
            ? MediaPlaybackState.Paused
            : MediaPlaybackState.Playing;
        RaiseChanged();
    }

    public void StopPlayback()
    {
        State = MediaPlaybackState.Stopped;
        StatusMessage = "Playback stopped";
        RaiseChanged();
    }

    public void SetVolume(int volume)
    {
        Volume = Math.Clamp(volume, 0, 100);
        RaiseChanged();
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        RaiseChanged();
    }

    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentItem is not null)
        {
            State = MediaPlaybackState.Playing;
            RaiseChanged();
        }

        return Task.CompletedTask;
    }

    public void SetAspectRatio(string aspectRatio)
    {
        AspectRatio = aspectRatio;
        RaiseChanged();
    }

    public void SelectAudioTrack(int trackId)
    {
    }

    public void SelectSubtitleTrack(int trackId)
    {
    }

    public void SetTracks(
        IReadOnlyList<PlaybackTrack> audioTracks,
        IReadOnlyList<PlaybackTrack> subtitleTracks)
    {
        AudioTracks = audioTracks;
        SubtitleTracks = subtitleTracks;
        RaiseChanged();
    }

    private void RaiseChanged() => PlaybackChanged?.Invoke(this, EventArgs.Empty);
}
