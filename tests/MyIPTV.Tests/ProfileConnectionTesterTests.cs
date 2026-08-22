using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Services;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class ProfileConnectionTesterTests
{
    [TestMethod]
    public async Task TestAsyncAcceptsSyntheticLocalM3uPlaylist()
    {
        using TemporaryApplicationPaths paths = new();
        paths.EnsureDirectoriesExist();
        string playlistPath = Path.Combine(paths.DataDirectory, "demo.m3u");
        await File.WriteAllTextAsync(
            playlistPath,
            "#EXTM3U\n#EXTINF:-1,Demo News\nhttps://example.invalid/live");
        ProfileConnectionTester tester = new(
            new FakeHttpClientFactory(),
            NullLogger<ProfileConnectionTester>.Instance);
        ProfileDraft draft = new()
        {
            Name = "Demo",
            ConnectionType = ProfileConnectionType.M3uPlaylist,
            ServerAddress = playlistPath,
        };

        ConnectionTestResult result = await tester.TestAsync(draft);

        Assert.IsTrue(result.IsSuccess);
        StringAssert.Contains(result.Message, "valid M3U header");
    }

    [TestMethod]
    public async Task TestAsyncRejectsMalformedLocalPlaylist()
    {
        using TemporaryApplicationPaths paths = new();
        paths.EnsureDirectoriesExist();
        string playlistPath = Path.Combine(paths.DataDirectory, "bad.m3u");
        await File.WriteAllTextAsync(playlistPath, "not a playlist");
        ProfileConnectionTester tester = new(
            new FakeHttpClientFactory(),
            NullLogger<ProfileConnectionTester>.Instance);
        ProfileDraft draft = new()
        {
            Name = "Bad Demo",
            ConnectionType = ProfileConnectionType.M3uPlaylist,
            ServerAddress = playlistPath,
        };

        ConnectionTestResult result = await tester.TestAsync(draft);

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.Message, "#EXTM3U");
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
