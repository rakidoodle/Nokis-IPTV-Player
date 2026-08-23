using System.Net;
using System.Text.Json;
using MyIPTV.App.Services;

namespace MyIPTV.Tests;

[TestClass]
public sealed class ExceptionMessageServiceTests
{
    private readonly ExceptionMessageService _service = new();

    [TestMethod]
    public void NetworkFailureProducesFriendlyMessageWithoutSensitiveExceptionText()
    {
        HttpRequestException exception = new(
            "GET https://example.invalid/api?password=do-not-show failed",
            null,
            HttpStatusCode.BadGateway);

        string message = _service.GetUserMessage(exception);

        Assert.Contains("server", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("do-not-show", message);
        Assert.DoesNotContain("https://", message);
    }

    [TestMethod]
    public void MalformedJsonProducesDataMessage()
    {
        string message = _service.GetUserMessage(new JsonException("raw response"));

        Assert.Contains("could not understand", message);
        Assert.DoesNotContain("raw response", message);
    }

    [TestMethod]
    public void UnknownFailureUsesCallerFallback()
    {
        Assert.AreEqual("Please restart.",
            _service.GetUserMessage(new InvalidOperationException("technical"), "Please restart."));
    }
}
