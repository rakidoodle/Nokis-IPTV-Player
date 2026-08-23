namespace MyIPTV.Infrastructure.Providers.Stalker;

public sealed record StalkerSession(
    string AccessToken,
    string UserId,
    int ExpiresInSeconds,
    bool IsMacSession = false)
{
    public override string ToString() =>
        $"StalkerSession {{ UserId = {UserId}, AccessToken = [REDACTED], ExpiresInSeconds = {ExpiresInSeconds} }}";
}
