namespace MyIPTV.Core.Models;

public sealed record FavoriteItem(
    Guid ProfileId,
    ContentKind ContentKind,
    string ContentId,
    string Title,
    DateTimeOffset AddedUtc);
