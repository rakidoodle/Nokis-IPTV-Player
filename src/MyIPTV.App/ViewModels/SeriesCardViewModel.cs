using CommunityToolkit.Mvvm.ComponentModel;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class SeriesCardViewModel(SeriesItem series, bool isFavorite) : ObservableObject
{
    [ObservableProperty] private bool _isFavorite = isFavorite;
    public SeriesItem Series { get; } = series;
    public string Id => Series.Id;
    public Guid ProfileId => Series.ProfileId;
    public string Title => Series.Name;
    public string? PosterUrl => Series.PosterUrl;
    public string Description => string.IsNullOrWhiteSpace(Series.Plot) ? "No description supplied." : Series.Plot;
    public string Genre => string.IsNullOrWhiteSpace(Series.Genre) ? "Genre unavailable" : Series.Genre;
    public string Rating => string.IsNullOrWhiteSpace(Series.Rating) ? "Not rated" : $"Rating {Series.Rating}";
    public string ReleaseDate => string.IsNullOrWhiteSpace(Series.ReleaseDate) ? "Release date unavailable" : Series.ReleaseDate;
    public FavoriteItem ToFavorite() =>
        new(ProfileId, ContentKind.Series, Id, Title, DateTimeOffset.UtcNow);
}
