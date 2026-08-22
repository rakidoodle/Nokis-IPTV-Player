namespace MyIPTV.Infrastructure.Providers.Xtream;

public enum XtreamClientError
{
    Authentication,
    UnexpectedResponse,
}

public sealed class XtreamClientException(
    XtreamClientError error,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public XtreamClientError Error { get; } = error;
}
