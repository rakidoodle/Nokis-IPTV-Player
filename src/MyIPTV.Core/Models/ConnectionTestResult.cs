namespace MyIPTV.Core.Models;

public sealed record ConnectionTestResult(bool IsSuccess, string Message)
{
    public static ConnectionTestResult Success(string message) => new(true, message);

    public static ConnectionTestResult Failure(string message) => new(false, message);
}
