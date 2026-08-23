using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IPlaybackService
{
    event EventHandler? PlaybackChanged;

    MediaPlaybackState State { get; }

    PlaybackRequest? CurrentItem { get; }

    string StatusMessage { get; }

    int Volume { get; }

    bool IsMuted { get; }

    string AspectRatio { get; }

    TimeSpan Position { get; }

    TimeSpan? Duration { get; }

    IReadOnlyList<PlaybackTrack> AudioTracks { get; }

    IReadOnlyList<PlaybackTrack> SubtitleTracks { get; }

    Task PlayAsync(PlaybackRequest request, CancellationToken cancellationToken = default);

    void Pause();

    void StopPlayback();

    void SetVolume(int volume);

    void ToggleMute();

    Task ReconnectAsync(CancellationToken cancellationToken = default);

    void SetAspectRatio(string aspectRatio);

    void SelectAudioTrack(int trackId);

    void SelectSubtitleTrack(int trackId);
}
