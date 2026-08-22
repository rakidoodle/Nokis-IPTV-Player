using MyIPTV.App.ViewModels;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers.M3U;

namespace MyIPTV.Tests;

[TestClass]
public sealed class LiveTvViewModelTests
{
    [TestMethod]
    public void ImportedChannelsUpdateLiveTvSummary()
    {
        InMemoryChannelCatalog catalog = new();
        FakePlaybackService playback = new();
        LiveTvViewModel viewModel = new(catalog, playback, new PlayerViewModel(playback, playback));
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
    public void SelectingCategoryFiltersChannelsCaseInsensitively()
    {
        InMemoryChannelCatalog catalog = new();
        FakePlaybackService playback = new();
        LiveTvViewModel viewModel = new(catalog, playback, new PlayerViewModel(playback, playback));
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
        LiveTvViewModel viewModel = new(catalog, playback, new PlayerViewModel(playback, playback));
        Guid profileId = Guid.NewGuid();
        IptvChannel channel = Channel("7", "Demo News", "News", profileId);
        catalog.ReplaceForProfile(profileId, [channel]);
        viewModel.SelectedChannel = channel;

        await viewModel.PlaySelectedChannelCommand.ExecuteAsync(null);

        Assert.AreEqual("7", playback.CurrentItem?.ContentId);
        Assert.AreEqual(ContentKind.LiveTv, playback.CurrentItem?.ContentKind);
        Assert.AreEqual("Demo News", playback.CurrentItem?.Title);
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
}
