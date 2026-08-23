using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class LiveTvViewModel : SectionViewModel
{
    private readonly IChannelCatalog _channelCatalog;
    private readonly IPlaybackService _playbackService;
    private readonly IFavoriteRepository _favoriteRepository;

    [ObservableProperty]
    private IReadOnlyList<ChannelCategoryViewModel> _categories = [];

    [ObservableProperty]
    private ChannelCategoryViewModel? _selectedCategory;

    [ObservableProperty]
    private IReadOnlyList<IptvChannel> _filteredChannels = [];

    [ObservableProperty]
    private IptvChannel? _selectedChannel;

    [ObservableProperty]
    private bool _isSelectedFavorite;

    public LiveTvViewModel(
        IChannelCatalog channelCatalog,
        IPlaybackService playbackService,
        PlayerViewModel player,
        IFavoriteRepository favoriteRepository)
        : base(
        "Live TV",
        "Browse channels from your authorized IPTV profiles.",
        "No channels yet",
        "Connect an IPTV profile to import and browse live channels.",
        "\uE714")
    {
        _channelCatalog = channelCatalog;
        _playbackService = playbackService;
        _favoriteRepository = favoriteRepository;
        Player = player;
        channelCatalog.ChannelsChanged += OnChannelsChanged;
        favoriteRepository.FavoritesChanged += OnFavoritesChanged;
        UpdateCatalog();
    }

    public PlayerViewModel Player { get; }

    public bool HasChannels => Categories.Count > 0;

    public bool CanPlaySelectedChannel => SelectedChannel is not null;

    public bool CanFavoriteSelectedChannel => SelectedChannel is not null;

    public string FavoriteButtonText => IsSelectedFavorite ? "Remove favorite" : "Add favorite";

    public void SelectSearchResult(Guid profileId, string channelId)
    {
        IptvChannel? channel = _channelCatalog.GetAll().FirstOrDefault(item =>
            item.ProfileId == profileId && string.Equals(item.Id, channelId, StringComparison.Ordinal));
        if (channel is null)
        {
            return;
        }

        string group = NormalizeGroup(channel.Group);
        SelectedCategory = Categories.FirstOrDefault(category =>
            string.Equals(category.Group, group, StringComparison.OrdinalIgnoreCase)) ?? Categories[0];
        SelectedChannel = FilteredChannels.FirstOrDefault(item =>
            item.ProfileId == profileId && string.Equals(item.Id, channelId, StringComparison.Ordinal));
    }

    [RelayCommand(CanExecute = nameof(CanPlaySelectedChannel), IncludeCancelCommand = true)]
    private async Task PlaySelectedChannelAsync(CancellationToken cancellationToken)
    {
        if (SelectedChannel is null)
        {
            return;
        }

        IptvChannel channel = SelectedChannel;
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

    [RelayCommand(CanExecute = nameof(CanFavoriteSelectedChannel))]
    private async Task ToggleFavoriteAsync(CancellationToken cancellationToken)
    {
        if (SelectedChannel is null)
        {
            return;
        }

        IptvChannel channel = SelectedChannel;
        await _favoriteRepository.SetAsync(
            new FavoriteItem(channel.ProfileId, ContentKind.LiveTv, channel.Id, channel.Name, DateTimeOffset.UtcNow),
            !IsSelectedFavorite,
            cancellationToken);
    }

    partial void OnSelectedCategoryChanged(ChannelCategoryViewModel? value) => ApplyCategory(value);

    partial void OnSelectedChannelChanged(IptvChannel? value)
    {
        OnPropertyChanged(nameof(CanPlaySelectedChannel));
        OnPropertyChanged(nameof(CanFavoriteSelectedChannel));
        PlaySelectedChannelCommand.NotifyCanExecuteChanged();
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        _ = RefreshSelectedFavoriteAsync(value);
    }

    private void OnChannelsChanged(object? sender, EventArgs e) => UpdateCatalog();

    private async void OnFavoritesChanged(object? sender, EventArgs e) =>
        await RefreshSelectedFavoriteAsync(SelectedChannel);

    private async Task RefreshSelectedFavoriteAsync(IptvChannel? channel)
    {
        bool isFavorite = channel is not null && await _favoriteRepository.ContainsAsync(
            channel.ProfileId, ContentKind.LiveTv, channel.Id);
        if (ReferenceEquals(channel, SelectedChannel))
        {
            IsSelectedFavorite = isFavorite;
            OnPropertyChanged(nameof(FavoriteButtonText));
        }
    }

    private void UpdateCatalog()
    {
        IReadOnlyList<IptvChannel> allChannels = _channelCatalog.GetAll();
        int channelCount = allChannels.Count;
        if (channelCount == 0)
        {
            Categories = [];
            FilteredChannels = [];
            SelectedCategory = null;
            SelectedChannel = null;
            SetEmptyContent(
                "No channels yet",
                "Connect an IPTV profile to import channels.");
            OnPropertyChanged(nameof(HasChannels));
            return;
        }

        string? previousGroup = SelectedCategory?.Group;
        List<ChannelCategoryViewModel> categories =
        [
            new("All channels", channelCount, null),
        ];
        categories.AddRange(allChannels
            .GroupBy(channel => NormalizeGroup(channel.Group), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ChannelCategoryViewModel(group.Key, group.Count(), group.Key)));
        Categories = categories;
        SelectedCategory = categories.FirstOrDefault(category =>
            string.Equals(category.Group, previousGroup, StringComparison.OrdinalIgnoreCase)) ?? categories[0];

        string channelLabel = channelCount == 1 ? "channel" : "channels";
        SetEmptyContent(
            $"{channelCount:N0} {channelLabel} available",
            "Select a category and channel, then choose Play.");
        OnPropertyChanged(nameof(HasChannels));
    }

    private void ApplyCategory(ChannelCategoryViewModel? category)
    {
        IReadOnlyList<IptvChannel> allChannels = _channelCatalog.GetAll();
        FilteredChannels = category?.Group is null
            ? allChannels.ToArray()
            : allChannels
                .Where(channel => string.Equals(
                    NormalizeGroup(channel.Group),
                    category.Group,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        if (SelectedChannel is not null && !FilteredChannels.Contains(SelectedChannel))
        {
            SelectedChannel = null;
        }
    }

    private static string NormalizeGroup(string? group) =>
        string.IsNullOrWhiteSpace(group) ? "Uncategorized" : group.Trim();
}
