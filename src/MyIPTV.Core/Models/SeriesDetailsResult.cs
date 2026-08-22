namespace MyIPTV.Core.Models;

public sealed record SeriesDetailsResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<EpisodeItem> Episodes)
{
    public static SeriesDetailsResult Failure(string message) => new(false, message, []);

    public static SeriesDetailsResult Success(IReadOnlyList<EpisodeItem> episodes) =>
        new(true, "Series details loaded.", episodes);
}
