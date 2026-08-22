using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Providers;
using MyIPTV.Infrastructure.Providers.M3U;
using MyIPTV.Infrastructure.Providers.Stalker;
using MyIPTV.Infrastructure.Providers.Stalker.Dtos;

namespace MyIPTV.Tests;

[TestClass]
public sealed class StalkerProviderTests
{
    [TestMethod]
    public async Task LoadCatalogAsyncMapsOnlyPlayableAuthorizedRestChannels()
    {
        Guid profileId = Guid.NewGuid();
        InMemoryChannelCatalog channelCatalog = new();
        StalkerProvider provider = new(
            new StubCredentialService(),
            new StubStalkerClient(),
            channelCatalog,
            new InMemoryMediaCatalog(),
            NullLogger<StalkerProvider>.Instance);

        ProviderLoadResult result = await provider.LoadCatalogAsync(CreateProfile(profileId));

        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(1, result.Catalog.LiveChannels);
        IptvChannel channel = channelCatalog.GetForProfile(profileId).Single();
        Assert.AreEqual("Demo News", channel.Name);
        Assert.AreEqual("News", channel.Group);
        Assert.AreEqual("https://media.example.invalid/live/7.m3u8", channel.StreamUrl);
    }

    [TestMethod]
    public async Task UnsupportedPortalDoesNotChangeExistingCatalog()
    {
        Guid profileId = Guid.NewGuid();
        InMemoryChannelCatalog channelCatalog = new();
        channelCatalog.ReplaceForProfile(profileId,
        [
            new IptvChannel("existing", profileId, "Existing", "https://example.invalid/old.m3u8", null, "Old", null),
        ]);
        StalkerProvider provider = new(
            new StubCredentialService(),
            new StubStalkerClient(unsupported: true),
            channelCatalog,
            new InMemoryMediaCatalog(),
            NullLogger<StalkerProvider>.Instance);

        ProviderLoadResult result = await provider.LoadCatalogAsync(CreateProfile(profileId));

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.Message, "MAC-based portal access is not supported");
        Assert.AreEqual("Existing", channelCatalog.GetForProfile(profileId).Single().Name);
    }

    private static IptvProfile CreateProfile(Guid profileId) =>
        new(
            profileId,
            "Synthetic Ministra",
            ProfileConnectionType.StalkerPortal,
            "https://example.invalid/stalker_portal",
            "demo user",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class StubCredentialService : ICredentialService
    {
        public Task StoreAsync(Guid profileId, ProfileCredentials credentials, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ProfileCredentials?> RetrieveAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProfileCredentials?>(new ProfileCredentials("demo user", "synthetic/pass"));

        public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubStalkerClient(bool unsupported = false) : IStalkerClient
    {
        public Task<StalkerSession> AuthenticateAsync(
            string portalAddress,
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            if (unsupported)
            {
                throw new StalkerClientException(
                    StalkerClientError.UnsupportedPortal,
                    "This portal does not expose the supported username/password REST interface. Device-bound or MAC-based portal access is not supported.");
            }

            return Task.FromResult(new StalkerSession("synthetic-token", "42", 3600));
        }

        public Task<IReadOnlyList<StalkerChannelDto>> GetLiveChannelsAsync(
            string portalAddress,
            StalkerSession session,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StalkerChannelDto>>(
            [
                new("7", "Demo News", "https://media.example.invalid/live/7.m3u8", null, "News", "demo.news"),
                new("8", "Unsupported UDP", "udp://239.0.0.1:1234", null, "News", null),
                new("9", "Missing URL", null, null, "News", null),
            ]);
    }
}
