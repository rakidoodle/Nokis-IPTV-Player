namespace MyIPTV.Core.Models;

public sealed record AppSettings
{
    public const int CurrentVersion = 2;

    public int Version { get; init; } = CurrentVersion;

    public string Theme { get; init; } = "Dark";

    public string StartPage { get; init; } = "Home";

    public bool RememberLastProfile { get; init; } = true;

    public int DefaultVolume { get; init; } = 80;

    public string Language { get; init; } = "en-US";

    public bool HardwareDecoding { get; init; } = true;

    public string AspectRatio { get; init; } = "Default";

    public bool ReconnectOnFailure { get; init; } = true;

    public string EpgSource { get; init; } = string.Empty;

    public int EpgRefreshHours { get; init; } = 6;

    public string EpgTimezoneBehavior { get; init; } = "Local";
}
