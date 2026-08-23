using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Services;

namespace MyIPTV.Tests;

[TestClass]
public sealed class ProfileValidatorTests
{
    private readonly ProfileValidator _validator = new();

    [TestMethod]
    public void ValidateAcceptsM3uCredentialsEmbeddedInUrl()
    {
        ProfileDraft draft = new()
        {
            Name = "Unsafe",
            ConnectionType = ProfileConnectionType.M3uPlaylist,
            ServerAddress = "https://example.invalid/list.m3u?username=user&password=secret",
        };

        ProfileValidationResult result = _validator.Validate(draft, requireCredentials: false);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void ValidateStillRejectsM3uTokensEmbeddedInUrl()
    {
        ProfileValidationResult result = _validator.Validate(new()
        {
            Name = "Unsafe",
            ConnectionType = ProfileConnectionType.M3uPlaylist,
            ServerAddress = "https://example.invalid/list.m3u?token=synthetic-secret",
        }, requireCredentials: false);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Message, "password or token");
    }

    [TestMethod]
    public void ValidateRequiresXtreamCredentials()
    {
        ProfileDraft draft = new()
        {
            Name = "Xtream Demo",
            ConnectionType = ProfileConnectionType.XtreamApi,
            ServerAddress = "https://example.invalid",
            Username = "demo-user",
        };

        ProfileValidationResult result = _validator.Validate(draft, requireCredentials: true);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Password is required for an Xtream profile.", result.Message);
    }

    [TestMethod]
    public void ValidateRequiresValidStalkerMacAddress()
    {
        ProfileDraft draft = new()
        {
            Name = "Ministra Demo",
            ConnectionType = ProfileConnectionType.StalkerPortal,
            ServerAddress = "https://example.invalid/stalker_portal",
            Username = "demo-user",
        };

        ProfileValidationResult result = _validator.Validate(draft, requireCredentials: true);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Enter a MAC address in the format 00:1A:79:00:00:00.", result.Message);

        ProfileValidationResult valid = _validator.Validate(new ProfileDraft
        {
            Name = draft.Name,
            ConnectionType = draft.ConnectionType,
            ServerAddress = draft.ServerAddress,
            Username = "00:1A:79:12:34:56",
        }, requireCredentials: true);
        Assert.IsTrue(valid.IsValid);
    }

    [TestMethod]
    public void ValidateAcceptsAbsoluteLocalM3uPath()
    {
        ProfileDraft draft = new()
        {
            Name = "Local Demo",
            ConnectionType = ProfileConnectionType.M3uPlaylist,
            ServerAddress = Path.Combine(Path.GetTempPath(), "demo.m3u"),
        };

        ProfileValidationResult result = _validator.Validate(draft, requireCredentials: false);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    [DataRow("ftp://example.invalid/list.m3u")]
    [DataRow("javascript:alert(1)")]
    [DataRow("relative/list.m3u")]
    public void ValidateRejectsUnsupportedOrRelativeRemoteAddress(string address)
    {
        ProfileValidationResult result = _validator.Validate(new()
        {
            Name = "Unsafe address",
            ConnectionType = ProfileConnectionType.M3uPlaylist,
            ServerAddress = address,
        }, requireCredentials: false);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("HTTP/HTTPS", result.Message);
    }

    [TestMethod]
    public void ValidateRejectsUrlUserInfo()
    {
        ProfileValidationResult result = _validator.Validate(new()
        {
            Name = "Embedded account",
            ConnectionType = ProfileConnectionType.M3uPlaylist,
            ServerAddress = "https://demo-user:secret@example.invalid/list.m3u",
        }, requireCredentials: false);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("inside the URL", result.Message);
    }
}
