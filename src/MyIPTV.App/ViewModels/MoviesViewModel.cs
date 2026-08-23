using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class MoviesViewModel : SectionViewModel
{
    private readonly IMediaCatalog _mediaCatalog;
    private readonly IFavoriteRepository _favoriteRepository;

    [ObservableProperty] private IReadOnlyList<FavoriteContentViewModel> _items = [];
    [ObservableProperty] private FavoriteContentViewModel? _selectedItem;

    public MoviesViewModel(IMediaCatalog mediaCatalog, IFavoriteRepository favoriteRepository)
        : base("Movies", "Browse movies supplied by your connected provider.", "No movies yet",
            "Movies will appear here when a supported profile is connected.", "\uE8B2")
    {
        _mediaCatalog = mediaCatalog;
        _favoriteRepository = favoriteRepository;
        mediaCatalog.CatalogChanged += OnSourceChanged;
        favoriteRepository.FavoritesChanged += OnSourceChanged;
        _ = RefreshAsync();
    }

    public bool HasItems => Items.Count > 0;
    public bool CanToggleFavorite => SelectedItem is not null;
    public string FavoriteButtonText => SelectedItem?.IsFavorite == true ? "Remove favorite" : "Add favorite";

    partial void OnSelectedItemChanged(FavoriteContentViewModel? value)
    {
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(FavoriteButtonText));
    }

    [RelayCommand(CanExecute = nameof(CanToggleFavorite))]
    private async Task ToggleFavoriteAsync(CancellationToken cancellationToken)
    {
        if (SelectedItem is null) return;
        bool newValue = !SelectedItem.IsFavorite;
        await _favoriteRepository.SetAsync(SelectedItem.ToFavorite(), newValue, cancellationToken);
    }

    private async void OnSourceChanged(object? sender, EventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        HashSet<(Guid, ContentKind, string)> favorites = (await _favoriteRepository.GetAllAsync())
            .Select(item => (item.ProfileId, item.ContentKind, item.ContentId)).ToHashSet();
        Items = _mediaCatalog.GetMovies().Select(item => new FavoriteContentViewModel(
            item.ProfileId, ContentKind.Movie, item.Id, item.Name, "Movie",
            favorites.Contains((item.ProfileId, ContentKind.Movie, item.Id)))).ToArray();
        SelectedItem = Items.Count == 0 ? null : Items[0];
        OnPropertyChanged(nameof(HasItems));
        SetEmptyContent(Items.Count == 0 ? "No movies yet" : $"{Items.Count:N0} movies loaded",
            Items.Count == 0 ? "Connect an Xtream profile to load movies." : "Select a movie to manage its favorite status.");
    }
}
