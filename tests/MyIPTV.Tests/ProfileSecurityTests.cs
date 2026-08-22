using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Security;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class ProfileSecurityTests
{
    [TestMethod]
    public async Task WindowsCredentialServicePersistsOnlyProtectedCiphertext()
    {
        using TemporaryApplicationPaths paths = new();
        using WindowsCredentialService service = CreateService(paths);
        Guid profileId = Guid.NewGuid();
        const string secret = "synthetic-secret-value";
        ProfileCredentials credentials = new("demo-user", secret);

        await service.StoreAsync(profileId, credentials);
        string credentialPath = GetCredentialPath(paths, profileId);
        byte[] protectedBytes = await File.ReadAllBytesAsync(credentialPath);

        Assert.IsTrue(protectedBytes.Length > 0);
        string storedText = System.Text.Encoding.UTF8.GetString(protectedBytes);
        Assert.IsFalse(storedText.Contains(secret, StringComparison.Ordinal));
        Assert.IsFalse(storedText.Contains("demo-user", StringComparison.Ordinal));

        using WindowsCredentialService restartedService = CreateService(paths);
        ProfileCredentials? retrieved = await restartedService.RetrieveAsync(profileId);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(secret, retrieved.Password);
        Assert.AreEqual("demo-user", retrieved.Username);
        Assert.IsFalse(retrieved.ToString().Contains(secret, StringComparison.Ordinal));
        Assert.IsFalse(retrieved.ToString().Contains("demo-user", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task WindowsCredentialServiceDeleteRemovesProtectedFile()
    {
        using TemporaryApplicationPaths paths = new();
        using WindowsCredentialService service = CreateService(paths);
        Guid profileId = Guid.NewGuid();

        await service.StoreAsync(profileId, new ProfileCredentials("demo-user", "synthetic-secret"));
        await service.DeleteAsync(profileId);

        Assert.IsFalse(File.Exists(GetCredentialPath(paths, profileId)));
        Assert.IsNull(await service.RetrieveAsync(profileId));
    }

    [TestMethod]
    public async Task WindowsCredentialServiceReturnsNullForDamagedCiphertext()
    {
        using TemporaryApplicationPaths paths = new();
        using WindowsCredentialService service = CreateService(paths);
        Guid profileId = Guid.NewGuid();
        paths.EnsureDirectoriesExist();
        await File.WriteAllBytesAsync(GetCredentialPath(paths, profileId), [1, 2, 3, 4]);

        ProfileCredentials? retrieved = await service.RetrieveAsync(profileId);

        Assert.IsNull(retrieved);
    }

    [TestMethod]
    public void ProfileDraftStringRepresentationRedactsPassword()
    {
        const string secret = "never-print-this";
        ProfileDraft draft = new() { Password = secret };

        string representation = draft.ToString();

        Assert.IsFalse(representation.Contains(secret, StringComparison.Ordinal));
        StringAssert.Contains(representation, "[REDACTED]");
    }

    private static WindowsCredentialService CreateService(TemporaryApplicationPaths paths) =>
        new(paths, NullLogger<WindowsCredentialService>.Instance);

    private static string GetCredentialPath(TemporaryApplicationPaths paths, Guid profileId) =>
        Path.Combine(paths.CredentialsDirectory, profileId.ToString("N") + ".credential");
}
