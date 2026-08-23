using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class MoviesViewModel : SectionViewModel
{
    private readonly IMediaCatalog _mediaCatalog;
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IWatchHistoryRepository _historyRepository;
    private readonly IPlaybackService _playbackService;
    private readonly SynchronizationContext? _synchronizationContext;
    private IReadOnlyList<MovieCardViewModel> _allMovies = [];

    [ObservableProperty] private IReadOnlyList<MovieCategoryViewModel> _categories = [];
    [ObservableProperty] private MovieCategoryViewModel? _selectedCategory;
    [ObservableProperty] private IReadOnlyList<MovieCardViewModel> _filteredMovies = [];
    [ObservableProperty] private MovieCardViewModel? _selectedMovie;

    public MoviesViewModel(
        IMediaCatalog mediaCatalog,
        IFavoriteRepository favoriteRepository,
        IWatchHistoryRepository historyRepository,
        IPlaybackService playbackService,
        PlayerViewModel player)
        : base("Movies", "Browse and play movies supplied by your connected provider.", "No movies yet",
            "Movies will appear here when a supported profile is connected.", "\uE8B2")
    {
        _mediaCatalog = mediaCatalog;
        _favoriteRepository = favoriteRepository;
        _historyRepository = historyRepository;
        _playbackService = playbackService;
        _synchronizationContext = SynchronizationContext.Current;
        Player = player;
        mediaCatalog.CatalogChanged += OnSourceChanged;
        favoriteRepository.FavoritesChanged += OnSourceChanged;
        historyRepository.HistoryChanged += OnSourceChanged;
        _ = RefreshAsync();
    }

    public PlayerViewModel Player { get; }
    public bool HasMovies => Categories.Count > 0;
    public bool CanUseSelectedMovie => SelectedMovie is not null;
    public bool CanContinueSelected => SelectedMovie?.CanContinue == true;
    public string FavoriteButtonText => SelectedMovie?.IsFavorite == true ? "Remove favorite" : "Add favorite";

    partial void OnSelectedCategoryChanged(MovieCategoryViewModel? value) => ApplyCategory(value);

    partial void OnSelectedMovieChanged(MovieCardViewModel? value)
    {
        OnPropertyChanged(nameof(CanUseSelectedMovie));
        OnPropertyChanged(nameof(CanContinueSelected));
        OnPropertyChanged(nameof(FavoriteButtonText));
        PlayMovieCommand.NotifyCanExecuteChanged();
        ContinueMovieCommand.NotifyCanExecuteChanged();
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedMovie), IncludeCancelCommand = true)]
    private Task PlayMovieAsync(CancellationToken cancellationToken) =>
        PlaySelectedAsync(continueWatching: false, cancellationToken);

    [RelayCommand(CanExecute = nameof(CanContinueSelected), IncludeCancelCommand = true)]
    private Task ContinueMovieAsync(CancellationToken cancellationToken) =>
        PlaySelectedAsync(continueWatching: true, cancellationToken);

    [RelayCommand(CanExecute = nameof(CanUseSelectedMovie))]
    private async Task ToggleFavoriteAsync(CancellationToken cancellationToken)
    {
        if (SelectedMovie is null) return;
        await _favoriteRepository.SetAsync(
            SelectedMovie.ToFavorite(), !SelectedMovie.IsFavorite, cancellationToken);
    }

    private async Task PlaySelectedAsync(bool continueWatching, CancellationToken cancellationToken)
    {
        if (SelectedMovie is null) return;
        MovieItem movie = SelectedMovie.Movie;
        await _playbackService.PlayAsync(new(
            movie.Id,
            ContentKind.Movie,
            movie.Name,
            movie.StreamUrl,
            movie.PosterUrl,
            StartPosition: continueWatching ? SelectedMovie.History?.Position : null,
            ProfileId: movie.ProfileId), cancellationToken);
    }

    private async void OnSourceChanged(object? sender, EventArgs e)
    {
        if (_synchronizationContext is not null && SynchronizationContext.Current != _synchronizationContext)
        {
            _synchronizationContext.Post(async _ => await RefreshAsync(), null);
            return;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        (Guid ProfileId, string Id)? previousSelection = SelectedMovie is null
            ? null
            : (SelectedMovie.ProfileId, SelectedMovie.Id);
        string? previousCategory = SelectedCategory?.CategoryId;
        IReadOnlyList<MovieItem> movies = _mediaCatalog.GetMovies();
        IReadOnlyList<ContentCategory> mediaCategories = _mediaCatalog.GetCategories();
        Task<IReadOnlyList<FavoriteItem>> favoritesTask = _favoriteRepository.GetAllAsync();
        Task<IReadOnlyList<WatchHistoryItem>> historyTask = _historyRepository.GetRecentAsync(500);
        await Task.WhenAll(favoritesTask, historyTask);
        IReadOnlyList<FavoriteItem> favoriteItems = await favoritesTask;
        IReadOnlyList<WatchHistoryItem> historyItems = await historyTask;
        HashSet<(Guid, string)> favorites = favoriteItems
            .Where(item => item.ContentKind == ContentKind.Movie)
            .Select(item => (item.ProfileId, item.ContentId)).ToHashSet();
        Dictionary<(Guid, string), WatchHistoryItem> history = historyItems
            .Where(item => item.ContentKind == ContentKind.Movie)
            .ToDictionary(item => (item.ProfileId, item.ContentId));
        Dictionary<(Guid, string), string> categoryNames = mediaCategories
            .Where(category => category.Kind == ContentKind.Movie)
            .ToDictionary(category => (category.ProfileId, category.Id), category => category.Name);
        MovieCardViewModel[] cards = movies.Select(movie => new MovieCardViewModel(
            movie,
            categoryNames.GetValueOrDefault((movie.ProfileId, movie.CategoryId), "Uncategorized"),
            favorites.Contains((movie.ProfileId, movie.Id)),
            history.GetValueOrDefault((movie.ProfileId, movie.Id)))).ToArray();

        _allMovies = cards;
        Categories = BuildCategories(cards);
        SelectedCategory = Categories.FirstOrDefault(category =>
            string.Equals(category.CategoryId, previousCategory, StringComparison.OrdinalIgnoreCase)) ??
            (Categories.Count == 0 ? null : Categories[0]);
        ApplyCategory(SelectedCategory);
        if (previousSelection.HasValue)
        {
            SelectedMovie = FilteredMovies.FirstOrDefault(movie =>
                movie.ProfileId == previousSelection.Value.ProfileId && movie.Id == previousSelection.Value.Id) ??
                SelectedMovie;
        }
        OnPropertyChanged(nameof(HasMovies));
        SetEmptyContent(cards.Length == 0 ? "No movies yet" : $"{cards.Length:N0} movies loaded",
            cards.Length == 0 ? "Connect an Xtream profile to load movies." : "Choose a movie to view details or start playback.");
    }

    private static List<MovieCategoryViewModel> BuildCategories(MovieCardViewModel[] cards)
    {
        if (cards.Length == 0) return [];
        List<MovieCategoryViewModel> categories = [new("All movies", cards.Length, null)];
        categories.AddRange(cards.GroupBy(card => card.CategoryName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new MovieCategoryViewModel(group.Key, group.Count(), group.Key)));
        return categories;
    }

    private void ApplyCategory(MovieCategoryViewModel? category)
    {
        FilteredMovies = _allMovies.Where(card => category?.CategoryId is null ||
                card.CategoryName.Equals(category.CategoryId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        SelectedMovie = FilteredMovies.Count == 0 ? null : FilteredMovies[0];
    }
}
