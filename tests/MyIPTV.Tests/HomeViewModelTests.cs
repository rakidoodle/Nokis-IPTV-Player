using MyIPTV.App.ViewModels;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers;
using MyIPTV.Infrastructure.Providers.M3U;

namespace MyIPTV.Tests;

[TestClass]
public sealed class HomeViewModelTests
{
    [TestMethod]
    public async Task FavoriteCountUpdatesWhenRepositoryChanges()
    {
        FakeFavoriteRepository favorites = new();
        HomeViewModel viewModel = new(
            new NoOpNavigationService(),
            favorites,
            new FakeWatchHistoryRepository(),
            new InMemoryChannelCatalog(),
            new InMemoryMediaCatalog(),
            new FakePlaybackService());

        await favorites.SetAsync(
            new(Guid.NewGuid(), ContentKind.Movie, "movie-1", "Demo Movie", DateTimeOffset.UtcNow),
            true);

        Assert.AreEqual(1, viewModel.FavoriteCount);
    }

    [TestMethod]
    public async Task ContinueWatchingStartsMovieAtSavedPosition()
    {
        Guid profileId = Guid.NewGuid();
        InMemoryMediaCatalog media = new();
        media.ReplaceForProfile(profileId, [],
        [
            new("movie-1", profileId, "Demo Movie", "movies", "https://example.invalid/movie.mp4",
                null, null, "mp4"),
        ], []);
        FakeWatchHistoryRepository history = new();
        await history.UpsertAsync(new(
            profileId, ContentKind.Movie, "movie-1", "Demo Movie", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(90)));
        FakePlaybackService playback = new();
        HomeViewModel viewModel = new(
            new NoOpNavigationService(), new FakeFavoriteRepository(), history,
            new InMemoryChannelCatalog(), media, playback);

        await viewModel.PlayRecentCommand.ExecuteAsync(null);

        Assert.AreEqual("movie-1", playback.CurrentItem?.ContentId);
        Assert.AreEqual(TimeSpan.FromMinutes(14), playback.CurrentItem?.StartPosition);
        Assert.AreEqual(profileId, playback.CurrentItem?.ProfileId);
    }

    private sealed class NoOpNavigationService : INavigationService
    {
        public object? CurrentViewModel => null;
        public event EventHandler? CurrentViewModelChanged;
        public void NavigateTo<TViewModel>() where TViewModel : class =>
            CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
        public void NavigateTo(Type viewModelType) =>
            CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}
