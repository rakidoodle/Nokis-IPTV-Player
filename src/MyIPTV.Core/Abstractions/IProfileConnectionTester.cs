using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IProfileConnectionTester
{
    Task<ConnectionTestResult> TestAsync(ProfileDraft draft, CancellationToken cancellationToken = default);
}
