using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Configuration;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class SettingsServiceTests
{
    [TestMethod]
    public async Task SaveAndLoadAsyncRoundTripsNonSensitivePreferences()
    {
        using TemporaryApplicationPaths paths = new();
        JsonSettingsService service = new(paths, NullLogger<JsonSettingsService>.Instance);
        AppSettings expected = new()
        {
            Theme = "Dark",
            StartPage = "LiveTv",
            RememberLastProfile = false,
            DefaultVolume = 65,
            Language = "en-US",
            HardwareDecoding = false,
            AspectRatio = "16:9",
            ReconnectOnFailure = false,
            EpgSource = "C:\\Guide\\demo.xml",
            EpgRefreshHours = 12,
            EpgTimezoneBehavior = "UTC",
        };

        await service.SaveAsync(expected);
        AppSettings actual = await service.LoadAsync();

        Assert.AreEqual(expected, actual);
        string serializedSettings = await File.ReadAllTextAsync(paths.SettingsPath);
        Assert.IsFalse(serializedSettings.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task LoadAsyncReturnsDefaultsForMalformedJson()
    {
        using TemporaryApplicationPaths paths = new();
        paths.EnsureDirectoriesExist();
        await File.WriteAllTextAsync(paths.SettingsPath, "{ malformed");
        JsonSettingsService service = new(paths, NullLogger<JsonSettingsService>.Instance);

        AppSettings settings = await service.LoadAsync();

        Assert.AreEqual(new AppSettings(), settings);
    }

    [TestMethod]
    public async Task SaveAsyncRejectsVolumeOutsideValidRange()
    {
        using TemporaryApplicationPaths paths = new();
        JsonSettingsService service = new(paths, NullLogger<JsonSettingsService>.Instance);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => service.SaveAsync(new AppSettings { DefaultVolume = 101 }));
    }

    [TestMethod]
    public async Task SaveAsyncRejectsInvalidEpgRefreshInterval()
    {
        using TemporaryApplicationPaths paths = new();
        JsonSettingsService service = new(paths, NullLogger<JsonSettingsService>.Instance);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => service.SaveAsync(new AppSettings { EpgRefreshHours = 0 }));
    }
}
