namespace MyIPTV.Core.Models;

public sealed record EpgProgram(
    string ChannelId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Title,
    string? Description);
