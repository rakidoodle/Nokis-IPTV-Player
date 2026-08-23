using Microsoft.Extensions.Logging;
using MyIPTV.Infrastructure.Logging;
using MyIPTV.Tests.Fixtures;

namespace MyIPTV.Tests;

[TestClass]
public sealed class LoggingTests
{
    [TestMethod]
    public void SanitizerRedactsAuthenticationMaterialAndUrlQueries()
    {
        string input = "Bearer abc.def password=hunter2 https://tv.example/player_api.php?username=alice&password=hunter2";

        string result = LogSanitizer.Sanitize(input);

        Assert.DoesNotContain("abc.def", result);
        Assert.DoesNotContain("hunter2", result);
        Assert.DoesNotContain("username=alice", result);
        Assert.Contains("[REDACTED]", result);
    }

    [TestMethod]
    public void FileProviderWritesStructuredSanitizedRecord()
    {
        using TemporaryApplicationPaths paths = new();
        using SanitizingFileLoggerProvider provider = new(paths);
        ILogger logger = provider.CreateLogger("MyIPTV.Tests");

        logger.Log(
            LogLevel.Information,
            new EventId(9001, "TestConnection"),
            "Connection failed at https://example.invalid/api?username=demo&password=do-not-write",
            null,
            static (state, _) => state);
        provider.Dispose();

        string log = File.ReadAllText(Directory.GetFiles(paths.LogsDirectory).Single());
        Assert.Contains("timestampUtc", log);
        Assert.Contains("Connection failed", log);
        Assert.DoesNotContain("do-not-write", log);
        Assert.DoesNotContain("username=demo", log);
    }
}
