namespace MyIPTV.Core.Models;

public sealed record IptvProfile(
    Guid Id,
    string Name,
    ProfileConnectionType ConnectionType,
    string ServerAddress,
    string? Username,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
