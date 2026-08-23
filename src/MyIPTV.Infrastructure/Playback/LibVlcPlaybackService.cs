using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Playback;

public sealed partial class LibVlcPlaybackService : IPlaybackService, IPlaybackVideoSource, IDisposable
{
    private static readonly HashSet<string> SupportedAspectRatios =
        new(StringComparer.OrdinalIgnoreCase) { "Fit", "16:9", "4:3", "21:9", "1:1" };
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly LibVLC _libVlc;
    private readonly ILogger<LibVlcPlaybackService> _logger;
    private Media? _currentMedia;
    private bool _disposed;
    private bool _suppressStoppedEvent;
    private TimeSpan? _pendingStartPosition;
    private TimeSpan _lastPosition;
    private TimeSpan? _lastDuration;
    private int _volume = 80;
    private int _volumeBeforeMute = 80;

    public LibVlcPlaybackService(ILogger<LibVlcPlaybackService> logger)
    {
        _logger = logger;
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC("--no-video-title-show", "--quiet");
        MediaPlayer = new MediaPlayer(_libVlc);
        MediaPlayer.Buffering += OnBuffering;
        MediaPlayer.Playing += OnPlaying;
        MediaPlayer.Paused += OnPaused;
        MediaPlayer.Stopped += OnStopped;
        MediaPlayer.EndReached += OnEndReached;
        MediaPlayer.EncounteredError += OnEncounteredError;
        MediaPlayer.Volume = _volume;
        StatusMessage = "Player idle";
    }

    public event EventHandler? PlaybackChanged;

    public MediaPlayer MediaPlayer { get; }

    public object NativeMediaPlayer => MediaPlayer;

    public MediaPlaybackState State { get; private set; }

    public PlaybackRequest? CurrentItem { get; private set; }

    public string StatusMessage { get; private set; }

    public int Volume => _volume;

    public bool IsMuted => MediaPlayer.Mute;

    public string AspectRatio { get; private set; } = "Fit";

    public TimeSpan Position => State is MediaPlaybackState.Stopped or MediaPlaybackState.Ended or MediaPlaybackState.Error
        ? _lastPosition
        : TimeSpan.FromMilliseconds(Math.Max(0L, MediaPlayer.Time));

    public TimeSpan? Duration => State is MediaPlaybackState.Stopped or MediaPlaybackState.Ended or MediaPlaybackState.Error
        ? _lastDuration
        : MediaPlayer.Length > 0 ? TimeSpan.FromMilliseconds(MediaPlayer.Length) : null;

    public IReadOnlyList<PlaybackTrack> AudioTracks { get; private set; } = [];

    public IReadOnlyList<PlaybackTrack> SubtitleTracks { get; private set; } = [];

