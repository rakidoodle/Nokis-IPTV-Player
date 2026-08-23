using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers;
using MyIPTV.Infrastructure.Providers.M3U;
using MyIPTV.Infrastructure.Providers.Xtream;
using MyIPTV.Infrastructure.Providers.Xtream.Dtos;

namespace MyIPTV.Tests;

[TestClass]
public sealed class XtreamProviderTests
{
    [TestMethod]
    public async Task LoadCatalogAsyncMapsAuthorizedSyntheticCatalog()
    {
        Guid profileId = Guid.NewGuid();
        StubCredentialService credentials = new(
            new ProfileCredentials("demo user", "synthetic/pass"));
        InMemoryChannelCatalog channelCatalog = new();
        InMemoryMediaCatalog mediaCatalog = new();
        XtreamProvider provider = new(
            credentials,
            new StubXtreamClient(),
            channelCatalog,
            mediaCatalog,
            NullLogger<XtreamProvider>.Instance);
        IptvProfile profile = CreateProfile(profileId);

        ProviderLoadResult result = await provider.LoadCatalogAsync(profile);

        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(1, result.Catalog.LiveChannels);
        Assert.HasCount(1, result.Catalog.Movies);
        Assert.HasCount(1, result.Catalog.Series);
        Assert.AreEqual("202", result.Catalog.Movies.Single().Id);
        Assert.AreEqual("2026", result.Catalog.Movies.Single().Year);
        Assert.AreEqual("Synthetic plot", result.Catalog.Movies.Single().Description);
        Assert.AreEqual("303", result.Catalog.Series.Single().Id);
        IptvChannel channel = channelCatalog.GetForProfile(profileId).Single();
        Assert.AreEqual("Demo News", channel.Name);
        Assert.AreEqual("News", channel.Group);
        StringAssert.Contains(channel.StreamUrl, "/live/demo%20user/synthetic%2Fpass/101.m3u8");
        Assert.IsFalse(channel.ToString().Contains("synthetic/pass", StringComparison.Ordinal));
        Assert.HasCount(3, mediaCatalog.GetCategories());
    }

    [TestMethod]
    public async Task LoadCatalogAsyncRejectsFailedAuthenticationWithoutChangingCatalogs()
    {
        InMemoryChannelCatalog channelCatalog = new();
        InMemoryMediaCatalog mediaCatalog = new();
        XtreamProvider provider = new(
            new StubCredentialService(new ProfileCredentials("user", "password")),
            new StubXtreamClient(authenticated: false),
            channelCatalog,
            mediaCatalog,
            NullLogger<XtreamProvider>.Instance);

        ProviderLoadResult result = await provider.LoadCatalogAsync(CreateProfile(Guid.NewGuid()));

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.Message, "Authentication failed");
        Assert.HasCount(0, channelCatalog.GetAll());
        Assert.HasCount(0, mediaCatalog.GetMovies());
    }

    [TestMethod]
    public async Task LoadSeriesDetailsAsyncMapsEpisodesAndRedactsStreamUrl()
    {
        Guid profileId = Guid.NewGuid();
        XtreamProvider provider = new(
            new StubCredentialService(new ProfileCredentials("demo user", "synthetic/pass")),
            new StubXtreamClient(),
            new InMemoryChannelCatalog(),
            new InMemoryMediaCatalog(),
            NullLogger<XtreamProvider>.Instance);

        SeriesDetailsResult result = await provider.LoadSeriesDetailsAsync(
            CreateProfile(profileId),
            "303");

        Assert.IsTrue(result.IsSuccess);
        EpisodeItem episode = result.Episodes.Single();
        Assert.AreEqual("404", episode.Id);
        Assert.AreEqual(1, episode.SeasonNumber);
        Assert.AreEqual(2, episode.EpisodeNumber);
        StringAssert.Contains(episode.StreamUrl, "/series/demo%20user/synthetic%2Fpass/404.mp4");
        Assert.IsFalse(episode.ToString().Contains("synthetic/pass", StringComparison.Ordinal));
    }

    private static IptvProfile CreateProfile(Guid profileId) =>
        new(
            profileId,
            "Synthetic Xtream",
            ProfileConnectionType.XtreamApi,
            "https://example.invalid:8443/iptv",
            "demo user",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class StubCredentialService(ProfileCredentials credentials) : ICredentialService
    {
        public Task StoreAsync(
            Guid profileId,
            ProfileCredentials value,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ProfileCredentials?> RetrieveAsync(
            Guid profileId,
            CancellationToken cancellationToken = default) => Task.FromResult<ProfileCredentials?>(credentials);

        public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubXtreamClient(bool authenticated = true) : IXtreamClient
    {
        public Task<XtreamAuthenticationDto> AuthenticateAsync(
            string serverAddress,
            string username,
            string password,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new XtreamAuthenticationDto
            {
                UserInfo = new XtreamUserInfoDto
                {
                    Auth = JsonDocument.Parse(authenticated ? "1" : "0").RootElement.Clone(),
                    Status = "Active",
                    AllowedOutputFormats = ["m3u8"],
                },
            });

        public Task<IReadOnlyList<XtreamCategoryDto>> GetCategoriesAsync(
            string serverAddress,
            string username,
            string password,
            string action,
            CancellationToken cancellationToken = default)
        {
            string name = action switch
            {
                "get_live_categories" => "News",
                "get_vod_categories" => "Movies",
                _ => "Drama",
            };
            return Task.FromResult<IReadOnlyList<XtreamCategoryDto>>(
                [new XtreamCategoryDto { CategoryId = "7", CategoryName = name }]);
        }

        public Task<IReadOnlyList<XtreamLiveStreamDto>> GetLiveStreamsAsync(
            string serverAddress,
            string username,
            string password,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XtreamLiveStreamDto>>(
                [new XtreamLiveStreamDto
                {
                    StreamId = 101,
                    Name = "Demo News",
                    CategoryId = "7",
                    EpgChannelId = "demo.news",
                }]);

        public Task<IReadOnlyList<XtreamVodStreamDto>> GetVodStreamsAsync(
            string serverAddress,
            string username,
            string password,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XtreamVodStreamDto>>(
                [new XtreamVodStreamDto
                {
                    StreamId = 202,
                    Name = "Demo Movie",
                    CategoryId = "7",
                    ContainerExtension = "mkv",
                    Plot = "Synthetic plot",
                    Year = JsonDocument.Parse("2026").RootElement.Clone(),
                    Duration = "1h 30m",
                }]);

        public Task<IReadOnlyList<XtreamSeriesDto>> GetSeriesAsync(
            string serverAddress,
            string username,
            string password,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XtreamSeriesDto>>(
                [new XtreamSeriesDto { SeriesId = 303, Name = "Demo Series", CategoryId = "7" }]);

        public Task<XtreamSeriesInfoDto> GetSeriesInfoAsync(
            string serverAddress,
            string username,
            string password,
            string seriesId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new XtreamSeriesInfoDto
            {
                Episodes = new Dictionary<string, List<XtreamEpisodeDto>>
                {
                    ["1"] =
                    [
                        new XtreamEpisodeDto
                        {
                            Id = JsonDocument.Parse("404").RootElement.Clone(),
                            SeasonNumber = 1,
                            EpisodeNumber = 2,
                            Title = "Demo Episode",
                            ContainerExtension = "mp4",
                        },
                    ],
                },
            });

        public Task<XtreamEpgResponseDto> GetShortEpgAsync(
            string serverAddress,
            string username,
            string password,
            string streamId,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new XtreamEpgResponseDto());
    }
}
