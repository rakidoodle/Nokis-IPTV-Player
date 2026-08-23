using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Tests;

internal sealed class FakeEpgService : IEpgService
{
    public event EventHandler? EpgChanged;

    public EpgNowNext NowNext { get; set; } = new(null, null);

    public IReadOnlyList<EpgChannelSchedule> Guide { get; set; } = [];

    public Task<EpgRefreshResult> RefreshAsync(
        string source,
        TimeSpan refreshInterval,
        CancellationToken cancellationToken = default)
    {
        EpgChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(EpgRefreshResult.Success(false, "Guide refreshed.",
            Guide.Sum(schedule => schedule.Programs.Count)));
    }

    public Task<IReadOnlyList<EpgChannelSchedule>> GetGuideAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default) => Task.FromResult(Guide);

    public Task<EpgNowNext> GetNowNextAsync(
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) => Task.FromResult(NowNext);
}
