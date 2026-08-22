using MyIPTV.Infrastructure.Providers.Xtream.Dtos;

namespace MyIPTV.Infrastructure.Providers.Xtream;

public interface IXtreamClient
{
    Task<XtreamAuthenticationDto> AuthenticateAsync(
        string serverAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<XtreamCategoryDto>> GetCategoriesAsync(
        string serverAddress,
        string username,
        string password,
        string action,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<XtreamLiveStreamDto>> GetLiveStreamsAsync(
        string serverAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<XtreamVodStreamDto>> GetVodStreamsAsync(
        string serverAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<XtreamSeriesDto>> GetSeriesAsync(
        string serverAddress,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<XtreamSeriesInfoDto> GetSeriesInfoAsync(
        string serverAddress,
        string username,
        string password,
        string seriesId,
        CancellationToken cancellationToken = default);

    Task<XtreamEpgResponseDto> GetShortEpgAsync(
        string serverAddress,
        string username,
        string password,
        string streamId,
        int limit,
        CancellationToken cancellationToken = default);
}
