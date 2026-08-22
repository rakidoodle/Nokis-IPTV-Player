namespace MyIPTV.Core.Models;

public sealed record ProfileValidationResult(bool IsValid, string Message)
{
    public static ProfileValidationResult Success() => new(true, string.Empty);

    public static ProfileValidationResult Failure(string message) => new(false, message);
}
