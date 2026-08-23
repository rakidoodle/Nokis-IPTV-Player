namespace MyIPTV.Core.Abstractions;

public interface IDevelopmentDataService
{
    bool IsLoaded { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task RemoveAsync(CancellationToken cancellationToken = default);
}