    public async Task PlayAsync(
        PlaybackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        if (!Uri.TryCreate(request.StreamUrl, UriKind.Absolute, out Uri? streamUri) ||
            streamUri.Scheme is not ("http" or "https"))
        {
            SetState(MediaPlaybackState.Error, "Unsupported stream address.");
            return;
        }

        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _suppressStoppedEvent = true;
            MediaPlayer.Stop();
            _currentMedia?.Dispose();
            _currentMedia = new Media(_libVlc, streamUri);
            _currentMedia.AddOption(":network-caching=1500");
            CurrentItem = request;
            _pendingStartPosition = request.StartPosition;
            _lastPosition = TimeSpan.Zero;
            _lastDuration = null;
            AudioTracks = [];
            SubtitleTracks = [];
            SetState(MediaPlaybackState.Opening, $"Opening {request.Title}…");
            LogPlaybackStarted(request.ContentId, request.ContentKind);

            MediaPlayer.Media = _currentMedia;
            if (!MediaPlayer.Play())
            {
                SetState(MediaPlaybackState.Error, "Stream unavailable or unsupported.");
                LogPlaybackFailed(request.ContentId, request.ContentKind);
            }
        }
        finally
        {
            _suppressStoppedEvent = false;
            _operationGate.Release();
        }
    }

    public void Pause()
    {
        ThrowIfDisposed();
        if (State == MediaPlaybackState.Playing)
        {
            MediaPlayer.Pause();
        }
        else if (State == MediaPlaybackState.Paused)
        {
            MediaPlayer.Play();
        }
    }

    public void StopPlayback()
    {
        ThrowIfDisposed();
        CapturePlaybackProgress();
        MediaPlayer.Stop();
        SetState(MediaPlaybackState.Stopped, "Playback stopped");
    }

    public void SetVolume(int volume)
    {
        ThrowIfDisposed();
        _volume = Math.Clamp(volume, 0, 100);
        MediaPlayer.Volume = _volume;
        if (_volume > 0)
        {
            _volumeBeforeMute = _volume;
            MediaPlayer.Mute = false;
        }
        else
        {
            MediaPlayer.Mute = true;
        }
        NotifyChanged();
    }

    public void ToggleMute()
    {
        ThrowIfDisposed();
        if (MediaPlayer.Mute)
        {
            if (_volume == 0)
            {
                _volume = Math.Max(1, _volumeBeforeMute);
                MediaPlayer.Volume = _volume;
            }

            MediaPlayer.Mute = false;
        }
        else
        {
            _volumeBeforeMute = Math.Max(1, _volume);
            _volume = 0;
            MediaPlayer.Volume = 0;
            MediaPlayer.Mute = true;
        }
        NotifyChanged();
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        PlaybackRequest? request = CurrentItem;
        if (request is null)
        {
            SetState(MediaPlaybackState.Error, "Choose a channel or video before reconnecting.");
            return;
        }

        await PlayAsync(request, cancellationToken);
    }

    public void SetAspectRatio(string aspectRatio)
    {
        ThrowIfDisposed();
        string selected = SupportedAspectRatios.Contains(aspectRatio) ? aspectRatio : "Fit";
        AspectRatio = selected;
        MediaPlayer.AspectRatio = selected == "Fit" ? null : selected;
        NotifyChanged();
    }

    public void SelectAudioTrack(int trackId)
    {
        ThrowIfDisposed();
        if (AudioTracks.Any(track => track.Id == trackId))
        {
            MediaPlayer.SetAudioTrack(trackId);
            NotifyChanged();
        }
    }

    public void SelectSubtitleTrack(int trackId)
    {
        ThrowIfDisposed();
        if (SubtitleTracks.Any(track => track.Id == trackId))
        {
            MediaPlayer.SetSpu(trackId);
            NotifyChanged();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        MediaPlayer.Buffering -= OnBuffering;
        MediaPlayer.Playing -= OnPlaying;
        MediaPlayer.Paused -= OnPaused;
        MediaPlayer.Stopped -= OnStopped;
        MediaPlayer.EndReached -= OnEndReached;
        MediaPlayer.EncounteredError -= OnEncounteredError;
        MediaPlayer.Stop();
        _currentMedia?.Dispose();
        MediaPlayer.Dispose();
        _libVlc.Dispose();
        _operationGate.Dispose();
    }

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        if (e.Cache < 100)
        {
            SetState(MediaPlaybackState.Buffering, $"Buffering {Math.Round(e.Cache):N0}%");
        }
    }

    private void OnPlaying(object? sender, EventArgs e)
    {
        if (_pendingStartPosition is { } startPosition && startPosition > TimeSpan.Zero)
        {
            long maximum = MediaPlayer.Length > 1000 ? MediaPlayer.Length - 1000 : long.MaxValue;
            MediaPlayer.Time = Math.Min((long)startPosition.TotalMilliseconds, maximum);
            _pendingStartPosition = null;
        }

        RefreshTracks();
        SetState(MediaPlaybackState.Playing, CurrentItem is null ? "Playing" : $"Playing {CurrentItem.Title}");
    }

    private void OnPaused(object? sender, EventArgs e) =>
        SetState(MediaPlaybackState.Paused, "Playback paused");

    private void OnStopped(object? sender, EventArgs e)
    {
        if (_suppressStoppedEvent)
        {
            return;
        }

        if (State is not MediaPlaybackState.Error and not MediaPlaybackState.Ended)
        {
            SetState(MediaPlaybackState.Stopped, "Playback stopped");
        }
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        CapturePlaybackProgress();
        SetState(MediaPlaybackState.Ended, "Stream ended or the server disconnected.");
    }

    private void OnEncounteredError(object? sender, EventArgs e)
    {
        CapturePlaybackProgress();
        SetState(MediaPlaybackState.Error, "Stream unavailable, timed out, or is unsupported.");
        if (CurrentItem is not null)
        {
            LogPlaybackFailed(CurrentItem.ContentId, CurrentItem.ContentKind);
        }
    }

    private void RefreshTracks()
    {
        AudioTracks = MediaPlayer.AudioTrackDescription?
            .Select(track => new PlaybackTrack(track.Id, track.Name ?? $"Audio {track.Id}"))
            .ToArray() ?? [];
        SubtitleTracks = MediaPlayer.SpuDescription?
            .Select(track => new PlaybackTrack(track.Id, track.Name ?? $"Subtitle {track.Id}"))
            .ToArray() ?? [];
    }

    private void CapturePlaybackProgress()
    {
        _lastPosition = TimeSpan.FromMilliseconds(Math.Max(0L, MediaPlayer.Time));
        _lastDuration = MediaPlayer.Length > 0 ? TimeSpan.FromMilliseconds(MediaPlayer.Length) : null;
    }

    private void SetState(MediaPlaybackState state, string statusMessage)
    {
        State = state;
        StatusMessage = statusMessage;
        NotifyChanged();
    }

    private void NotifyChanged() => PlaybackChanged?.Invoke(this, EventArgs.Empty);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    [LoggerMessage(
        EventId = 6001,
        Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Playback started for content {ContentId} of type {ContentKind}.")]
    private partial void LogPlaybackStarted(string contentId, ContentKind contentKind);

    [LoggerMessage(
        EventId = 6002,
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Playback failed for content {ContentId} of type {ContentKind}.")]
    private partial void LogPlaybackFailed(string contentId, ContentKind contentKind);
}
