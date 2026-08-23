using CommunityToolkit.Mvvm.ComponentModel;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class EpisodeCardViewModel(EpisodeItem episode, WatchHistoryItem? history) : ObservableObject
{
    [ObservableProperty] private WatchHistoryItem? _history = history;
    public EpisodeItem Episode { get; } = episode;
    public string Id => Episode.Id;
    public string Title => $"E{Episode.EpisodeNumber:N0} · {Episode.Name}";
    public string Description => string.IsNullOrWhiteSpace(Episode.Plot) ? "No episode description supplied." : Episode.Plot;
    public string Duration => string.IsNullOrWhiteSpace(Episode.Duration) ? "Duration unavailable" : Episode.Duration;
    public bool CanContinue => History?.CanContinue == true;
    public string ProgressLabel => CanContinue ? $"Continue at {History!.Position:h\\:mm\\:ss}" : Duration;

    partial void OnHistoryChanged(WatchHistoryItem? value)
    {
        OnPropertyChanged(nameof(CanContinue));
        OnPropertyChanged(nameof(ProgressLabel));
    }
}
