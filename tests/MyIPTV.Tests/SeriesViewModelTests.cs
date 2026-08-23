using MyIPTV.App.ViewModels;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers;

namespace MyIPTV.Tests;

[TestClass]
public sealed class SeriesViewModelTests
{
    [TestMethod]
    public async Task BuildsSeasonEpisodeHierarchyAndContinuesSavedEpisode()
    {
        Guid profileId = Guid.NewGuid();
        InMemoryMediaCatalog media = CreateCatalog(profileId);
        FakeWatchHistoryRepository history = new();
        await history.UpsertAsync(new(
            profileId, ContentKind.Series, "episode-2", "Second Episode", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(11), TimeSpan.FromMinutes(44)));
        FakePlaybackService playback = new();
        SeriesViewModel viewModel = new(
            media, new FakeFavoriteRepository(), history, new StubProfileRepository(profileId),
            [new StubContentProvider()], playback, new PlayerViewModel(playback, playback));

        Assert.HasCount(2, viewModel.Seasons);
        Assert.AreEqual(1, viewModel.SelectedSeason?.Number);
        viewModel.SelectedSeason = viewModel.Seasons.Single(season => season.Number == 2);
        Assert.HasCount(1, viewModel.Episodes);
        Assert.IsTrue(viewModel.CanContinueEpisode);

        await viewModel.ContinueEpisodeCommand.ExecuteAsync(null);

        Assert.AreEqual("episode-2", playback.CurrentItem?.ContentId);
        Assert.AreEqual(TimeSpan.FromMinutes(11), playback.CurrentItem?.StartPosition);
        Assert.AreEqual(profileId, playback.CurrentItem?.ProfileId);
    }

    [TestMethod]
    public async Task SeriesFavoriteUsesStableSeriesIdentity()
    {
        Guid profileId = Guid.NewGuid();
        FakeFavoriteRepository favorites = new();
        FakePlaybackService playback = new();
        SeriesViewModel viewModel = new(
            CreateCatalog(profileId), favorites, new FakeWatchHistoryRepository(),
            new StubProfileRepository(profileId), [new StubContentProvider()], playback,
            new PlayerViewModel(playback, playback));

        await viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.IsTrue(await favorites.ContainsAsync(profileId, ContentKind.Series, "series-1"));
    }

    private static InMemoryMediaCatalog CreateCatalog(Guid profileId)
    {
        InMemoryMediaCatalog media = new();
        media.ReplaceForProfile(profileId, [], [],
            [new("series-1", profileId, "Demo Series", "series-category", null,
                "A safe synthetic series.", "Drama", "8.8", "2026")]);
        media.ReplaceSeriesEpisodes(profileId, "series-1",
        [
            new("episode-1", profileId, "series-1", 1, 1, "Pilot",
                "https://example.invalid/e1.mp4", "mp4", "Pilot episode", "42m"),
            new("episode-2", profileId, "series-1", 2, 1, "Second Episode",
                "https://example.invalid/e2.mp4", "mp4", "Second season", "44m"),
        ]);
        return media;
    }

    private sealed class StubProfileRepository(Guid profileId) : IProfileRepository
    {
        private readonly IptvProfile _profile = new(
            profileId, "Demo", ProfileConnectionType.XtreamApi, "https://example.invalid", "demo",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        public Task<IReadOnlyList<IptvProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IptvProfile>>([_profile]);
        public Task<IptvProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IptvProfile?>(id == profileId ? _profile : null);
        public Task UpsertAsync(IptvProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubContentProvider : IContentProvider
    {
        public ProfileConnectionType ConnectionType => ProfileConnectionType.XtreamApi;
        public Task<ProviderLoadResult> LoadCatalogAsync(IptvProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProviderLoadResult.Failure("Not used"));
        public Task<SeriesDetailsResult> LoadSeriesDetailsAsync(
            IptvProfile profile, string seriesId, CancellationToken cancellationToken = default) =>
            Task.FromResult(SeriesDetailsResult.Failure("Not used"));
    }
}
