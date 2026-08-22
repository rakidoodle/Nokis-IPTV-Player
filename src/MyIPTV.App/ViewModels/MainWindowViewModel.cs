using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.App.Services;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private object? _currentViewModel;

    [ObservableProperty]
    private NavigationItemViewModel? _selectedNavigationItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public MainWindowViewModel(
        INavigationService navigationService,
        IApplicationConfiguration configuration,
        ISettingsService settingsService,
        IThemeService themeService)
    {
        _navigationService = navigationService;
        _settingsService = settingsService;
        _themeService = themeService;
        ApplicationName = configuration.Application.Name;

        NavigationItems =
        [
            new("Home", "\uE80F", typeof(HomeViewModel)),
            new("Live TV", "\uE714", typeof(LiveTvViewModel)),
            new("Movies", "\uE8B2", typeof(MoviesViewModel)),
            new("Series", "\uE8D6", typeof(SeriesViewModel)),
            new("Favorites", "\uE734", typeof(FavoritesViewModel)),
            new("Guide", "\uE787", typeof(GuideViewModel)),
            new("Settings", "\uE713", typeof(SettingsViewModel)),
        ];

        navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
        themeService.ThemeChanged += OnThemeChanged;
        SelectedNavigationItem = NavigationItems[0];
    }

    public string ApplicationName { get; }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public string ThemeGlyph => _themeService.CurrentTheme == AppTheme.Dark ? "\uE706" : "\uE708";

    public string ThemeButtonLabel =>
        _themeService.CurrentTheme == AppTheme.Dark ? "Switch to light theme" : "Switch to dark theme";

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
}
