using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Infrastructure.Providers;
using MyIPTV.Infrastructure.Providers.M3U;
using MyIPTV.Infrastructure.Security;
using MyIPTV.Infrastructure.Services;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class ProfileServiceTests
{
    [TestMethod]
    public async Task SaveAsyncRejectsDuplicateNamesIgnoringCaseAndWhitespace()
    {
        using ServiceFixture fixture = await ServiceFixture.CreateAsync();
        ProfileSaveResult first = await fixture.Service.SaveAsync(Draft("Living Room"));
        ProfileSaveResult duplicate = await fixture.Service.SaveAsync(Draft("  living room  "));

        Assert.IsTrue(first.IsSuccess);
        Assert.IsFalse(duplicate.IsSuccess);
        Assert.AreEqual("A profile with this name already exists.", duplicate.Message);
        Assert.HasCount(1, await fixture.Service.GetAllAsync());
    }

    [TestMethod]
    public async Task M3uQueryCredentialsAreProtectedAndRestoredForEditing()
    {
        using ServiceFixture fixture = await ServiceFixture.CreateAsync();
        const string secret = "synthetic-secret";
        ProfileSaveResult saved = await fixture.Service.SaveAsync(new ProfileDraft
        {
            Name = "Remote M3U",
            ConnectionType = ProfileConnectionType.M3uPlaylist,
            ServerAddress = $"https://example.invalid/get.php?username=demo&password={secret}&type=m3u_plus",
        });

        Assert.IsTrue(saved.IsSuccess);
        Assert.IsNotNull(saved.Profile);
        Assert.IsFalse(saved.Profile.ServerAddress.Contains(secret, StringComparison.Ordinal));
        Assert.IsFalse(saved.Profile.ServerAddress.Contains("username=", StringComparison.OrdinalIgnoreCase));
        ProfileDraft? editor = await fixture.Service.GetDraftAsync(saved.Profile.Id);
        Assert.IsNotNull(editor);
        Assert.AreEqual(secret, editor.Password);
        Assert.AreEqual("demo", editor.Username);
        StringAssert.Contains(editor.ServerAddress, "password=synthetic-secret");
    }

    [TestMethod]
    public async Task M3uEscapedAmpersandsFromPastedLinksAreNormalized()
    {
        using ServiceFixture fixture = await ServiceFixture.CreateAsync();
        ProfileSaveResult saved = await fixture.Service.SaveAsync(new ProfileDraft
        {
            Name = "Pasted M3U",
            ConnectionType = ProfileConnectionType.M3uPlaylist,
            ServerAddress = "https://example.invalid/get.php?username=demo\\&password=secret&amp;type=m3u_plus",
        });

        Assert.IsTrue(saved.IsSuccess);
        Assert.IsNotNull(saved.Profile);
        ProfileDraft? editor = await fixture.Service.GetDraftAsync(saved.Profile.Id);
        Assert.IsNotNull(editor);
        StringAssert.Contains(editor.ServerAddress, "type=m3u_plus");
        Assert.IsFalse(editor.ServerAddress.Contains("\\&", StringComparison.Ordinal));
        Assert.IsFalse(editor.ServerAddress.Contains("&amp;", StringComparison.OrdinalIgnoreCase));
    }

    private static ProfileDraft Draft(string name) => new()
    {
        Name = name,
        ConnectionType = ProfileConnectionType.M3uPlaylist,
        ServerAddress = "https://example.invalid/list.m3u",
    };

    private sealed class ServiceFixture : IDisposable
    {
        private ServiceFixture(TemporaryApplicationPaths paths, WindowsCredentialService credentials)
        {
            Paths = paths;
            Credentials = credentials;
            Service = new ProfileService(
                new SqliteProfileRepository(paths),
                credentials,
                new ProfileValidator(),
                new SuccessfulConnectionTester(),
                [],
                new InMemoryChannelCatalog(),
                new InMemoryMediaCatalog(),
                new SqliteFavoriteRepository(paths),
                new SqliteWatchHistoryRepository(paths),
                new ActiveProfileService(),
                NullLogger<ProfileService>.Instance);
        }

        public TemporaryApplicationPaths Paths { get; }

        public WindowsCredentialService Credentials { get; }

        public ProfileService Service { get; }

        public static async Task<ServiceFixture> CreateAsync()
        {
            TemporaryApplicationPaths paths = new();
            paths.EnsureDirectoriesExist();
            SqliteDatabaseService database = new(paths, NullLogger<SqliteDatabaseService>.Instance);
            await database.InitializeAsync();
            return new ServiceFixture(
                paths,
                new WindowsCredentialService(paths, NullLogger<WindowsCredentialService>.Instance));
        }

        public void Dispose()
        {
            Credentials.Dispose();
            Paths.Dispose();
        }
    }

    private sealed class SuccessfulConnectionTester : IProfileConnectionTester
    {
        public Task<ConnectionTestResult> TestAsync(
            ProfileDraft draft,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ConnectionTestResult.Success("Connected."));
    }
}
