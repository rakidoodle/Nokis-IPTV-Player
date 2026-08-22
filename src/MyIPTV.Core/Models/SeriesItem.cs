namespace MyIPTV.Core.Models;

public sealed record SeriesItem(
    string Id,
    Guid ProfileId,
    string Name,
    string CategoryId,
    string? PosterUrl,
    string? Plot,
    string? Genre,
    string? Rating,
    string? ReleaseDate);
