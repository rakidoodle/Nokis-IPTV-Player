using MyIPTV.App.Services;
using MyIPTV.App.ViewModels;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Infrastructure.Providers;

namespace MyIPTV.Tests;

[TestClass]
public sealed class MainWindowViewModelTests
{
    [TestMethod]
    public void SelectingNavigationItemUpdatesCurrentPage()
    {
        FakeNavigationService navigation = new();
        MainWindowViewModel viewModel = CreateViewModel(navigation);
        NavigationItemViewModel liveTv = viewModel.NavigationItems.Single(item => item.Label == "Live TV");

        viewModel.SelectedNavigationItem = liveTv;

        Assert.IsInstanceOfType<LiveTvViewModel>(viewModel.CurrentViewModel);
        Assert.AreEqual("Viewing Live TV", viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task ToggleThemeCommandAppliesAndPersistsTheme()
    {
        FakeNavigationService navigation = new();
        FakeThemeService theme = new();
        FakeSettingsService settings = new();
        MainWindowViewModel viewModel = CreateViewModel(navigation, theme, settings);

        await viewModel.ToggleThemeCommand.ExecuteAsync(null);

        Assert.AreEqual(AppTheme.Light, theme.CurrentTheme);
        Assert.AreEqual("Light", settings.Settings.Theme);
        Assert.AreEqual("Light theme applied", viewModel.StatusMessage);
    }

    private static MainWindowViewModel CreateViewModel(
        FakeNavigationService navigation,
        FakeThemeService? theme = null,
        FakeSettingsService? settings = null) =>
        new(
            navigation,
            new FakeApplicationConfiguration(),
            settings ?? new FakeSettingsService(),
            theme ?? new FakeThemeService(),
            new FakePlaybackService(),
            new FakeSearchService(),
            NullLogger<MainWindowViewModel>.Instance);

    private sealed class FakeSearchService : ISearchService
    {
        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query,
            int maximumResults = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchResult>>([]);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public object? CurrentViewModel { get; private set; }

        public event EventHandler? CurrentViewModelChanged;

        public void NavigateTo<TViewModel>() where TViewModel : class
        {
            NavigateTo(typeof(TViewModel));
        }

        public void NavigateTo(Type viewModelType)
        {
            CurrentViewModel = viewModelType == typeof(HomeViewModel)
                ? CreateHomeViewModel()
                : viewModelType == typeof(LiveTvViewModel)
                    ? CreateLiveTvViewModel()
                : Activator.CreateInstance(viewModelType)
                  ?? throw new InvalidOperationException($"Could not create {viewModelType.Name}.");
            CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
        }

        private static LiveTvViewModel CreateLiveTvViewModel()
        {
            FakePlaybackService playback = new();
            return new LiveTvViewModel(
                new FakeChannelCatalog(),
                playback,
                new PlayerViewModel(playback, playback),
                new FakeFavoriteRepository(),
                new FakeEpgService());
        }

        private HomeViewModel CreateHomeViewModel()
        {
            FakePlaybackService playback = new();
            return new HomeViewModel(
                this,
                new FakeFavoriteRepository(),
                new FakeWatchHistoryRepository(),
                new FakeChannelCatalog(),
                new InMemoryMediaCatalog(),
                playback);
        }
    }

    private sealed class FakeThemeService : IThemeService
    {
        public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

        public event EventHandler? ThemeChanged;

        public void ApplyTheme(AppTheme theme)
        {
            CurrentTheme = theme;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; private set; } = new();

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeApplicationConfiguration : IApplicationConfiguration
    {
        public ApplicationOptions Application { get; } = new();

        public NetworkOptions Network { get; } = new();
    }

    private sealed class FakeChannelCatalog : IChannelCatalog
    {
        public event EventHandler? ChannelsChanged;

        public IReadOnlyList<IptvChannel> GetAll() => [];

        public IReadOnlyList<IptvChannel> GetForProfile(Guid profileId) => [];

        public void ReplaceForProfile(Guid profileId, IReadOnlyList<IptvChannel> channels) =>
            ChannelsChanged?.Invoke(this, EventArgs.Empty);

        public void RemoveProfile(Guid profileId) =>
            ChannelsChanged?.Invoke(this, EventArgs.Empty);
    }
}
