namespace MyIPTV.Core.Models;

public sealed record EpgCacheState(
    string SourceKey,
    DateTimeOffset FetchedUtc,
    DateTimeOffset ExpiresUtc,
    string? EntityTag,
    DateTimeOffset? LastModifiedUtc,
    int ProgramCount);
