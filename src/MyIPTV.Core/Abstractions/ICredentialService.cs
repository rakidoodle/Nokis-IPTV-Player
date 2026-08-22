using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface ICredentialService
{
    Task StoreAsync(Guid profileId, ProfileCredentials credentials, CancellationToken cancellationToken = default);

    Task<ProfileCredentials?> RetrieveAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);
}
