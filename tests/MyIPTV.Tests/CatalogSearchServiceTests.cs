using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers;
using MyIPTV.Infrastructure.Providers.M3U;
using MyIPTV.Infrastructure.Services;

namespace MyIPTV.Tests;

[TestClass]
public sealed class CatalogSearchServiceTests
{
    [TestMethod]
    public async Task SearchFindsEveryLoadedContentType()
    {
        Guid profileId = Guid.NewGuid();
        InMemoryChannelCatalog channels = new();
        InMemoryMediaCatalog media = new();
        channels.ReplaceForProfile(profileId,
        [
            new("channel-1", profileId, "Demo News", "https://example.test/live", null, "Demo Group", null),
        ]);
        media.ReplaceForProfile(profileId,
        [
            new("category-1", profileId, "Demo Movies", ContentKind.Movie),
        ],
        [
            new("movie-1", profileId, "Demo Film", "category-1", "https://example.test/movie", null, null, null),
        ],
        [
            new("series-1", profileId, "Demo Series", "series-category", null, null, null, null, null),
        ]);
        media.ReplaceSeriesEpisodes(profileId, "series-1",
        [
            new("episode-1", profileId, "series-1", 1, 2, "Demo Episode", "https://example.test/episode", null, null, null),
        ]);
        CatalogSearchService service = new(channels, media);

        IReadOnlyList<SearchResult> results = await service.SearchAsync("demo");

        CollectionAssert.AreEquivalent(
            new[]
            {
                SearchResultKind.LiveChannel,
                SearchResultKind.Movie,
                SearchResultKind.Series,
                SearchResultKind.Episode,
                SearchResultKind.Category,
            },
            results.Select(result => result.Kind).ToArray());
    }

    [TestMethod]
    public async Task PrefixMatchesRankBeforeSubstringMatchesAndLimitIsHonored()
    {
        Guid profileId = Guid.NewGuid();
        InMemoryChannelCatalog channels = new();
        channels.ReplaceForProfile(profileId,
        [
            new("substring", profileId, "The News", "https://example.test/1", null, "General", null),
            new("prefix-b", profileId, "News World", "https://example.test/2", null, "General", null),
            new("prefix-a", profileId, "News Local", "https://example.test/3", null, "General", null),
        ]);
        CatalogSearchService service = new(channels, new InMemoryMediaCatalog());

        IReadOnlyList<SearchResult> results = await service.SearchAsync("news", 2);

        Assert.HasCount(2, results);
        Assert.AreEqual("News Local", results[0].Title);
        Assert.AreEqual("News World", results[1].Title);
    }

    [TestMethod]
    public async Task SearchRejectsCancelledWork()
    {
        CatalogSearchService service = new(new InMemoryChannelCatalog(), new InMemoryMediaCatalog());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => service.SearchAsync("demo", cancellationToken: cancellation.Token));
    }
}
