using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IWatchHistoryRepository _historyRepository;
    private readonly IChannelCatalog _channelCatalog;
    private readonly IMediaCatalog _mediaCatalog;
    private readonly IPlaybackService _playbackService;
    private readonly IProfileRepository? _profileRepository;
    private readonly SynchronizationContext? _synchronizationContext;

    [ObservableProperty]
    private int _favoriteCount;

    [ObservableProperty]
    private int _profileCount;

    [ObservableProperty]
    private IReadOnlyList<RecentlyWatchedItemViewModel> _recentItems = [];

    [ObservableProperty]
    private RecentlyWatchedItemViewModel? _selectedRecentItem;

    public HomeViewModel(
        INavigationService navigationService,
        IFavoriteRepository favoriteRepository,
        IWatchHistoryRepository historyRepository,
        IChannelCatalog channelCatalog,
        IMediaCatalog mediaCatalog,
        IPlaybackService playbackService,
        IProfileRepository? profileRepository = null)
    {
        _navigationService = navigationService;
        _favoriteRepository = favoriteRepository;
        _historyRepository = historyRepository;
        _channelCatalog = channelCatalog;
        _mediaCatalog = mediaCatalog;
        _playbackService = playbackService;
        _profileRepository = profileRepository;
        _synchronizationContext = SynchronizationContext.Current;
        favoriteRepository.FavoritesChanged += OnFavoritesChanged;
        historyRepository.HistoryChanged += OnHistoryChanged;
        channelCatalog.ChannelsChanged += OnCatalogChanged;
        mediaCatalog.CatalogChanged += OnCatalogChanged;
        if (profileRepository is not null)
        {
            profileRepository.ProfilesChanged += OnProfilesChanged;
            _ = RefreshProfilesAsync();
        }
        _ = RefreshFavoritesAsync();
        _ = RefreshHistoryAsync();
    }

    public string Heading { get; } = "Good to see you";

    public string Description { get; } =
        "Connect a legitimate IPTV source to start building your personal media library.";

    public int RecentCount => RecentItems.Count;

    public bool HasRecentItems => RecentItems.Count > 0;

    public string RecentSummary => HasRecentItems ? "Ready to continue" : "Nothing played yet";

    public string ProfileSummary => ProfileCount == 0 ? "No source connected" : "Configured sources";

    public bool CanPlayRecent => SelectedRecentItem is not null && FindPlaybackRequest(SelectedRecentItem.Item) is not null;

    public string RecentActionLabel => SelectedRecentItem?.ActionLabel ?? "Continue watching";

    [RelayCommand]
    private void BrowseLiveTv()
    {
        _navigationService.NavigateTo<LiveTvViewModel>();
    }

    [RelayCommand]
    private void ManageProfiles()
    {
        _navigationService.NavigateTo<ProfilesViewModel>();
    }

    partial void OnSelectedRecentItemChanged(RecentlyWatchedItemViewModel? value)
    {
        OnPropertyChanged(nameof(CanPlayRecent));
        OnPropertyChanged(nameof(RecentActionLabel));
        PlayRecentCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPlayRecent), IncludeCancelCommand = true)]
    private async Task PlayRecentAsync(CancellationToken cancellationToken)
    {
        if (SelectedRecentItem is null || FindPlaybackRequest(SelectedRecentItem.Item) is not { } request)
        {
            return;
        }

        _navigationService.NavigateTo<LiveTvViewModel>();
        await _playbackService.PlayAsync(request, cancellationToken);
    }

    private async void OnFavoritesChanged(object? sender, EventArgs e) => await RefreshFavoritesAsync();

    private async void OnProfilesChanged(object? sender, EventArgs e)
    {
        if (_synchronizationContext is not null && SynchronizationContext.Current != _synchronizationContext)
        {
            _synchronizationContext.Post(async _ => await RefreshProfilesAsync(), null);
            return;
        }

        await RefreshProfilesAsync();
    }

    private async Task RefreshProfilesAsync()
    {
        if (_profileRepository is null)
        {
            return;
        }

        ProfileCount = (await _profileRepository.GetAllAsync()).Count;
        OnPropertyChanged(nameof(ProfileSummary));
    }

    private async Task RefreshFavoritesAsync() =>
        FavoriteCount = (await _favoriteRepository.GetAllAsync()).Count;

    private async void OnHistoryChanged(object? sender, EventArgs e)
    {
        if (_synchronizationContext is not null && SynchronizationContext.Current != _synchronizationContext)
        {
            _synchronizationContext.Post(async _ => await RefreshHistoryAsync(), null);
            return;
        }

        await RefreshHistoryAsync();
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanPlayRecent));
        PlayRecentCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshHistoryAsync()
    {
        RecentItems = (await _historyRepository.GetRecentAsync(10))
            .Select(item => new RecentlyWatchedItemViewModel(item))
            .ToArray();
        SelectedRecentItem = RecentItems.Count == 0 ? null : RecentItems[0];
        OnPropertyChanged(nameof(RecentCount));
        OnPropertyChanged(nameof(HasRecentItems));
        OnPropertyChanged(nameof(RecentSummary));
        OnPropertyChanged(nameof(CanPlayRecent));
        PlayRecentCommand.NotifyCanExecuteChanged();
    }

    private PlaybackRequest? FindPlaybackRequest(WatchHistoryItem history)
    {
        if (history.ContentKind == ContentKind.LiveTv)
        {
            IptvChannel? channel = _channelCatalog.GetAll().FirstOrDefault(item =>
                item.ProfileId == history.ProfileId && item.Id == history.ContentId);
            return channel is null ? null : new(
                channel.Id, ContentKind.LiveTv, channel.Name, channel.StreamUrl, channel.LogoUrl,
                ProfileId: channel.ProfileId);
        }

        if (history.ContentKind == ContentKind.Movie)
        {
            MovieItem? movie = _mediaCatalog.GetMovies().FirstOrDefault(item =>
                item.ProfileId == history.ProfileId && item.Id == history.ContentId);
            return movie is null ? null : new(
                movie.Id, ContentKind.Movie, movie.Name, movie.StreamUrl, movie.PosterUrl,
                StartPosition: history.Position, ProfileId: movie.ProfileId);
        }

        EpisodeItem? episode = _mediaCatalog.GetEpisodes().FirstOrDefault(item =>
            item.ProfileId == history.ProfileId && item.Id == history.ContentId);
        return episode is null ? null : new(
            episode.Id, ContentKind.Series, episode.Name, episode.StreamUrl,
            StartPosition: history.Position, ProfileId: episode.ProfileId);
    }
}
