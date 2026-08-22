namespace MyIPTV.Core.Models;

public sealed record PlaylistImportResult(
    bool IsSuccess,
    string Message,
    int ImportedCount,
    int SkippedCount,
    int DuplicateCount)
{
    public static PlaylistImportResult Failure(string message) =>
        new(false, message, 0, 0, 0);

    public static PlaylistImportResult Success(
        string message,
        int importedCount,
        int skippedCount,
        int duplicateCount) =>
        new(true, message, importedCount, skippedCount, duplicateCount);
}
