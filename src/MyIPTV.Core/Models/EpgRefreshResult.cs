namespace MyIPTV.Core.Models;

public sealed record EpgRefreshResult(bool IsSuccess, bool FromCache, string Message, int ProgramCount)
{
    public static EpgRefreshResult Success(bool fromCache, string message, int programCount) =>
        new(true, fromCache, message, programCount);

    public static EpgRefreshResult Failure(string message, int cachedProgramCount = 0) =>
        new(false, cachedProgramCount > 0, message, cachedProgramCount);
}
