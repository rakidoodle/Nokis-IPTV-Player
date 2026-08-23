using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Configuration;

public sealed partial class JsonSettingsService(
    IApplicationPaths paths,
    ILogger<JsonSettingsService> logger) : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly ILogger<JsonSettingsService> _logger = logger;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectoriesExist();

        if (!File.Exists(paths.SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            await using FileStream stream = new(
                paths.SettingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            return await JsonSerializer.DeserializeAsync<AppSettings>(
                       stream,
                       SerializerOptions,
                       cancellationToken)
                   ?? new AppSettings();
        }
        catch (JsonException exception)
        {
            LogMalformedSettings(exception);
            return new AppSettings();
        }
        catch (IOException exception)
        {
            LogUnreadableSettings(exception);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.DefaultVolume is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.DefaultVolume,
                "Default volume must be between 0 and 100.");
        }

        if (settings.EpgRefreshHours is < 1 or > 168)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings), settings.EpgRefreshHours,
                "EPG refresh interval must be between 1 and 168 hours.");
        }

        paths.EnsureDirectoriesExist();
        string temporaryPath = paths.SettingsPath + ".tmp";

        try
        {
            await using (FileStream stream = new(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, paths.SettingsPath, overwrite: true);
            LogSettingsSaved();
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Settings file is malformed; defaults will be used.")]
    private partial void LogMalformedSettings(Exception exception);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Settings file could not be read; defaults will be used.")]
    private partial void LogUnreadableSettings(Exception exception);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Application settings saved.")]
    private partial void LogSettingsSaved();
}
