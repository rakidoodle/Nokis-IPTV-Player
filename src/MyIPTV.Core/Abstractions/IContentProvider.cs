using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IContentProvider
{
    ProfileConnectionType ConnectionType { get; }

    Task<ProviderLoadResult> LoadCatalogAsync(
        IptvProfile profile,
        CancellationToken cancellationToken = default);

    Task<SeriesDetailsResult> LoadSeriesDetailsAsync(
        IptvProfile profile,
        string seriesId,
        CancellationToken cancellationToken = default);
}
