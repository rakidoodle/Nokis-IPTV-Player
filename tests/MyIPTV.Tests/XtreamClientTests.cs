using System.Net;
using MyIPTV.Infrastructure.Providers.Xtream;
using MyIPTV.Infrastructure.Providers.Xtream.Dtos;

namespace MyIPTV.Tests;

[TestClass]
public sealed class XtreamClientTests
{
    [TestMethod]
    public async Task AuthenticateAsyncEncodesCredentialsAndReadsResponse()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = new(new DelegatingStubHandler(request =>
        {
            capturedUri = request.RequestUri;
            return JsonResponse("""
                {"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts","m3u8"]}}
                """);
        }));
        XtreamClient client = new(new StubHttpClientFactory(httpClient));

        XtreamAuthenticationDto result = await client.AuthenticateAsync(
            "https://example.invalid:8443/service",
            "demo user",
            "synthetic/pass");

        Assert.IsNotNull(result.UserInfo);
        Assert.AreEqual("Active", result.UserInfo.Status);
        Assert.IsNotNull(capturedUri);
        Assert.AreEqual("/service/player_api.php", capturedUri.AbsolutePath);
        StringAssert.Contains(capturedUri.Query, "username=demo%20user");
        StringAssert.Contains(capturedUri.Query, "password=synthetic%2Fpass");
    }

    [TestMethod]
    public async Task AuthenticateAsyncAcceptsSingleOutputFormatString()
    {
        using HttpClient httpClient = new(new DelegatingStubHandler(_ => JsonResponse("""
            {"user_info":{"auth":"1","status":"Active","allowed_output_formats":"m3u8"}}
            """)));
        XtreamClient client = new(new StubHttpClientFactory(httpClient));

        XtreamAuthenticationDto result = await client.AuthenticateAsync(
            "https://example.invalid",
            "user",
            "password");

        Assert.IsNotNull(result.UserInfo?.AllowedOutputFormats);
        Assert.AreEqual("m3u8", result.UserInfo.AllowedOutputFormats.Single());
    }

    [TestMethod]
    public async Task GetLiveStreamsAsyncReadsStringEncodedNumbers()
    {
        using HttpClient httpClient = new(new DelegatingStubHandler(_ => JsonResponse("""
            [{"stream_id":"42","name":"Demo News","category_id":"7"}]
            """)));
        XtreamClient client = new(new StubHttpClientFactory(httpClient));

        IReadOnlyList<XtreamLiveStreamDto> streams = await client.GetLiveStreamsAsync(
            "https://example.invalid",
            "user",
            "password");

        Assert.AreEqual(42, streams.Single().StreamId);
    }

    [TestMethod]
    public async Task GetLiveStreamsAsyncAcceptsNumericStringFieldsAndKeyedObjects()
    {
        using HttpClient httpClient = new(new DelegatingStubHandler(_ => JsonResponse("""
            {"42":{"stream_id":"42","name":9001,"category_id":7}}
            """)));
        XtreamClient client = new(new StubHttpClientFactory(httpClient));

        IReadOnlyList<XtreamLiveStreamDto> streams = await client.GetLiveStreamsAsync(
            "https://example.invalid",
            "user",
            "password");

        Assert.AreEqual("9001", streams.Single().Name);
        Assert.AreEqual("7", streams.Single().CategoryId);
    }

    [TestMethod]
    public async Task EmptyObjectCatalogResponseIsTreatedAsAnEmptyList()
    {
        using HttpClient httpClient = new(new DelegatingStubHandler(_ => JsonResponse("{}")));
        XtreamClient client = new(new StubHttpClientFactory(httpClient));

        IReadOnlyList<XtreamLiveStreamDto> streams = await client.GetLiveStreamsAsync(
            "https://example.invalid",
            "user",
            "password");

        Assert.IsEmpty(streams);
    }

    [TestMethod]
    public async Task MalformedResponseReturnsSanitizedClientException()
    {
        const string secret = "synthetic-secret-never-log";
        using HttpClient httpClient = new(new DelegatingStubHandler(_ => JsonResponse("not-json")));
        XtreamClient client = new(new StubHttpClientFactory(httpClient));

        XtreamClientException exception = await Assert.ThrowsExactlyAsync<XtreamClientException>(
            async () => await client.AuthenticateAsync("https://example.invalid", "user", secret));

        Assert.AreEqual(XtreamClientError.UnexpectedResponse, exception.Error);
        Assert.IsFalse(exception.ToString().Contains(secret, StringComparison.Ordinal));
        StringAssert.Contains(exception.Message, "malformed data");
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class DelegatingStubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
