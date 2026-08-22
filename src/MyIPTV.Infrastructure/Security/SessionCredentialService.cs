using System.Collections.Concurrent;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Security;

public sealed class SessionCredentialService : ICredentialService
{
    private readonly ConcurrentDictionary<Guid, ProfileCredentials> _credentials = new();

    public Task StoreAsync(
        Guid profileId,
        ProfileCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(credentials);
        _credentials[profileId] = credentials;
        return Task.CompletedTask;
    }

    public Task<ProfileCredentials?> RetrieveAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _credentials.TryGetValue(profileId, out ProfileCredentials? credentials);
        return Task.FromResult(credentials);
    }

    public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _credentials.TryRemove(profileId, out _);
        return Task.CompletedTask;
    }
}
