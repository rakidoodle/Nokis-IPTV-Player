using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IEpgRepository
{
    Task<EpgCacheState?> GetCacheStateAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task ReplaceAsync(
        string sourceKey,
        EpgParseResult data,
        EpgCacheState cacheState,
        CancellationToken cancellationToken = default);

    Task TouchAsync(
        string sourceKey,
        DateTimeOffset fetchedUtc,
        DateTimeOffset expiresUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EpgChannelSchedule>> GetGuideAsync(
        string sourceKey,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default);

    Task<EpgNowNext> GetNowNextAsync(
        string sourceKey,
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
