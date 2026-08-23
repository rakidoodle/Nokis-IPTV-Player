namespace MyIPTV.Core.Abstractions;

public interface IExceptionMessageService
{
    string GetUserMessage(Exception exception, string? fallback = null);
}
