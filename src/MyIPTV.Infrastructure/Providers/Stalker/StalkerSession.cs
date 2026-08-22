namespace MyIPTV.Infrastructure.Providers.Stalker;

public sealed record StalkerSession(
    string AccessToken,
    string UserId,
    int ExpiresInSeconds)
{
    public override string ToString() =>
        $"StalkerSession {{ UserId = {UserId}, AccessToken = [REDACTED], ExpiresInSeconds = {ExpiresInSeconds} }}";
}
