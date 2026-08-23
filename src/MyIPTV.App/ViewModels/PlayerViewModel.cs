using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public partial class PlayerViewModel : ObservableObject
{
    private readonly IPlaybackService _playbackService;
    private readonly SynchronizationContext? _synchronizationContext;
    private bool _suppressVolumeUpdate;

    [ObservableProperty]
    private int _volume;

    [ObservableProperty]
    private string _selectedAspectRatio = "Fit";

    [ObservableProperty]
    private PlaybackTrack? _selectedAudioTrack;

    [ObservableProperty]
    private PlaybackTrack? _selectedSubtitleTrack;

    [ObservableProperty]
    private bool _isFullScreen;

    public PlayerViewModel(
        IPlaybackService playbackService,
        IPlaybackVideoSource videoSource)
    {
        _playbackService = playbackService;
        _synchronizationContext = SynchronizationContext.Current;
        NativeMediaPlayer = videoSource.NativeMediaPlayer;
        _volume = playbackService.Volume;
        AspectRatios = ["Fit", "16:9", "4:3", "21:9", "1:1"];
        playbackService.PlaybackChanged += OnPlaybackChanged;
        RefreshFromService();
    }

    public event EventHandler? FullScreenChanged;

    public object NativeMediaPlayer { get; }

    public IReadOnlyList<string> AspectRatios { get; }

    public ObservableCollection<PlaybackTrack> AudioTracks { get; } = [];

    public ObservableCollection<PlaybackTrack> SubtitleTracks { get; } = [];

    public PlaybackRequest? CurrentItem => _playbackService.CurrentItem;

    public string Title => CurrentItem?.Title ?? "Nothing playing";

    public string? LogoUrl => CurrentItem?.LogoUrl;

    public string CurrentProgram => CurrentItem?.CurrentProgram ?? "Program information unavailable";

    public string NextProgram => CurrentItem?.NextProgram ?? "Next program unavailable";

    public string StatusMessage => _playbackService.StatusMessage;

    public bool IsMuted => _playbackService.IsMuted;

    public bool HasCurrentItem => CurrentItem is not null;

    public bool IsPlaying => _playbackService.State == MediaPlaybackState.Playing;

    public bool IsLoading =>
        _playbackService.State is MediaPlaybackState.Opening or MediaPlaybackState.Buffering;

    public bool ShowPlayerOverlay =>
        IsLoading || !HasCurrentItem ||
        _playbackService.State is MediaPlaybackState.Error or MediaPlaybackState.Stopped or MediaPlaybackState.Ended;

    public string PlayPauseLabel => IsPlaying ? "Pause" : "Resume";

    public string PlayPauseGlyph => IsPlaying ? "\uE769" : "\uE768";

    public string MuteLabel => IsMuted ? "Unmute" : "Mute";

    public string MuteGlyph => IsMuted ? "\uE74F" : "\uE767";

    [RelayCommand(CanExecute = nameof(HasCurrentItem))]
    private void PlayPause() => _playbackService.Pause();

    [RelayCommand(CanExecute = nameof(HasCurrentItem))]
    private void Stop() => _playbackService.StopPlayback();

    [RelayCommand]
    private void ToggleMute() => _playbackService.ToggleMute();

    [RelayCommand(CanExecute = nameof(HasCurrentItem), IncludeCancelCommand = true)]
    private Task ReconnectAsync(CancellationToken cancellationToken) =>
        _playbackService.ReconnectAsync(cancellationToken);

    [RelayCommand]
    private void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
        FullScreenChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnVolumeChanged(int value)
    {
        if (_suppressVolumeUpdate)
        {
            return;
        }

        int clamped = Math.Clamp(value, 0, 100);
        if (clamped != value)
        {
            Volume = clamped;
            return;
        }

        _playbackService.SetVolume(clamped);
    }

    partial void OnSelectedAspectRatioChanged(string value) =>
        _playbackService.SetAspectRatio(value);

    partial void OnSelectedAudioTrackChanged(PlaybackTrack? value)
    {
        if (value is not null)
        {
            _playbackService.SelectAudioTrack(value.Id);
        }
    }

    partial void OnSelectedSubtitleTrackChanged(PlaybackTrack? value)
    {
        if (value is not null)
        {
            _playbackService.SelectSubtitleTrack(value.Id);
        }
    }

    private void OnPlaybackChanged(object? sender, EventArgs e)
    {
        if (_synchronizationContext is not null && SynchronizationContext.Current != _synchronizationContext)
        {
            _synchronizationContext.Post(_ => RefreshFromService(), null);
            return;
        }

        RefreshFromService();
    }

    private void RefreshFromService()
    {
        if (Volume != _playbackService.Volume)
        {
            _suppressVolumeUpdate = true;
            Volume = _playbackService.Volume;
            _suppressVolumeUpdate = false;
        }

        SelectedAspectRatio = _playbackService.AspectRatio;
        ReplaceTracks(AudioTracks, _playbackService.AudioTracks);
        ReplaceTracks(SubtitleTracks, _playbackService.SubtitleTracks);
        OnPropertyChanged(nameof(CurrentItem));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(LogoUrl));
        OnPropertyChanged(nameof(CurrentProgram));
        OnPropertyChanged(nameof(NextProgram));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(HasCurrentItem));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(ShowPlayerOverlay));
        OnPropertyChanged(nameof(PlayPauseLabel));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(MuteLabel));
        OnPropertyChanged(nameof(MuteGlyph));
        PlayPauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ReconnectCommand.NotifyCanExecuteChanged();
    }

    private static void ReplaceTracks(
        ObservableCollection<PlaybackTrack> target,
        IReadOnlyList<PlaybackTrack> source)
    {
        if (target.SequenceEqual(source))
        {
            return;
        }

        target.Clear();
        foreach (PlaybackTrack track in source)
        {
            target.Add(track);
        }
    }
}
