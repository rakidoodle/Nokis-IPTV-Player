using MyIPTV.Core.Abstractions;

namespace MyIPTV.Infrastructure.Services;

public sealed class DataMaintenanceService(IApplicationPaths paths) : IDataMaintenanceService
{
    public Task ClearCacheAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureDirectoriesExist();
        string cacheRoot = Path.GetFullPath(paths.CacheDirectory)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (string file in Directory.EnumerateFiles(paths.CacheDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(ValidateChildPath(cacheRoot, file));
        }

        foreach (string directory in Directory.EnumerateDirectories(paths.CacheDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Delete(ValidateChildPath(cacheRoot, directory), recursive: true);
        }
    }, cancellationToken);

    private static string ValidateChildPath(string root, string candidate)
    {
        string resolved = Path.GetFullPath(candidate);
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The cache entry is outside the application cache directory.");
        }

        return resolved;
    }
}
