namespace MyIPTV.Core.Models;

public sealed record WatchHistoryItem(
    Guid ProfileId,
    ContentKind ContentKind,
    string ContentId,
    string Title,
    DateTimeOffset LastWatchedUtc,
    TimeSpan Position,
    TimeSpan? Duration)
{
    public bool CanContinue => ContentKind is not ContentKind.LiveTv && Position > TimeSpan.Zero;
}
