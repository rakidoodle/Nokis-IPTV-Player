using System.Net;
using MyIPTV.Infrastructure.Providers.Stalker;
using MyIPTV.Infrastructure.Providers.Stalker.Dtos;

namespace MyIPTV.Tests;

[TestClass]
public sealed class StalkerClientTests
{
    [TestMethod]
    public async Task AuthenticateAsyncUsesFormBodyAndNormalizesPortalAddress()
    {
        Uri? capturedUri = null;
        string? capturedBody = null;
        using HttpClient httpClient = new(new AsyncStubHandler(async (request, cancellationToken) =>
        {
            capturedUri = request.RequestUri;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("""
                {"access_token":"synthetic-token","user_id":42,"expires_in":"3600"}
                """);
        }));
        StalkerClient client = new(new StubHttpClientFactory(httpClient));

        StalkerSession session = await client.AuthenticateAsync(
            "https://example.invalid/stalker_portal/c/",
            "demo user",
            "synthetic/pass");

        Assert.AreEqual("42", session.UserId);
        Assert.AreEqual(3600, session.ExpiresInSeconds);
        Assert.IsNotNull(capturedUri);
        Assert.AreEqual("/stalker_portal/auth/token", capturedUri.AbsolutePath);
        Assert.AreEqual(string.Empty, capturedUri.Query);
        StringAssert.Contains(capturedBody, "username=demo+user");
        StringAssert.Contains(capturedBody, "password=synthetic%2Fpass");
        Assert.IsFalse(session.ToString().Contains("synthetic-token", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetLiveChannelsAsyncUsesBearerTokenAndReadsEnvelope()
    {
        string? authorization = null;
        Uri? capturedUri = null;
        using HttpClient httpClient = new(new AsyncStubHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(JsonResponse("""
                {"status":"OK","results":[
                  {"id":"7","name":"Demo News","cmd":"https://media.example.invalid/live/7.m3u8","genre_name":"News","xmltv_id":"demo.news"}
                ]}
                """));
        }));
        StalkerClient client = new(new StubHttpClientFactory(httpClient));

        IReadOnlyList<StalkerChannelDto> channels = await client.GetLiveChannelsAsync(
            "https://example.invalid/stalker_portal",
            new StalkerSession("synthetic-token", "42", 3600));

        Assert.AreEqual("Bearer synthetic-token", authorization);
        Assert.AreEqual("/stalker_portal/api/users/42/tv-channels", capturedUri?.AbsolutePath);
        Assert.AreEqual("Demo News", channels.Single().Name);
        Assert.AreEqual("News", channels.Single().Group);
    }

    [TestMethod]
    public async Task LegacyOnlyPortalReturnsClearUnsupportedErrorWithoutLeakingPassword()
    {
        const string secret = "synthetic-secret-never-log";
        using HttpClient httpClient = new(new AsyncStubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        StalkerClient client = new(new StubHttpClientFactory(httpClient));

        StalkerClientException exception = await Assert.ThrowsExactlyAsync<StalkerClientException>(
            async () => await client.AuthenticateAsync(
                "https://example.invalid/stalker_portal",
                "demo",
                secret));

        Assert.AreEqual(StalkerClientError.UnsupportedPortal, exception.Error);
        StringAssert.Contains(exception.Message, "MAC-based portal access is not supported");
        Assert.IsFalse(exception.ToString().Contains(secret, StringComparison.Ordinal));
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class AsyncStubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
