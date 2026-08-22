namespace MyIPTV.Core.Models;

public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public string Theme { get; init; } = "Dark";

    public string StartPage { get; init; } = "Home";

    public bool RememberLastProfile { get; init; } = true;

    public int DefaultVolume { get; init; } = 80;
}
