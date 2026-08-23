using MyIPTV.App.ViewModels;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers.M3U;
using MyIPTV.Infrastructure.Services;

namespace MyIPTV.Tests;

[TestClass]
public sealed class LiveTvViewModelTests
{
    [TestMethod]
    public void ImportedChannelsUpdateLiveTvSummary()
    {
        InMemoryChannelCatalog catalog = new();
        FakePlaybackService playback = new();
        LiveTvViewModel viewModel = CreateViewModel(catalog, playback);
        Guid profileId = Guid.NewGuid();
        catalog.ReplaceForProfile(
            profileId,
            [new IptvChannel(
                "demo-id",
                profileId,
                "Demo News",
                "https://example.invalid/news.m3u8",
                null,
                "News",
                "demo.news")]);

        Assert.AreEqual("1 channel available", viewModel.EmptyTitle);
        Assert.HasCount(2, viewModel.Categories);
        Assert.AreEqual("All channels", viewModel.SelectedCategory?.Name);
        Assert.HasCount(1, viewModel.FilteredChannels);
    }

    [TestMethod]
    public void ChangingActiveProfileShowsOnlyThatProfilesChannels()
    {
        InMemoryChannelCatalog catalog = new();
        ActiveProfileService activeProfiles = new();
        FakePlaybackService playback = new();
        LiveTvViewModel viewModel = CreateViewModel(catalog, playback, activeProfiles);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        catalog.ReplaceForProfile(firstId, [Channel("first", "First News", "News", firstId)]);
        catalog.ReplaceForProfile(secondId, [Channel("second", "Second News", "News", secondId)]);

        activeProfiles.SetActive(Profile(firstId, "First"));
        Assert.AreEqual("First News", viewModel.FilteredChannels.Single().Name);

        activeProfiles.SetActive(Profile(secondId, "Second"));
        Assert.AreEqual("Second News", viewModel.FilteredChannels.Single().Name);
    }

    [TestMethod]
    public void SelectingCategoryFiltersChannelsCaseInsensitively()
    {
        InMemoryChannelCatalog catalog = new();
        FakePlaybackService playback = new();
        LiveTvViewModel viewModel = CreateViewModel(catalog, playback);
        Guid profileId = Guid.NewGuid();
        catalog.ReplaceForProfile(profileId,
        [
            Channel("1", "Demo News", "News", profileId),
            Channel("2", "Demo World", "news", profileId),
            Channel("3", "Demo Sports", "Sports", profileId),
        ]);

        viewModel.SelectedCategory = viewModel.Categories.Single(category => category.Name == "News");

        Assert.HasCount(2, viewModel.FilteredChannels);
        Assert.IsTrue(viewModel.FilteredChannels.All(channel =>
            channel.Group.Equals("News", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task PlaySelectedChannelSendsRedactedPlaybackRequest()
    {
        InMemoryChannelCatalog catalog = new();
        FakePlaybackService playback = new();
        LiveTvViewModel viewModel = CreateViewModel(catalog, playback);
        Guid profileId = Guid.NewGuid();
        IptvChannel channel = Channel("7", "Demo News", "News", profileId);
        catalog.ReplaceForProfile(profileId, [channel]);
        viewModel.SelectedChannel = channel;

        await viewModel.PlaySelectedChannelCommand.ExecuteAsync(null);

        Assert.AreEqual("7", playback.CurrentItem?.ContentId);
        Assert.AreEqual(ContentKind.LiveTv, playback.CurrentItem?.ContentKind);
        Assert.AreEqual("Demo News", playback.CurrentItem?.Title);
    }

    [TestMethod]
    public async Task SearchResultSelectsItsCategoryAndStartsPlayback()
    {
        InMemoryChannelCatalog catalog = new();
        FakePlaybackService playback = new();
        LiveTvViewModel viewModel = CreateViewModel(catalog, playback);
        Guid profileId = Guid.NewGuid();
        IptvChannel news = Channel("news", "Demo News", "News", profileId);
        IptvChannel sports = Channel("sports", "Demo Sports", "Sports", profileId);
        catalog.ReplaceForProfile(profileId, [news, sports]);

        bool played = await viewModel.SelectAndPlaySearchResultAsync(profileId, sports.Id);

        Assert.IsTrue(played);
        Assert.AreEqual("Sports", viewModel.SelectedCategory?.Name);
        Assert.AreEqual(sports, viewModel.SelectedChannel);
        Assert.AreEqual(sports.Id, playback.CurrentItem?.ContentId);
    }

    [TestMethod]
    public async Task ToggleFavoriteUsesStableChannelIdentity()
    {
        InMemoryChannelCatalog catalog = new();
        FakePlaybackService playback = new();
        FakeFavoriteRepository favorites = new();
        LiveTvViewModel viewModel = new(
            catalog, new ActiveProfileService(), playback,
            new PlayerViewModel(playback, playback), favorites, new FakeEpgService());
        Guid profileId = Guid.NewGuid();
        IptvChannel channel = Channel("favorite-7", "Demo News", "News", profileId);
        catalog.ReplaceForProfile(profileId, [channel]);
        viewModel.SelectedChannel = channel;

        await viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.IsTrue(await favorites.ContainsAsync(profileId, ContentKind.LiveTv, "favorite-7"));
    }

    [TestMethod]
    public void SelectingMappedChannelDisplaysCurrentAndNextPrograms()
    {
        InMemoryChannelCatalog catalog = new();
        FakePlaybackService playback = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        FakeEpgService epg = new()
        {
            NowNext = new(
                new("demo.8", now.AddMinutes(-10), now.AddMinutes(20), "Current News", null),
                new("demo.8", now.AddMinutes(20), now.AddMinutes(50), "Next News", null)),
        };
        LiveTvViewModel viewModel = new(
            catalog, new ActiveProfileService(), playback,
            new PlayerViewModel(playback, playback), new FakeFavoriteRepository(), epg);
        Guid profileId = Guid.NewGuid();
        IptvChannel channel = Channel("8", "Demo News", "News", profileId);
        catalog.ReplaceForProfile(profileId, [channel]);

        viewModel.SelectedChannel = channel;

        Assert.AreEqual("Current News", viewModel.SelectedCurrentProgram);
        Assert.AreEqual("Next News", viewModel.SelectedNextProgram);
    }

    private static IptvChannel Channel(string id, string name, string group, Guid profileId) =>
        new(
            id,
            profileId,
            name,
            $"https://example.invalid/{id}.m3u8",
            null,
            group,
            $"demo.{id}");

    private static LiveTvViewModel CreateViewModel(
        InMemoryChannelCatalog catalog,
        FakePlaybackService playback,
        ActiveProfileService? activeProfiles = null) =>
        new(catalog, activeProfiles ?? new ActiveProfileService(), playback, new PlayerViewModel(playback, playback),
            new FakeFavoriteRepository(), new FakeEpgService());

    private static IptvProfile Profile(Guid id, string name) =>
        new(
            id,
            name,
            ProfileConnectionType.M3uPlaylist,
            "https://example.invalid/list.m3u",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
