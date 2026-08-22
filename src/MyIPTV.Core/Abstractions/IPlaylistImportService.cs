using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IPlaylistImportService
{
    Task<PlaylistImportResult> ImportAsync(
        IptvProfile profile,
        CancellationToken cancellationToken = default);
}
