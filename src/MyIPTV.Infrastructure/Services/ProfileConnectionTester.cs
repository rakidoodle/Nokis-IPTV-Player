using System.Net;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Services;

public sealed partial class ProfileConnectionTester(
    IHttpClientFactory httpClientFactory,
    ILogger<ProfileConnectionTester> logger) : IProfileConnectionTester
{
    private readonly ILogger<ProfileConnectionTester> _logger = logger;

    public async Task<ConnectionTestResult> TestAsync(
        ProfileDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        LogConnectionTestStarted(draft.Id, draft.ConnectionType);

        try
        {
            ConnectionTestResult result = draft.ConnectionType == ProfileConnectionType.M3uPlaylist &&
                                          IsLocalPath(draft.ServerAddress)
                ? await TestLocalPlaylistAsync(draft.ServerAddress, cancellationToken)
                : await TestRemoteAsync(draft, cancellationToken);

            LogConnectionTestCompleted(draft.Id, draft.ConnectionType, result.IsSuccess);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogConnectionTestCompleted(draft.Id, draft.ConnectionType, succeeded: false);
            return ConnectionTestResult.Failure("Connection timed out.");
        }
        catch (HttpRequestException)
        {
            LogConnectionTestCompleted(draft.Id, draft.ConnectionType, succeeded: false);
            return ConnectionTestResult.Failure("Unable to reach server.");
        }
        catch (IOException)
        {
            LogConnectionTestCompleted(draft.Id, draft.ConnectionType, succeeded: false);
            return ConnectionTestResult.Failure("The playlist file could not be read.");
        }
    }

    private static async Task<ConnectionTestResult> TestLocalPlaylistAsync(
        string address,
        CancellationToken cancellationToken)
    {
        string path = Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.IsFile
            ? uri.LocalPath
            : address;

        if (!File.Exists(path))
        {
            return ConnectionTestResult.Failure("Playlist file was not found.");
        }

        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        using StreamReader reader = new(stream);
        string? firstLine = await reader.ReadLineAsync(cancellationToken);
        return IsM3uHeader(firstLine)
            ? ConnectionTestResult.Success("Local playlist is readable and has a valid M3U header.")
            : ConnectionTestResult.Failure("Playlist does not begin with a valid #EXTM3U header.");
    }

    private async Task<ConnectionTestResult> TestRemoteAsync(
        ProfileDraft draft,
        CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("Iptv");
        using HttpRequestMessage request = new(HttpMethod.Get, draft.ServerAddress);
        request.Headers.UserAgent.ParseAdd("MyIPTV/1.0");

        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ConnectionTestResult.Failure("Authentication failed.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return ConnectionTestResult.Failure("Server returned an unexpected response.");
        }

        if (draft.ConnectionType == ProfileConnectionType.M3uPlaylist)
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using StreamReader reader = new(stream);
            string? firstLine = await reader.ReadLineAsync(cancellationToken);
            return IsM3uHeader(firstLine)
                ? ConnectionTestResult.Success("Remote playlist is reachable and has a valid M3U header.")
                : ConnectionTestResult.Failure("Playlist contains no recognizable M3U header.");
        }

        return draft.ConnectionType == ProfileConnectionType.XtreamApi
            ? ConnectionTestResult.Success("Xtream server is reachable. Use Connect to authenticate and load its catalog.")
            : ConnectionTestResult.Success(
                "Server is reachable. Stalker/Ministra authentication will be validated when that provider module is enabled.");
    }

    private static bool IsLocalPath(string address) =>
        Path.IsPathFullyQualified(address) ||
        (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.IsFile);

    private static bool IsM3uHeader(string? firstLine) =>
        firstLine?.TrimStart('\uFEFF', ' ', '\t').StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase) == true;

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Testing connection for profile {ProfileId} of type {ConnectionType}.")]
    private partial void LogConnectionTestStarted(Guid? profileId, ProfileConnectionType connectionType);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Connection test completed for profile {ProfileId} of type {ConnectionType}. Success: {Succeeded}.")]
    private partial void LogConnectionTestCompleted(
        Guid? profileId,
        ProfileConnectionType connectionType,
        bool succeeded);
}
