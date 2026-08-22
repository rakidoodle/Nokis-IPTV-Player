using MyIPTV.Infrastructure.Providers.Stalker.Dtos;

namespace MyIPTV.Infrastructure.Providers.Stalker;

public interface IStalkerClient
{
    Task<StalkerSession> AuthenticateAsync(
        string portalAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StalkerChannelDto>> GetLiveChannelsAsync(
        string portalAddress,
        StalkerSession session,
        CancellationToken cancellationToken = default);
}
