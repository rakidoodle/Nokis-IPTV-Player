using System.Net;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Providers.M3U;

public sealed partial class M3uPlaylistImportService(
    IHttpClientFactory httpClientFactory,
    IM3uPlaylistParser parser,
    IChannelCatalog channelCatalog,
    ILogger<M3uPlaylistImportService> logger) : IPlaylistImportService
{
    private readonly ILogger<M3uPlaylistImportService> _logger = logger;

    public async Task<PlaylistImportResult> ImportAsync(
        IptvProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.ConnectionType != ProfileConnectionType.M3uPlaylist)
        {
            return PlaylistImportResult.Failure("Only M3U profiles can be imported by this provider.");
        }

        LogImportStarted(profile.Id);
        try
        {
            PlaylistParseResult parsed = IsLocalPath(profile.ServerAddress)
                ? await ParseLocalAsync(profile, cancellationToken)
                : await ParseRemoteAsync(profile, cancellationToken);

            if (!parsed.IsValid)
            {
                LogImportCompleted(profile.Id, 0, succeeded: false);
                return PlaylistImportResult.Failure(parsed.Message);
            }

            if (parsed.Channels.Count == 0)
            {
                LogImportCompleted(profile.Id, 0, succeeded: false);
                return PlaylistImportResult.Failure("Playlist contains no playable channels.");
            }

            channelCatalog.ReplaceForProfile(profile.Id, parsed.Channels);
            string message = $"Imported {parsed.Channels.Count:N0} channels.";
            if (parsed.DuplicateEntries > 0 || parsed.SkippedEntries > 0)
            {
                message += $" Skipped {parsed.DuplicateEntries:N0} duplicates and {parsed.SkippedEntries:N0} malformed entries.";
            }

            LogImportCompleted(profile.Id, parsed.Channels.Count, succeeded: true);
            return PlaylistImportResult.Success(
                message,
                parsed.Channels.Count,
                parsed.SkippedEntries,
                parsed.DuplicateEntries);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogImportCompleted(profile.Id, 0, succeeded: false);
            return PlaylistImportResult.Failure("Playlist import timed out.");
        }
        catch (HttpRequestException)
        {
            LogImportCompleted(profile.Id, 0, succeeded: false);
            return PlaylistImportResult.Failure("Unable to reach the playlist server.");
        }
        catch (IOException)
        {
            LogImportCompleted(profile.Id, 0, succeeded: false);
            return PlaylistImportResult.Failure("The playlist could not be read.");
        }
    }

    private async Task<PlaylistParseResult> ParseLocalAsync(
        IptvProfile profile,
        CancellationToken cancellationToken)
    {
        string path = Uri.TryCreate(profile.ServerAddress, UriKind.Absolute, out Uri? uri) && uri.IsFile
            ? uri.LocalPath
            : profile.ServerAddress;
        if (!File.Exists(path))
        {
            return PlaylistParseResult.Invalid("Playlist file was not found.");
        }

        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16_384,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await parser.ParseAsync(stream, profile.Id, cancellationToken);
    }

    private async Task<PlaylistParseResult> ParseRemoteAsync(
        IptvProfile profile,
        CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("Iptv");
        using HttpRequestMessage request = new(HttpMethod.Get, profile.ServerAddress);
        request.Headers.UserAgent.ParseAdd("VLC/3.0.21 LibVLC/3.0.21");
        request.Headers.Accept.ParseAdd("application/x-mpegURL, application/vnd.apple.mpegurl, text/plain, */*");
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return PlaylistParseResult.Invalid("Authentication failed.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return PlaylistParseResult.Invalid("Playlist server returned an unexpected response.");
        }

        if (response.Content.Headers.ContentLength > M3uPlaylistParser.MaximumTextCharacters)
        {
            return PlaylistParseResult.Invalid("Playlist is too large to import safely.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await parser.ParseAsync(stream, profile.Id, cancellationToken);
    }

    private static bool IsLocalPath(string address) =>
        Path.IsPathFullyQualified(address) ||
        (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.IsFile);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Importing M3U playlist for profile {ProfileId}.")]
    private partial void LogImportStarted(Guid profileId);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Information,
        Message = "M3U import completed for profile {ProfileId}. Channels: {ChannelCount}. Success: {Succeeded}.")]
    private partial void LogImportCompleted(Guid profileId, int channelCount, bool succeeded);
}
