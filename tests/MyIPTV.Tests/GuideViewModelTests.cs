using MyIPTV.App.ViewModels;
using MyIPTV.Core.Models;

namespace MyIPTV.Tests;

[TestClass]
public sealed class GuideViewModelTests
{
    [TestMethod]
    public async Task RefreshBuildsGuideRowsFromServiceSchedules()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        FakeEpgService epg = new()
        {
            Guide =
            [
                new(
                    new EpgChannel("demo", "Demo Channel"),
                    [new EpgProgram("demo", now, now.AddHours(1), "Demo Show", "Synthetic")]),
            ],
        };
        GuideViewModel viewModel = new(epg) { Source = "https://example.invalid/demo.xml" };

        await viewModel.RefreshGuideCommand.ExecuteAsync(null);

        Assert.HasCount(1, viewModel.Rows);
        Assert.AreEqual("Demo Channel", viewModel.Rows[0].ChannelName);
        Assert.AreEqual("Demo Show", viewModel.Rows[0].Programs[0].Title);
        Assert.AreEqual("Guide refreshed.", viewModel.StatusMessage);
    }
}
