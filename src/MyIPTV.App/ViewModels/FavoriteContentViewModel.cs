using CommunityToolkit.Mvvm.ComponentModel;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class FavoriteContentViewModel(
    Guid profileId,
    ContentKind contentKind,
    string contentId,
    string title,
    string subtitle,
    bool isFavorite) : ObservableObject
{
    [ObservableProperty]
    private bool _isFavorite = isFavorite;

    public Guid ProfileId { get; } = profileId;
    public ContentKind ContentKind { get; } = contentKind;
    public string ContentId { get; } = contentId;
    public string Title { get; } = title;
    public string Subtitle { get; } = subtitle;

    public string KindLabel => ContentKind switch
    {
        ContentKind.LiveTv => "Live channel",
        ContentKind.Movie => "Movie",
        ContentKind.Series => "Series",
        _ => "Content",
    };

    public FavoriteItem ToFavorite() =>
        new(ProfileId, ContentKind, ContentId, Title, DateTimeOffset.UtcNow);
}
