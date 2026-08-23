namespace MyIPTV.Core.Abstractions;

public interface IDataMaintenanceService
{
    Task ClearCacheAsync(CancellationToken cancellationToken = default);
}
