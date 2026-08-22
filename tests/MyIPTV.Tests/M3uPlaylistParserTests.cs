using System.Text;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers.M3U;

namespace MyIPTV.Tests;

[TestClass]
public sealed class M3uPlaylistParserTests
{
    private readonly M3uPlaylistParser _parser = new();

    [TestMethod]
    public async Task ParseAsyncReadsCommonMetadataAndMissingMetadata()
    {
        const string playlist = """
            #EXTM3U x-tvg-url="https://example.invalid/epg.xml"
            #EXTINF:-1 tvg-id="news.demo" tvg-name="Demo News HD" tvg-logo="https://example.invalid/news.png" group-title="News, Local",Fallback News
            https://example.invalid/live/news.m3u8
            #EXTINF:-1,Demo Sports
            udp://@239.10.10.10:1234
            https://example.invalid/live/unnamed.m3u8
            """;
        Guid profileId = Guid.NewGuid();

        PlaylistParseResult result = await ParseTextAsync(playlist, profileId);

        Assert.IsTrue(result.IsValid);
        Assert.HasCount(3, result.Channels);
        IptvChannel news = result.Channels[0];
        Assert.AreEqual("Demo News HD", news.Name);
        Assert.AreEqual("news.demo", news.EpgId);
        Assert.AreEqual("News, Local", news.Group);
        Assert.AreEqual("https://example.invalid/news.png", news.LogoUrl);
        Assert.AreEqual(profileId, news.ProfileId);
        Assert.AreEqual("Demo Sports", result.Channels[1].Name);
        Assert.AreEqual("Uncategorized", result.Channels[1].Group);
        Assert.AreEqual("Channel 3", result.Channels[2].Name);
    }

    [TestMethod]
    public async Task ParseAsyncSkipsMalformedAndDuplicateEntries()
    {
        const string playlist = """
            #EXTM3U
            #EXTINF:-1,Missing stream
            #EXTINF:-1,Playable
            https://example.invalid/live/playable.m3u8
            #EXTINF:-1,Invalid stream
            relative/not-a-stream
            #EXTINF:-1,Duplicate
            https://example.invalid/live/playable.m3u8
            """;

        PlaylistParseResult result = await ParseTextAsync(playlist, Guid.NewGuid());

        Assert.IsTrue(result.IsValid);
        Assert.HasCount(1, result.Channels);
        Assert.AreEqual(2, result.SkippedEntries);
        Assert.AreEqual(1, result.DuplicateEntries);
    }

    [TestMethod]
    public async Task ParseAsyncRejectsMissingHeader()
    {
        PlaylistParseResult result = await ParseTextAsync(
            "#EXTINF:-1,Demo\nhttps://example.invalid/live",
            Guid.NewGuid());

        Assert.IsFalse(result.IsValid);
        Assert.HasCount(0, result.Channels);
        StringAssert.Contains(result.Message, "#EXTM3U");
    }

    [TestMethod]
    public async Task ParseAsyncCreatesStableIdsForTheSameProfile()
    {
        const string playlist = "#EXTM3U\n#EXTINF:-1 tvg-id=\"demo\",Demo\nhttps://example.invalid/live";
        Guid profileId = Guid.NewGuid();

        PlaylistParseResult first = await ParseTextAsync(playlist, profileId);
        PlaylistParseResult second = await ParseTextAsync(playlist, profileId);

        Assert.AreEqual(first.Channels.Single().Id, second.Channels.Single().Id);
    }

    [TestMethod]
    public async Task ParseAsyncSupportsLargeSyntheticPlaylist()
    {
        const int channelCount = 20_000;
        StringBuilder playlist = new("#EXTM3U\n", channelCount * 90);
        for (int index = 0; index < channelCount; index++)
        {
            playlist.Append("#EXTINF:-1 tvg-id=\"demo.")
                .Append(index)
                .Append("\" group-title=\"Demo\",Demo ")
                .Append(index)
                .Append("\nhttps://example.invalid/live/")
                .Append(index)
                .Append(".m3u8\n");
        }

        PlaylistParseResult result = await ParseTextAsync(playlist.ToString(), Guid.NewGuid());

        Assert.IsTrue(result.IsValid);
        Assert.HasCount(channelCount, result.Channels);
        Assert.AreEqual(0, result.SkippedEntries);
    }

    [TestMethod]
    public async Task ParseAsyncHonorsCancellation()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("#EXTM3U\nhttps://example.invalid/live"));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await _parser.ParseAsync(stream, Guid.NewGuid(), cancellation.Token));
    }

    private async Task<PlaylistParseResult> ParseTextAsync(string playlist, Guid profileId)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(playlist));
        return await _parser.ParseAsync(stream, profileId);
    }
}
