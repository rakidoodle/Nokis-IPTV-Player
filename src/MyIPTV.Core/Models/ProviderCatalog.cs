namespace MyIPTV.Core.Models;

public sealed record ProviderCatalog(
    IReadOnlyList<ContentCategory> Categories,
    IReadOnlyList<IptvChannel> LiveChannels,
    IReadOnlyList<MovieItem> Movies,
    IReadOnlyList<SeriesItem> Series)
{
    public static ProviderCatalog Empty { get; } = new([], [], [], []);
}
