namespace MyIPTV.Core.Models;

public sealed record EpisodeItem(
    string Id,
    Guid ProfileId,
    string SeriesId,
    int SeasonNumber,
    int EpisodeNumber,
    string Name,
    string StreamUrl,
    string? ContainerExtension,
    string? Plot,
    string? Duration)
{
    public override string ToString() =>
        $"EpisodeItem {{ Id = {Id}, ProfileId = {ProfileId}, Name = {Name}, StreamUrl = [REDACTED] }}";
}
