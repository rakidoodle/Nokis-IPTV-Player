using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IProfileService
{
    Task<IReadOnlyList<IptvProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ProfileDraft?> GetDraftAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<ProfileSaveResult> SaveAsync(ProfileDraft draft, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<ConnectionTestResult> TestConnectionAsync(ProfileDraft draft, CancellationToken cancellationToken = default);

    Task<ConnectionTestResult> ConnectAsync(ProfileDraft draft, CancellationToken cancellationToken = default);
}
