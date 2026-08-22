using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IM3uPlaylistParser
{
    Task<PlaylistParseResult> ParseAsync(
        Stream stream,
        Guid profileId,
        CancellationToken cancellationToken = default);
}
