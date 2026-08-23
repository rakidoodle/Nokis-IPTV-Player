using MyIPTV.Infrastructure.Services;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class DataMaintenanceServiceTests
{
    [TestMethod]
    public async Task ClearCacheRemovesOnlyCacheContents()
    {
        using TemporaryApplicationPaths paths = new();
        paths.EnsureDirectoriesExist();
        string nested = Path.Combine(paths.CacheDirectory, "posters");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(paths.CacheDirectory, "guide.tmp"), "cache");
        await File.WriteAllTextAsync(Path.Combine(nested, "poster.tmp"), "cache");
        await File.WriteAllTextAsync(paths.SettingsPath, "keep");

        await new DataMaintenanceService(paths).ClearCacheAsync();

        Assert.IsTrue(Directory.Exists(paths.CacheDirectory));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(paths.CacheDirectory));
        Assert.IsTrue(File.Exists(paths.SettingsPath));
    }
}
