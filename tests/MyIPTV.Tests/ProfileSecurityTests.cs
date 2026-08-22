using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Security;

namespace MyIPTV.Tests;

[TestClass]
public sealed class ProfileSecurityTests
{
    [TestMethod]
    public async Task SessionCredentialServiceStoresCredentialsWithoutRevealingThem()
    {
        SessionCredentialService service = new();
        Guid profileId = Guid.NewGuid();
        const string secret = "synthetic-secret-value";
        ProfileCredentials credentials = new("demo-user", secret);

        await service.StoreAsync(profileId, credentials);
        ProfileCredentials? retrieved = await service.RetrieveAsync(profileId);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(secret, retrieved.Password);
        Assert.IsFalse(retrieved.ToString().Contains(secret, StringComparison.Ordinal));
        Assert.IsFalse(retrieved.ToString().Contains("demo-user", StringComparison.Ordinal));
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
}
