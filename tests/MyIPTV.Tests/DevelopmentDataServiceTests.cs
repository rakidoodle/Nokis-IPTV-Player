using Microsoft.Extensions.Logging.Abstractions;
using MyIPTV.Infrastructure.Data;
using MyIPTV.Infrastructure.Providers;
using MyIPTV.Infrastructure.Providers.M3U;
using MyIPTV.Infrastructure.Services;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class DevelopmentDataServiceTests
{
    [TestMethod]
    public async Task LoadsAndRemovesSafeNonFunctionalCatalog()
    {
        using TemporaryApplicationPaths paths = new();
        await new SqliteDatabaseService(paths, NullLogger<SqliteDatabaseService>.Instance).InitializeAsync();
        SqliteProfileRepository profiles = new(paths);
        InMemoryChannelCatalog channels = new();
        InMemoryMediaCatalog media = new();
        DevelopmentDataService service = new(profiles, channels, media);

        await service.LoadAsync();

        Assert.IsTrue(service.IsLoaded);
        Assert.HasCount(3, channels.GetForProfile(DevelopmentDataService.DemoProfileId));
        Assert.HasCount(2, media.GetMovies());
        Assert.HasCount(1, media.GetSeries());
        Assert.HasCount(2, media.GetEpisodes());
        Assert.IsTrue(channels.GetAll().All(item => new Uri(item.StreamUrl).Host == "example.invalid"));
        Assert.IsTrue(media.GetMovies().All(item => new Uri(item.StreamUrl).Host == "example.invalid"));
        Assert.IsNotNull(await profiles.GetByIdAsync(DevelopmentDataService.DemoProfileId));

        await service.RemoveAsync();

        Assert.IsFalse(service.IsLoaded);
        Assert.IsEmpty(channels.GetAll());
        Assert.IsEmpty(media.GetMovies());
        Assert.IsNull(await profiles.GetByIdAsync(DevelopmentDataService.DemoProfileId));
    }
}
