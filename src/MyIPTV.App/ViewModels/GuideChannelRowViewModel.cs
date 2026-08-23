namespace MyIPTV.App.ViewModels;

public sealed record GuideChannelRowViewModel(
    string ChannelId,
    string ChannelName,
    IReadOnlyList<GuideProgramViewModel> Programs);
