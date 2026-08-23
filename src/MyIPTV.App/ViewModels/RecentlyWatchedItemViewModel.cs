using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed class RecentlyWatchedItemViewModel(WatchHistoryItem item)
{
    public WatchHistoryItem Item { get; } = item;
    public string Title => Item.Title;
    public string KindLabel => Item.ContentKind switch
    {
        ContentKind.LiveTv => "Live channel",
        ContentKind.Movie => "Movie",
        ContentKind.Series => "Episode",
        _ => "Content",
    };
    public string ProgressLabel => Item.CanContinue
        ? $"Continue at {Item.Position:h\\:mm\\:ss}"
        : $"Watched {Item.LastWatchedUtc.ToLocalTime():g}";
    public string ActionLabel => Item.CanContinue ? "Continue watching" : "Watch again";
}
