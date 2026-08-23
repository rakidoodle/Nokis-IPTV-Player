namespace MyIPTV.Core.Models;

public sealed record SearchResult(
    SearchResultKind Kind,
    string Id,
    Guid ProfileId,
    string Title,
    string Subtitle,
    ContentKind? CategoryKind = null);
