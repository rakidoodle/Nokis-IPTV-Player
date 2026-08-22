using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers.M3U;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class M3uPlaylistImportServiceTests
{
    [TestMethod]
    public async Task ImportAsyncLoadsLocalPlaylistIntoCatalog()
    {
        using TemporaryApplicationPaths paths = new();
        paths.EnsureDirectoriesExist();
        string playlistPath = Path.Combine(paths.DataDirectory, "demo.m3u");
        await File.WriteAllTextAsync(
            playlistPath,
            "#EXTM3U\n#EXTINF:-1 group-title=\"News\",Demo News\nhttps://example.invalid/news.m3u8");
        InMemoryChannelCatalog catalog = new();
        M3uPlaylistImportService importer = CreateImporter(catalog, new HttpClient());
        IptvProfile profile = CreateProfile(playlistPath);

        PlaylistImportResult result = await importer.ImportAsync(profile);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.ImportedCount);
        IptvChannel channel = catalog.GetForProfile(profile.Id).Single();
        Assert.AreEqual("Demo News", channel.Name);
        Assert.AreEqual("News", channel.Group);
    }

    [TestMethod]
    public async Task ImportAsyncStreamsRemotePlaylistIntoCatalog()
    {
        const string playlist =
            "#EXTM3U\n#EXTINF:-1,Remote Demo\nhttps://example.invalid/remote-demo.m3u8";
        HttpClient client = new(new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(playlist),
            }));
        InMemoryChannelCatalog catalog = new();
        M3uPlaylistImportService importer = CreateImporter(catalog, client);
        IptvProfile profile = CreateProfile("https://playlist.example.invalid/demo.m3u");

        PlaylistImportResult result = await importer.ImportAsync(profile);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Remote Demo", catalog.GetForProfile(profile.Id).Single().Name);
    }

    [TestMethod]
    public async Task ImportAsyncRejectsPlaylistWithoutPlayableChannels()
    {
        using TemporaryApplicationPaths paths = new();
        paths.EnsureDirectoriesExist();
        string playlistPath = Path.Combine(paths.DataDirectory, "empty.m3u");
        await File.WriteAllTextAsync(playlistPath, "#EXTM3U\n# This playlist is intentionally empty");
        InMemoryChannelCatalog catalog = new();
        M3uPlaylistImportService importer = CreateImporter(catalog, new HttpClient());

        PlaylistImportResult result = await importer.ImportAsync(CreateProfile(playlistPath));

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.Message, "no playable channels");
        Assert.HasCount(0, catalog.GetAll());
    }

    private static M3uPlaylistImportService CreateImporter(
        InMemoryChannelCatalog catalog,
        HttpClient client) =>
        new(
            new StubHttpClientFactory(client),
            new M3uPlaylistParser(),
            catalog,
            NullLogger<M3uPlaylistImportService>.Instance);

    private static IptvProfile CreateProfile(string address) =>
        new(
            Guid.NewGuid(),
            "Synthetic M3U",
            ProfileConnectionType.M3uPlaylist,
            address,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
