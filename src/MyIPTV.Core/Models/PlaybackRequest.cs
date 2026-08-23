namespace MyIPTV.Core.Models;

public sealed record PlaybackRequest(
    string ContentId,
    ContentKind ContentKind,
    string Title,
    string StreamUrl,
    string? LogoUrl = null,
    string? CurrentProgram = null,
    string? NextProgram = null,
    TimeSpan? StartPosition = null,
    Guid? ProfileId = null)
{
    public override string ToString() =>
        $"PlaybackRequest {{ ContentId = {ContentId}, ContentKind = {ContentKind}, Title = {Title}, StreamUrl = [REDACTED] }}";
}
