using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class FavoritesViewModel : SectionViewModel
{
    private readonly IFavoriteRepository _favoriteRepository;

    [ObservableProperty]
    private IReadOnlyList<FavoriteContentViewModel> _items = [];

    [ObservableProperty]
    private FavoriteContentViewModel? _selectedItem;

    public FavoritesViewModel(IFavoriteRepository favoriteRepository)
        : base("Favorites", "Keep your preferred channels, movies, and series close by.",
            "No favorites yet", "Use the favorite button on content to add it here.", "\uE734")
    {
        _favoriteRepository = favoriteRepository;
        favoriteRepository.FavoritesChanged += OnFavoritesChanged;
        _ = RefreshAsync();
    }

    public bool HasItems => Items.Count > 0;
    public bool CanRemoveSelected => SelectedItem is not null;

    partial void OnSelectedItemChanged(FavoriteContentViewModel? value) =>
        RemoveSelectedCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private async Task RemoveSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedItem is not null)
        {
            await _favoriteRepository.SetAsync(SelectedItem.ToFavorite(), false, cancellationToken);
        }
    }

    private async void OnFavoritesChanged(object? sender, EventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        IReadOnlyList<FavoriteItem> favorites = await _favoriteRepository.GetAllAsync();
        Items = favorites.Select(item => new FavoriteContentViewModel(
            item.ProfileId, item.ContentKind, item.ContentId, item.Title,
            item.ContentKind switch
            {
                ContentKind.LiveTv => "Live channel",
                ContentKind.Movie => "Movie",
                ContentKind.Series => "Series",
                _ => "Content",
            }, true)).ToArray();
        SelectedItem = Items.Count == 0 ? null : Items[0];
        OnPropertyChanged(nameof(HasItems));
    }
}
