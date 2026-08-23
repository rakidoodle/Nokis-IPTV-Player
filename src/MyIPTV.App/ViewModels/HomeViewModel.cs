using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IFavoriteRepository _favoriteRepository;

    [ObservableProperty]
    private int _favoriteCount;

    public HomeViewModel(
        INavigationService navigationService,
        IFavoriteRepository favoriteRepository)
    {
        _navigationService = navigationService;
        _favoriteRepository = favoriteRepository;
        favoriteRepository.FavoritesChanged += OnFavoritesChanged;
        _ = RefreshFavoritesAsync();
    }

    public string Heading { get; } = "Good to see you";

    public string Description { get; } =
        "Connect a legitimate IPTV source to start building your personal media library.";

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

    private async void OnFavoritesChanged(object? sender, EventArgs e) => await RefreshFavoritesAsync();

    private async Task RefreshFavoritesAsync() =>
        FavoriteCount = (await _favoriteRepository.GetAllAsync()).Count;
}
