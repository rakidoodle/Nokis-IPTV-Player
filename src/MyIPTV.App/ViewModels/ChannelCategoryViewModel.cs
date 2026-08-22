namespace MyIPTV.App.ViewModels;

public sealed record ChannelCategoryViewModel(string Name, int ChannelCount, string? Group)
{
    public string DisplayName => $"{Name} ({ChannelCount:N0})";
}
