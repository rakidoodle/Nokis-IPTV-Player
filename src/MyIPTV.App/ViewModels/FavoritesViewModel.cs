using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class FavoritesViewModel : SectionViewModel
{
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly INavigationService _navigationService;
    private readonly IChannelCatalog _channelCatalog;
    private readonly IPlaybackService _playbackService;

    [ObservableProperty]
    private IReadOnlyList<FavoriteContentViewModel> _items = [];

    [ObservableProperty]
    private FavoriteContentViewModel? _selectedItem;

    public FavoritesViewModel(
        IFavoriteRepository favoriteRepository,
        INavigationService navigationService,
        IChannelCatalog channelCatalog,
        IPlaybackService playbackService)
        : base("Favorites", "Keep your preferred channels, movies, and series close by.",
            "No favorites yet", "Use the favorite button on content to add it here.", "\uE734")
    {
        _favoriteRepository = favoriteRepository;
        _navigationService = navigationService;
        _channelCatalog = channelCatalog;
        _playbackService = playbackService;
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

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task PlayFavoriteAsync(FavoriteContentViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null || item.ContentKind != ContentKind.LiveTv)
        {
            return;
        }

        IptvChannel? channel = _channelCatalog.GetAll().FirstOrDefault(candidate =>
            candidate.ProfileId == item.ProfileId &&
            string.Equals(candidate.Id, item.ContentId, StringComparison.Ordinal));
        if (channel is null)
        {
            return;
        }

        SelectedItem = item;
        _navigationService.NavigateTo<LiveTvViewModel>();
        if (_navigationService.CurrentViewModel is LiveTvViewModel liveTv)
        {
            liveTv.SelectSearchResult(channel.ProfileId, channel.Id);
        }

        await _playbackService.PlayAsync(
            new PlaybackRequest(
                channel.Id,
                ContentKind.LiveTv,
                channel.Name,
                channel.StreamUrl,
                channel.LogoUrl,
                ProfileId: channel.ProfileId),
            cancellationToken);
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
