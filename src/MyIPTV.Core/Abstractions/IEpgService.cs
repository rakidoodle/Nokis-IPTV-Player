using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IEpgService
{
    event EventHandler? EpgChanged;

    Task<EpgRefreshResult> RefreshAsync(
        string source,
        TimeSpan refreshInterval,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EpgChannelSchedule>> GetGuideAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default);

    Task<EpgNowNext> GetNowNextAsync(
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
