namespace MyIPTV.Core.Models;

public sealed record EpgParseResult(
    IReadOnlyList<EpgChannel> Channels,
    IReadOnlyList<EpgProgram> Programs,
    int SkippedPrograms);
