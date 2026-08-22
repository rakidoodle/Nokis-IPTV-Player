namespace MyIPTV.Core.Models;

public sealed record ContentCategory(
    string Id,
    Guid ProfileId,
    string Name,
    ContentKind Kind);
