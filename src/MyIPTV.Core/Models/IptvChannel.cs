namespace MyIPTV.Core.Models;

public sealed record IptvChannel(
    string Id,
    Guid ProfileId,
    string Name,
    string StreamUrl,
    string? LogoUrl,
    string Group,
    string? EpgId,
    bool Favorite = false)
{
    public override string ToString() =>
        $"IptvChannel {{ Id = {Id}, ProfileId = {ProfileId}, Name = {Name}, StreamUrl = [REDACTED] }}";
}
