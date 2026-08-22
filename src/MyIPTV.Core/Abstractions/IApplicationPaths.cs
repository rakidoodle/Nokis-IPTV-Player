namespace MyIPTV.Core.Abstractions;

public interface IApplicationPaths
{
    string DataDirectory { get; }

    string DatabasePath { get; }

    string SettingsPath { get; }

    string LogsDirectory { get; }

    string CacheDirectory { get; }

    void EnsureDirectoriesExist();
}
