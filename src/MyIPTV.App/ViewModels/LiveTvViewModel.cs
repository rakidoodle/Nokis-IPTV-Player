using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.ViewModels;

public sealed class LiveTvViewModel : SectionViewModel
{
    private readonly IChannelCatalog _channelCatalog;

    public LiveTvViewModel(IChannelCatalog channelCatalog)
        : base(
        "Live TV",
        "Browse channels from your authorized IPTV profiles.",
        "No channels yet",
        "Connect an IPTV profile to import and browse live channels.",
        "\uE714")
    {
        _channelCatalog = channelCatalog;
        channelCatalog.ChannelsChanged += OnChannelsChanged;
        UpdateSummary();
    }

    private void OnChannelsChanged(object? sender, EventArgs e) => UpdateSummary();

    private void UpdateSummary()
    {
        int channelCount = _channelCatalog.GetAll().Count;
        if (channelCount == 0)
        {
            SetEmptyContent(
                "No channels yet",
                "Connect an M3U profile to import channels.");
            return;
        }

        string channelLabel = channelCount == 1 ? "channel" : "channels";
        SetEmptyContent(
            $"{channelCount:N0} {channelLabel} imported",
            "Your M3U catalog is ready. The virtualized channel browser arrives in Phase 11.");
    }
}
