namespace MyIPTV.Core.Models;

public sealed class NetworkOptions
{
    public const string SectionName = "Network";

    public int TimeoutSeconds { get; init; } = 30;

    public int PlaylistTimeoutSeconds { get; init; } = 120;
}
