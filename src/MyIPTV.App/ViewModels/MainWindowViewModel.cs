using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyIPTV.App.Services;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IPlaybackService _playbackService;
    private readonly ISearchService _searchService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly SynchronizationContext? _synchronizationContext;
    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty]
    private object? _currentViewModel;

    [ObservableProperty]
    private NavigationItemViewModel? _selectedNavigationItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<SearchResult> _searchResults = [];

    [ObservableProperty]
    private bool _isSearchBusy;

    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _playerStatusMessage;

    public MainWindowViewModel(
        INavigationService navigationService,
        IApplicationConfiguration configuration,
        ISettingsService settingsService,
        IThemeService themeService,
        IPlaybackService playbackService,
        ISearchService searchService,
        ILogger<MainWindowViewModel> logger)
    {
        _navigationService = navigationService;
        _settingsService = settingsService;
        _themeService = themeService;
        _playbackService = playbackService;
        _searchService = searchService;
        _logger = logger;
        _synchronizationContext = SynchronizationContext.Current;
        _playerStatusMessage = playbackService.StatusMessage;
        ApplicationName = configuration.Application.Name;

        NavigationItems =
        [
            new("Home", "\uE80F", typeof(HomeViewModel)),
            new("Profiles", "\uE77B", typeof(ProfilesViewModel)),
            new("Live TV", "\uE714", typeof(LiveTvViewModel)),
            new("Movies", "\uE8B2", typeof(MoviesViewModel)),
            new("Series", "\uE8D6", typeof(SeriesViewModel)),
            new("Favorites", "\uE734", typeof(FavoritesViewModel)),
            new("Guide", "\uE787", typeof(GuideViewModel)),
            new("Settings", "\uE713", typeof(SettingsViewModel)),
        ];

        navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
        themeService.ThemeChanged += OnThemeChanged;
        playbackService.PlaybackChanged += OnPlaybackChanged;
        SelectedNavigationItem = NavigationItems[0];
    }

    public string ApplicationName { get; }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public string ThemeGlyph => _themeService.CurrentTheme == AppTheme.Dark ? "\uE706" : "\uE708";

    public string ThemeButtonLabel =>
        _themeService.CurrentTheme == AppTheme.Dark ? "Switch to light theme" : "Switch to dark theme";

    public bool HasSearchResults => SearchResults.Count > 0;

    partial void OnSearchTextChanged(string value)
    {
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _searchCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();

        if (value.Trim().Length < 2)
        {
            SearchResults = [];
            IsSearchBusy = false;
            IsSearchOpen = false;
            OnPropertyChanged(nameof(HasSearchResults));
            return;
        }

        IsSearchBusy = true;
        IsSearchOpen = true;
        _ = RunSearchAsync(value, cancellation.Token);
    }

    [RelayCommand]
    private void OpenSearchResult(SearchResult? result)
    {
        if (result is null)
        {
            return;
        }

        switch (result.Kind)
        {
            case SearchResultKind.LiveChannel:
                _navigationService.NavigateTo<LiveTvViewModel>();
                if (_navigationService.CurrentViewModel is LiveTvViewModel liveTv)
                {
                    liveTv.SelectSearchResult(result.ProfileId, result.Id);
                }
                break;
            case SearchResultKind.Movie:
                _navigationService.NavigateTo<MoviesViewModel>();
                break;
            case SearchResultKind.Series:
            case SearchResultKind.Episode:
                _navigationService.NavigateTo<SeriesViewModel>();
                break;
            case SearchResultKind.Category:
                NavigateToCategory(result.CategoryKind);
                break;
        }

        StatusMessage = $"Opened {result.Title}";
        SearchText = string.Empty;
        IsSearchOpen = false;
    }

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        if (value is null || _navigationService.CurrentViewModel?.GetType() == value.ViewModelType)
        {
            return;
        }

        _navigationService.NavigateTo(value.ViewModelType);
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        AppTheme newTheme = _themeService.CurrentTheme == AppTheme.Dark
            ? AppTheme.Light
            : AppTheme.Dark;
        _themeService.ApplyTheme(newTheme);

        AppSettings settings = await _settingsService.LoadAsync();
        await _settingsService.SaveAsync(settings with { Theme = newTheme.ToString() });
        StatusMessage = $"{newTheme} theme applied";
    }

    private void OnCurrentViewModelChanged(object? sender, EventArgs e)
    {
        CurrentViewModel = _navigationService.CurrentViewModel;
        Type? currentType = CurrentViewModel?.GetType();
        NavigationItemViewModel? matchingItem = NavigationItems.FirstOrDefault(
            item => item.ViewModelType == currentType);

        if (matchingItem is not null && SelectedNavigationItem != matchingItem)
        {
            SelectedNavigationItem = matchingItem;
        }

        StatusMessage = matchingItem is null ? "Ready" : $"Viewing {matchingItem.Label}";
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ThemeGlyph));
        OnPropertyChanged(nameof(ThemeButtonLabel));
    }

    private void OnPlaybackChanged(object? sender, EventArgs e)
    {
        if (_synchronizationContext is not null && SynchronizationContext.Current != _synchronizationContext)
        {
            _synchronizationContext.Post(_ => PlayerStatusMessage = _playbackService.StatusMessage, null);
            return;
        }

        PlayerStatusMessage = _playbackService.StatusMessage;
    }

    private async Task RunSearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            IReadOnlyList<SearchResult> results = await _searchService.SearchAsync(query, 50, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SearchResults = results;
            IsSearchOpen = true;
            OnPropertyChanged(nameof(HasSearchResults));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SearchResults = [];
            StatusMessage = "Search is temporarily unavailable.";
            LogSearchFailed(exception);
            OnPropertyChanged(nameof(HasSearchResults));
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsSearchBusy = false;
            }
        }
    }

    private void NavigateToCategory(ContentKind? kind)
    {
        switch (kind)
        {
            case ContentKind.LiveTv:
                _navigationService.NavigateTo<LiveTvViewModel>();
                break;
            case ContentKind.Movie:
                _navigationService.NavigateTo<MoviesViewModel>();
                break;
            case ContentKind.Series:
                _navigationService.NavigateTo<SeriesViewModel>();
                break;
        }
    }

    [LoggerMessage(EventId = 7001, Level = LogLevel.Error, Message = "Catalog search failed.")]
    private partial void LogSearchFailed(Exception exception);
}
