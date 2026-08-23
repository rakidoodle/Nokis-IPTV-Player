using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class LiveTvViewModel : SectionViewModel
{
    private readonly IChannelCatalog _channelCatalog;
    private readonly IActiveProfileService _activeProfileService;
    private readonly IPlaybackService _playbackService;
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IEpgService _epgService;
    private IReadOnlyList<IptvChannel> _allChannels = [];
    private IReadOnlyDictionary<string, IptvChannel[]> _channelsByGroup =
        new Dictionary<string, IptvChannel[]>(StringComparer.OrdinalIgnoreCase);

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

    [ObservableProperty]
    private string _selectedCurrentProgram = "Program information unavailable";

    [ObservableProperty]
    private string _selectedNextProgram = "Next program unavailable";

    public LiveTvViewModel(
        IChannelCatalog channelCatalog,
        IActiveProfileService activeProfileService,
        IPlaybackService playbackService,
        PlayerViewModel player,
        IFavoriteRepository favoriteRepository,
        IEpgService epgService)
        : base(
        "Live TV",
        "Browse channels from your authorized IPTV profiles.",
        "No channels yet",
        "Connect an IPTV profile to import and browse live channels.",
        "\uE714")
    {
        _channelCatalog = channelCatalog;
        _activeProfileService = activeProfileService;
        _playbackService = playbackService;
        _favoriteRepository = favoriteRepository;
        _epgService = epgService;
        Player = player;
        channelCatalog.ChannelsChanged += OnChannelsChanged;
        activeProfileService.ActiveProfileChanged += OnActiveProfileChanged;
        favoriteRepository.FavoritesChanged += OnFavoritesChanged;
        epgService.EpgChanged += OnEpgChanged;
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

    public async Task<bool> SelectAndPlaySearchResultAsync(
        Guid profileId,
        string channelId,
        CancellationToken cancellationToken = default)
    {
        SelectSearchResult(profileId, channelId);
        if (SelectedChannel is null ||
            SelectedChannel.ProfileId != profileId ||
            !string.Equals(SelectedChannel.Id, channelId, StringComparison.Ordinal))
        {
            return false;
        }

        await PlaySelectedChannelAsync(cancellationToken);
        return true;
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
                SelectedCurrentProgram,
                SelectedNextProgram,
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
        _ = RefreshSelectedProgramsAsync(value);
    }

    private void OnChannelsChanged(object? sender, EventArgs e) => UpdateCatalog();

    private void OnActiveProfileChanged(object? sender, EventArgs e) => UpdateCatalog();

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

    private async void OnEpgChanged(object? sender, EventArgs e) =>
        await RefreshSelectedProgramsAsync(SelectedChannel);

    private async Task RefreshSelectedProgramsAsync(IptvChannel? channel)
    {
        if (channel is null)
        {
            SelectedCurrentProgram = "Program information unavailable";
            SelectedNextProgram = "Next program unavailable";
            return;
        }

        string channelId = string.IsNullOrWhiteSpace(channel.EpgId) ? channel.Name : channel.EpgId;
        EpgNowNext programs = await _epgService.GetNowNextAsync(channelId, DateTimeOffset.UtcNow);
        if (ReferenceEquals(channel, SelectedChannel))
        {
            SelectedCurrentProgram = programs.Current?.Title ?? "Program information unavailable";
            SelectedNextProgram = programs.Next?.Title ?? "Next program unavailable";
        }
    }

    private void UpdateCatalog()
    {
        IptvProfile? activeProfile = _activeProfileService.ActiveProfile;
        IReadOnlyList<IptvChannel> allChannels = activeProfile is null
            ? _channelCatalog.GetAll()
            : _channelCatalog.GetForProfile(activeProfile.Id);
        _allChannels = allChannels;
        int channelCount = allChannels.Count;
        if (channelCount == 0)
        {
            Categories = [];
            _channelsByGroup = new Dictionary<string, IptvChannel[]>(StringComparer.OrdinalIgnoreCase);
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
        _channelsByGroup = allChannels
            .GroupBy(channel => NormalizeGroup(channel.Group), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        List<ChannelCategoryViewModel> categories =
        [
            new("All channels", channelCount, null),
        ];
        categories.AddRange(_channelsByGroup
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ChannelCategoryViewModel(group.Key, group.Value.Length, group.Key)));
        Categories = categories;
        SelectedCategory = categories.FirstOrDefault(category =>
            string.Equals(category.Group, previousGroup, StringComparison.OrdinalIgnoreCase)) ?? categories[0];
        // Profiles commonly reuse category names. If the new category value compares
        // equal to the old one, the generated property hook does not run, so refresh
        // the channel list explicitly from the newly active profile's lookup.
        ApplyCategory(SelectedCategory);

        string channelLabel = channelCount == 1 ? "channel" : "channels";
        SetEmptyContent(
            $"{channelCount:N0} {channelLabel} available",
            "Select a category and channel, then choose Play.");
        OnPropertyChanged(nameof(HasChannels));
    }

    private void ApplyCategory(ChannelCategoryViewModel? category)
    {
        FilteredChannels = category?.Group is null
            ? _allChannels
            : _channelsByGroup.GetValueOrDefault(category.Group) ?? [];
        if (SelectedChannel is not null && !FilteredChannels.Contains(SelectedChannel))
        {
            SelectedChannel = null;
        }
    }

    private static string NormalizeGroup(string? group) =>
        string.IsNullOrWhiteSpace(group) ? "Uncategorized" : group.Trim();
}
