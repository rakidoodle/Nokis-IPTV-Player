namespace MyIPTV.Core.Models;

public sealed class ProfileDraft
{
    public Guid? Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public ProfileConnectionType ConnectionType { get; init; } = ProfileConnectionType.M3uPlaylist;

    public string ServerAddress { get; init; } = string.Empty;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public override string ToString() =>
        $"ProfileDraft {{ Id = {Id}, ConnectionType = {ConnectionType}, Password = [REDACTED] }}";
}
