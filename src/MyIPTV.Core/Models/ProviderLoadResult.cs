namespace MyIPTV.Core.Models;

public sealed record ProviderLoadResult(
    bool IsSuccess,
    string Message,
    ProviderCatalog Catalog)
{
    public static ProviderLoadResult Failure(string message) =>
        new(false, message, ProviderCatalog.Empty);

    public static ProviderLoadResult Success(string message, ProviderCatalog catalog) =>
        new(true, message, catalog);
}
