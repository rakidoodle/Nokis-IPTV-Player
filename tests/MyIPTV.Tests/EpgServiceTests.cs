using System.Net;
using System.Net.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Infrastructure.Epg;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class EpgServiceTests
{
    [TestMethod]
    public async Task LocalXmlTvRefreshCachesGuideAndReturnsNowNext()
    {
        using TemporaryApplicationPaths paths = new();
        paths.EnsureDirectoriesExist();
        await new SqliteDatabaseService(paths, NullLogger<SqliteDatabaseService>.Instance).InitializeAsync();
        string sourcePath = Path.Combine(paths.DataDirectory, "demo.xml");
        await File.WriteAllTextAsync(sourcePath,
            """
            <tv>
              <channel id="demo"><display-name>Demo Channel</display-name></channel>
              <programme start="20260823100000 +0000" stop="20260823110000 +0000" channel="demo"><title>Now Show</title></programme>
              <programme start="20260823110000 +0000" stop="20260823120000 +0000" channel="demo"><title>Next Show</title></programme>
            </tv>
            """);
        EpgService service = CreateService(paths);

        EpgRefreshResult first = await service.RefreshAsync(sourcePath, TimeSpan.FromHours(1));
        await File.WriteAllTextAsync(sourcePath, "<not-valid");
        EpgRefreshResult cached = await service.RefreshAsync(sourcePath, TimeSpan.FromHours(1));
        EpgNowNext nowNext = await service.GetNowNextAsync(
            "demo", new DateTimeOffset(2026, 8, 23, 10, 30, 0, TimeSpan.Zero));
        IReadOnlyList<EpgChannelSchedule> guide = await service.GetGuideAsync(
            new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));

        Assert.IsTrue(first.IsSuccess);
        Assert.IsFalse(first.FromCache);
        Assert.IsTrue(cached.IsSuccess);
        Assert.IsTrue(cached.FromCache);
        Assert.AreEqual("Now Show", nowNext.Current?.Title);
        Assert.AreEqual("Next Show", nowNext.Next?.Title);
        Assert.HasCount(1, guide);
        Assert.HasCount(2, guide[0].Programs);
        SqliteConnection.ClearAllPools();
    }

    [TestMethod]
    public async Task HttpFailureReturnsFriendlyResultWithoutExposingAddress()
    {
        using TemporaryApplicationPaths paths = new();
        await new SqliteDatabaseService(paths, NullLogger<SqliteDatabaseService>.Instance).InitializeAsync();
        const string sensitiveAddress = "https://example.invalid/guide.xml?token=synthetic-secret";
        HttpClient client = new(new StubHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        EpgService service = new(
            new StubHttpClientFactory(client),
            new XmlTvParser(),
            new SqliteEpgRepository(paths),
            NullLogger<EpgService>.Instance);

        EpgRefreshResult result = await service.RefreshAsync(sensitiveAddress, TimeSpan.FromHours(1));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsFalse(result.Message.Contains("synthetic-secret", StringComparison.Ordinal));
        Assert.IsFalse(result.Message.Contains(sensitiveAddress, StringComparison.Ordinal));
        SqliteConnection.ClearAllPools();
    }

    private static EpgService CreateService(TemporaryApplicationPaths paths) =>
        new(
            new StubHttpClientFactory(new HttpClient(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)))),
            new XmlTvParser(),
            new SqliteEpgRepository(paths),
            NullLogger<EpgService>.Instance);

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
