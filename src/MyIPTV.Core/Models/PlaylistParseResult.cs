namespace MyIPTV.Core.Models;

public sealed record PlaylistParseResult(
    bool IsValid,
    string Message,
    IReadOnlyList<IptvChannel> Channels,
    int SkippedEntries,
    int DuplicateEntries)
{
    public static PlaylistParseResult Invalid(string message) =>
        new(false, message, [], 0, 0);

    public static PlaylistParseResult Success(
        IReadOnlyList<IptvChannel> channels,
        int skippedEntries,
        int duplicateEntries) =>
        new(true, "Playlist parsed successfully.", channels, skippedEntries, duplicateEntries);
}
