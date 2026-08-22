using Microsoft.Extensions.Options;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Configuration;

public sealed class ApplicationPaths : IApplicationPaths
{
    public ApplicationPaths(IOptions<ApplicationOptions> options)
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            options.Value.DataDirectoryName)
    {
    }

    internal ApplicationPaths(string localApplicationData, string directoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);

        if (!IsSafeDirectoryName(directoryName))
        {
            throw new ArgumentException("The application data directory name is invalid.", nameof(directoryName));
        }

        DataDirectory = Path.Combine(localApplicationData, directoryName);
        DatabasePath = Path.Combine(DataDirectory, "myiptv.db");
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

    private static bool IsSafeDirectoryName(string directoryName) =>
        !string.IsNullOrWhiteSpace(directoryName) &&
        !Path.IsPathRooted(directoryName) &&
        string.Equals(directoryName, Path.GetFileName(directoryName), StringComparison.Ordinal) &&
        directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
