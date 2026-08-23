using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class SeriesViewModel : SectionViewModel
{
    private readonly IMediaCatalog _mediaCatalog;
    private readonly IFavoriteRepository _favoriteRepository;

    [ObservableProperty] private IReadOnlyList<FavoriteContentViewModel> _items = [];
    [ObservableProperty] private FavoriteContentViewModel? _selectedItem;

    public SeriesViewModel(IMediaCatalog mediaCatalog, IFavoriteRepository favoriteRepository)
        : base("Series", "Browse shows, seasons, and episodes.", "No series yet",
            "Series will appear here when a supported profile is connected.", "\uE8D6")
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
        Items = _mediaCatalog.GetSeries().Select(item => new FavoriteContentViewModel(
            item.ProfileId, ContentKind.Series, item.Id, item.Name, "Series",
            favorites.Contains((item.ProfileId, ContentKind.Series, item.Id)))).ToArray();
        SelectedItem = Items.Count == 0 ? null : Items[0];
        OnPropertyChanged(nameof(HasItems));
        SetEmptyContent(Items.Count == 0 ? "No series yet" : $"{Items.Count:N0} series loaded",
            Items.Count == 0 ? "Connect an Xtream profile to load series." : "Select a series to manage its favorite status.");
    }
}
