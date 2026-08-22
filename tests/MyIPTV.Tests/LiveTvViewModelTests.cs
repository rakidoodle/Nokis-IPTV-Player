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
        LiveTvViewModel viewModel = new(catalog);
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

        Assert.AreEqual("1 channel imported", viewModel.EmptyTitle);
        StringAssert.Contains(viewModel.EmptyMessage, "Phase 11");
    }
}
