using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IProfileRepository
{
    Task<IReadOnlyList<IptvProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IptvProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpsertAsync(IptvProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
