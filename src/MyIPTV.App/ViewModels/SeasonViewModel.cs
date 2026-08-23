namespace MyIPTV.App.ViewModels;

public sealed record SeasonViewModel(int Number, int EpisodeCount)
{
    public string DisplayName => $"Season {Number:N0} ({EpisodeCount:N0})";
}
