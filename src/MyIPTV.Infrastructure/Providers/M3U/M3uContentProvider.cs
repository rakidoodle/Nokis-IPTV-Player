using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Providers.M3U;

public sealed class M3uContentProvider(
    IPlaylistImportService importService,
    IChannelCatalog channelCatalog) : IContentProvider
{
    public ProfileConnectionType ConnectionType => ProfileConnectionType.M3uPlaylist;

    public async Task<ProviderLoadResult> LoadCatalogAsync(
        IptvProfile profile,
        CancellationToken cancellationToken = default)
    {
        PlaylistImportResult imported = await importService.ImportAsync(profile, cancellationToken);
        if (!imported.IsSuccess)
        {
            return ProviderLoadResult.Failure(imported.Message);
        }

        ProviderCatalog catalog = new(
            [],
            channelCatalog.GetForProfile(profile.Id),
            [],
            []);
        return ProviderLoadResult.Success(imported.Message, catalog);
    }

    public Task<SeriesDetailsResult> LoadSeriesDetailsAsync(
        IptvProfile profile,
        string seriesId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SeriesDetailsResult.Failure("M3U profiles do not provide series metadata."));
}
