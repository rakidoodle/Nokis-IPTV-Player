namespace MyIPTV.Core.Models;

public sealed record MovieItem(
    string Id,
    Guid ProfileId,
    string Name,
    string CategoryId,
    string StreamUrl,
    string? PosterUrl,
    string? Rating,
    string? ContainerExtension,
    string? Description = null,
    string? Year = null,
    string? Duration = null)
{
    public override string ToString() =>
        $"MovieItem {{ Id = {Id}, ProfileId = {ProfileId}, Name = {Name}, StreamUrl = [REDACTED] }}";
}
