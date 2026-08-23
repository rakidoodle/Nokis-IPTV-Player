using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class SeriesViewModel : SectionViewModel
{
    private readonly IMediaCatalog _mediaCatalog;
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IWatchHistoryRepository _historyRepository;
    private readonly IProfileRepository _profileRepository;
    private readonly IReadOnlyList<IContentProvider> _contentProviders;
    private readonly IPlaybackService _playbackService;
    private readonly SynchronizationContext? _synchronizationContext;
    private CancellationTokenSource? _detailsCancellation;
    private IReadOnlyList<EpisodeCardViewModel> _allEpisodes = [];
    private bool _suppressCatalogRefresh;

    [ObservableProperty] private IReadOnlyList<SeriesCardViewModel> _series = [];
    [ObservableProperty] private SeriesCardViewModel? _selectedSeries;
    [ObservableProperty] private IReadOnlyList<SeasonViewModel> _seasons = [];
    [ObservableProperty] private SeasonViewModel? _selectedSeason;
    [ObservableProperty] private IReadOnlyList<EpisodeCardViewModel> _episodes = [];
    [ObservableProperty] private EpisodeCardViewModel? _selectedEpisode;
    [ObservableProperty] private bool _isLoadingDetails;
    [ObservableProperty] private string _detailsStatus = "Select a series to load its seasons.";

    public SeriesViewModel(
        IMediaCatalog mediaCatalog,
        IFavoriteRepository favoriteRepository,
        IWatchHistoryRepository historyRepository,
        IProfileRepository profileRepository,
        IEnumerable<IContentProvider> contentProviders,
        IPlaybackService playbackService,
        PlayerViewModel player)
        : base("Series", "Browse shows, seasons, and episodes.", "No series yet",
            "Series will appear here when a supported profile is connected.", "\uE8D6")
    {
        _mediaCatalog = mediaCatalog;
        _favoriteRepository = favoriteRepository;
        _historyRepository = historyRepository;
        _profileRepository = profileRepository;
        _contentProviders = contentProviders.ToArray();
        _playbackService = playbackService;
        _synchronizationContext = SynchronizationContext.Current;
        Player = player;
        mediaCatalog.CatalogChanged += OnCatalogChanged;
        favoriteRepository.FavoritesChanged += OnFavoritesChanged;
        historyRepository.HistoryChanged += OnHistoryChanged;
        _ = RefreshSeriesAsync();
    }

    public PlayerViewModel Player { get; }
    public bool HasSeries => Series.Count > 0;
    public bool CanFavoriteSeries => SelectedSeries is not null;
    public bool CanPlayEpisode => SelectedEpisode is not null;
    public bool CanContinueEpisode => SelectedEpisode?.CanContinue == true;
    public string FavoriteButtonText => SelectedSeries?.IsFavorite == true ? "Remove favorite" : "Add favorite";

    partial void OnSelectedSeriesChanged(SeriesCardViewModel? value)
    {
        OnPropertyChanged(nameof(CanFavoriteSeries));
        OnPropertyChanged(nameof(FavoriteButtonText));
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        _ = LoadSelectedSeriesAsync(value);
    }

    partial void OnSelectedSeasonChanged(SeasonViewModel? value)
    {
        Episodes = value is null ? [] : _allEpisodes
            .Where(episode => episode.Episode.SeasonNumber == value.Number)
            .OrderBy(episode => episode.Episode.EpisodeNumber)
            .ToArray();
        SelectedEpisode = Episodes.Count == 0 ? null : Episodes[0];
    }

    partial void OnSelectedEpisodeChanged(EpisodeCardViewModel? value)
    {
        OnPropertyChanged(nameof(CanPlayEpisode));
        OnPropertyChanged(nameof(CanContinueEpisode));
        PlayEpisodeCommand.NotifyCanExecuteChanged();
        ContinueEpisodeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanFavoriteSeries))]
    private async Task ToggleFavoriteAsync(CancellationToken cancellationToken)
    {
        if (SelectedSeries is null) return;
        await _favoriteRepository.SetAsync(
            SelectedSeries.ToFavorite(), !SelectedSeries.IsFavorite, cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanPlayEpisode), IncludeCancelCommand = true)]
    private Task PlayEpisodeAsync(CancellationToken cancellationToken) =>
        PlaySelectedEpisodeAsync(continueWatching: false, cancellationToken);

    [RelayCommand(CanExecute = nameof(CanContinueEpisode), IncludeCancelCommand = true)]
    private Task ContinueEpisodeAsync(CancellationToken cancellationToken) =>
        PlaySelectedEpisodeAsync(continueWatching: true, cancellationToken);

    private async Task PlaySelectedEpisodeAsync(bool continueWatching, CancellationToken cancellationToken)
    {
        if (SelectedEpisode is null) return;
        EpisodeItem episode = SelectedEpisode.Episode;
        await _playbackService.PlayAsync(new(
            episode.Id,
            ContentKind.Series,
            episode.Name,
            episode.StreamUrl,
            StartPosition: continueWatching ? SelectedEpisode.History?.Position : null,
            ProfileId: episode.ProfileId), cancellationToken);
    }

    private async void OnCatalogChanged(object? sender, EventArgs e)
    {
        if (!_suppressCatalogRefresh) await DispatchAsync(RefreshSeriesAsync);
    }

    private async void OnFavoritesChanged(object? sender, EventArgs e) =>
        await DispatchAsync(RefreshSeriesAsync);

    private async void OnHistoryChanged(object? sender, EventArgs e) =>
        await DispatchAsync(RefreshEpisodeProgressAsync);

    private Task DispatchAsync(Func<Task> action)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            return action();
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _synchronizationContext.Post(async _ =>
        {
            try
            {
                await action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, null);
        return completion.Task;
    }

    private async Task RefreshSeriesAsync()
    {
        (Guid ProfileId, string Id)? previous = SelectedSeries is null
            ? null
            : (SelectedSeries.ProfileId, SelectedSeries.Id);
        HashSet<(Guid, string)> favorites = (await _favoriteRepository.GetAllAsync())
            .Where(item => item.ContentKind == ContentKind.Series)
            .Select(item => (item.ProfileId, item.ContentId)).ToHashSet();
        Series = _mediaCatalog.GetSeries()
            .Select(item => new SeriesCardViewModel(item, favorites.Contains((item.ProfileId, item.Id))))
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SelectedSeries = previous.HasValue
            ? Series.FirstOrDefault(item => item.ProfileId == previous.Value.ProfileId && item.Id == previous.Value.Id)
                ?? (Series.Count == 0 ? null : Series[0])
            : Series.Count == 0 ? null : Series[0];
        OnPropertyChanged(nameof(HasSeries));
        SetEmptyContent(Series.Count == 0 ? "No series yet" : $"{Series.Count:N0} series loaded",
            Series.Count == 0 ? "Connect an Xtream profile to load series." : "Choose a series to load seasons and episodes.");
    }

    private async Task LoadSelectedSeriesAsync(SeriesCardViewModel? selected)
    {
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _detailsCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();
        Seasons = [];
        Episodes = [];
        _allEpisodes = [];
        if (selected is null)
        {
            IsLoadingDetails = false;
            DetailsStatus = "Select a series to load its seasons.";
            return;
        }

        IsLoadingDetails = true;
        try
        {
            IReadOnlyList<EpisodeItem> loaded = _mediaCatalog.GetEpisodes()
                .Where(episode => episode.ProfileId == selected.ProfileId && episode.SeriesId == selected.Id)
                .ToArray();
            if (loaded.Count == 0)
            {
                IptvProfile? profile = await _profileRepository.GetByIdAsync(selected.ProfileId, cancellation.Token);
                IContentProvider? provider = profile is null ? null :
                    _contentProviders.FirstOrDefault(item => item.ConnectionType == profile.ConnectionType);
                if (profile is null || provider is null)
                {
                    DetailsStatus = "Reconnect this series profile to load episodes.";
                    return;
                }

                _suppressCatalogRefresh = true;
                try
                {
                    SeriesDetailsResult result = await provider.LoadSeriesDetailsAsync(
                        profile, selected.Id, cancellation.Token);
                    if (!result.IsSuccess)
                    {
                        DetailsStatus = result.Message;
                        return;
                    }
                    loaded = result.Episodes;
                }
                finally
                {
                    _suppressCatalogRefresh = false;
                }
            }

            await BuildEpisodeHierarchyAsync(selected, loaded, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            if (!cancellation.IsCancellationRequested) IsLoadingDetails = false;
        }
    }

    private async Task BuildEpisodeHierarchyAsync(
        SeriesCardViewModel selected,
        IReadOnlyList<EpisodeItem> episodes,
        CancellationToken cancellationToken)
    {
        int? previousSeason = SelectedSeason?.Number;
        string? previousEpisode = SelectedEpisode?.Episode.Id;
        Dictionary<(Guid, string), WatchHistoryItem> history = (await _historyRepository.GetRecentAsync(500, cancellationToken))
            .Where(item => item.ContentKind == ContentKind.Series)
            .ToDictionary(item => (item.ProfileId, item.ContentId));
        _allEpisodes = episodes.Select(episode => new EpisodeCardViewModel(
            episode, history.GetValueOrDefault((episode.ProfileId, episode.Id)))).ToArray();
        Seasons = _allEpisodes.GroupBy(episode => episode.Episode.SeasonNumber)
            .OrderBy(group => group.Key)
            .Select(group => new SeasonViewModel(group.Key, group.Count()))
            .ToArray();
        SelectedSeason = previousSeason.HasValue
            ? Seasons.FirstOrDefault(season => season.Number == previousSeason.Value)
                ?? (Seasons.Count == 0 ? null : Seasons[0])
            : Seasons.Count == 0 ? null : Seasons[0];
        if (previousEpisode is not null)
        {
            SelectedEpisode = Episodes.FirstOrDefault(episode => episode.Episode.Id == previousEpisode)
                ?? SelectedEpisode;
        }
        DetailsStatus = Seasons.Count == 0
            ? "This provider returned no episodes for the selected series."
            : $"Loaded {episodes.Count:N0} episodes for {selected.Title}.";
    }

    private Task RefreshEpisodeProgressAsync() => SelectedSeries is null
        ? Task.CompletedTask
        : BuildEpisodeHierarchyAsync(
            SelectedSeries,
            _mediaCatalog.GetEpisodes().Where(episode =>
                episode.ProfileId == SelectedSeries.ProfileId && episode.SeriesId == SelectedSeries.Id).ToArray(),
            CancellationToken.None);
}
