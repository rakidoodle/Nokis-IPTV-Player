using MyIPTV.Core.Abstractions;

namespace MyIPTV.Tests.Fixtures;

internal sealed class TemporaryApplicationPaths : IApplicationPaths, IDisposable
{
    public TemporaryApplicationPaths()
    {
        DataDirectory = Path.Combine(
            Path.GetTempPath(),
            "MyIPTV.Tests",
            Guid.NewGuid().ToString("N"));
        DatabasePath = Path.Combine(DataDirectory, "test.db");
        SettingsPath = Path.Combine(DataDirectory, "settings.json");
        LogsDirectory = Path.Combine(DataDirectory, "Logs");
        CacheDirectory = Path.Combine(DataDirectory, "Cache");
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string SettingsPath { get; }

    public string LogsDirectory { get; }

    public string CacheDirectory { get; }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }

    public void Dispose()
    {
        string fullDataDirectory = Path.GetFullPath(DataDirectory);
        string fullTemporaryRoot = Path.GetFullPath(Path.GetTempPath());

        if (Directory.Exists(fullDataDirectory) &&
            fullDataDirectory.StartsWith(fullTemporaryRoot, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(fullDataDirectory, recursive: true);
        }
    }
}
