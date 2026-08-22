namespace MyIPTV.Core.Abstractions;

public interface IDatabaseService
{
    string DatabasePath { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
}
