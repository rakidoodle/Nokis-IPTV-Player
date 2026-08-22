namespace MyIPTV.Infrastructure.Providers.Stalker;

public enum StalkerClientError
{
    Authentication,
    UnsupportedPortal,
    UnexpectedResponse,
}

public sealed class StalkerClientException : Exception
{
    public StalkerClientException(StalkerClientError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public StalkerClientError Error { get; }
}
