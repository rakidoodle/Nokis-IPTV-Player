using CommunityToolkit.Mvvm.ComponentModel;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class MovieCardViewModel(
    MovieItem movie,
    string categoryName,
    bool isFavorite,
    WatchHistoryItem? history) : ObservableObject
{
    [ObservableProperty]
    private bool _isFavorite = isFavorite;

    [ObservableProperty]
    private WatchHistoryItem? _history = history;

    public MovieItem Movie { get; } = movie;
    public string Id => Movie.Id;
    public Guid ProfileId => Movie.ProfileId;
    public string Title => Movie.Name;
    public string CategoryName { get; } = categoryName;
    public string? PosterUrl => Movie.PosterUrl;
    public string Rating => string.IsNullOrWhiteSpace(Movie.Rating) ? "Not rated" : $"Rating {Movie.Rating}";
    public string Year => string.IsNullOrWhiteSpace(Movie.Year) ? "Year unavailable" : Movie.Year;
    public string Duration => string.IsNullOrWhiteSpace(Movie.Duration) ? "Duration unavailable" : Movie.Duration;
    public string Description => string.IsNullOrWhiteSpace(Movie.Description)
        ? "No description supplied by the provider."
        : Movie.Description;
    public bool CanContinue => History?.CanContinue == true;
    public string ProgressLabel => CanContinue ? $"Continue at {History!.Position:h\\:mm\\:ss}" : "Not started";

    partial void OnHistoryChanged(WatchHistoryItem? value)
    {
        OnPropertyChanged(nameof(CanContinue));
        OnPropertyChanged(nameof(ProgressLabel));
    }

    public FavoriteItem ToFavorite() =>
        new(ProfileId, ContentKind.Movie, Id, Title, DateTimeOffset.UtcNow);
}
