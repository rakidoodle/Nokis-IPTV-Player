using MyIPTV.App.ViewModels;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers;

namespace MyIPTV.Tests;

[TestClass]
public sealed class MoviesViewModelTests
{
    [TestMethod]
    public async Task MovieBrowserDisplaysMetadataAndContinuesAtSavedPosition()
    {
        Guid profileId = Guid.NewGuid();
        InMemoryMediaCatalog media = CreateCatalog(profileId);
        FakeWatchHistoryRepository history = new();
        await history.UpsertAsync(new(
            profileId, ContentKind.Movie, "movie-1", "Demo Movie", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(18), TimeSpan.FromMinutes(95)));
        FakePlaybackService playback = new();
        MoviesViewModel viewModel = new(
            media, new FakeFavoriteRepository(), history, playback,
            new PlayerViewModel(playback, playback));

        Assert.HasCount(1, viewModel.FilteredMovies);
        Assert.AreEqual("Drama", viewModel.SelectedMovie?.CategoryName);
        Assert.AreEqual("2026", viewModel.SelectedMovie?.Year);
        Assert.AreEqual("1h 35m", viewModel.SelectedMovie?.Duration);
        Assert.IsTrue(viewModel.CanContinueSelected);

        await viewModel.ContinueMovieCommand.ExecuteAsync(null);

        Assert.AreEqual("movie-1", playback.CurrentItem?.ContentId);
        Assert.AreEqual(TimeSpan.FromMinutes(18), playback.CurrentItem?.StartPosition);
        Assert.AreEqual(profileId, playback.CurrentItem?.ProfileId);
    }

    [TestMethod]
    public async Task MovieFavoriteToggleUsesStableMovieIdentity()
    {
        Guid profileId = Guid.NewGuid();
        FakeFavoriteRepository favorites = new();
        FakePlaybackService playback = new();
        MoviesViewModel viewModel = new(
            CreateCatalog(profileId), favorites, new FakeWatchHistoryRepository(), playback,
            new PlayerViewModel(playback, playback));

        await viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.IsTrue(await favorites.ContainsAsync(profileId, ContentKind.Movie, "movie-1"));
    }

    private static InMemoryMediaCatalog CreateCatalog(Guid profileId)
    {
        InMemoryMediaCatalog media = new();
        string categoryId = $"xtream:{profileId:N}:movie:7";
        media.ReplaceForProfile(profileId,
            [new(categoryId, profileId, "Drama", ContentKind.Movie)],
            [new("movie-1", profileId, "Demo Movie", categoryId,
                "https://example.invalid/movie.mp4", null, "8.4", "mp4",
                "A safe synthetic movie.", "2026", "1h 35m")],
            []);
        return media;
    }
}
