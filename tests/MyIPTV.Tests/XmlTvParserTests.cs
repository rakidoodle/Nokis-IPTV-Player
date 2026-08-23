using System.Text;
using System.Xml;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Epg;

namespace MyIPTV.Tests;

[TestClass]
public sealed class XmlTvParserTests
{
    [TestMethod]
    public async Task ParsesChannelsProgramsDescriptionsAndNumericTimeZones()
    {
        const string xml =
            """
            <tv>
              <channel id="demo.news"><display-name>Demo News</display-name></channel>
              <programme start="20260823190000 +0800" stop="20260823200000 +0800" channel="demo.news">
                <title>Evening News</title><desc>Headlines
                  and weather</desc>
              </programme>
            </tv>
            """;

        EpgParseResult result = await ParseAsync(xml);

        Assert.HasCount(1, result.Channels);
        Assert.HasCount(1, result.Programs);
        Assert.AreEqual("Demo News", result.Channels[0].DisplayName);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 23, 11, 0, 0, TimeSpan.Zero), result.Programs[0].StartUtc);
        Assert.AreEqual("Headlines and weather", result.Programs[0].Description);
    }

    [TestMethod]
    public async Task InfersMissingStopFromNextProgramAndSkipsInvalidEntries()
    {
        const string xml =
            """
            <tv>
              <programme start="20260823100000 +0000" channel="demo"><title>First</title></programme>
              <programme start="20260823103000 +0000" channel="demo"><title>Second</title></programme>
              <programme start="invalid" channel="demo"><title>Broken</title></programme>
            </tv>
            """;

        EpgParseResult result = await ParseAsync(xml);

        Assert.HasCount(2, result.Programs);
        Assert.AreEqual(result.Programs[1].StartUtc, result.Programs[0].EndUtc);
        Assert.AreEqual(TimeSpan.FromMinutes(30), result.Programs[1].EndUtc - result.Programs[1].StartUtc);
        Assert.AreEqual(1, result.SkippedPrograms);
    }

    [TestMethod]
    public async Task ProhibitsDocumentTypeAndExternalEntities()
    {
        const string xml = "<!DOCTYPE tv [<!ENTITY secret SYSTEM 'file:///c:/secret.txt'>]><tv>&secret;</tv>";

        await Assert.ThrowsExactlyAsync<XmlException>(() => ParseAsync(xml));
    }

    private static async Task<EpgParseResult> ParseAsync(string xml)
    {
        await using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));
        return await new XmlTvParser().ParseAsync(stream);
    }
}
