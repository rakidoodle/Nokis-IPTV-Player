using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;
using MyIPTV.Infrastructure.Networking;

namespace MyIPTV.Infrastructure.Epg;

public sealed partial class EpgService(
    IHttpClientFactory httpClientFactory,
    IXmlTvParser parser,
    IEpgRepository repository,
    ILogger<EpgService> logger) : IEpgService
{
    private const long MaximumDecompressedBytes = 64L * 1024L * 1024L;
    private readonly ILogger<EpgService> _logger = logger;
    private string? _activeSourceKey;

    public event EventHandler? EpgChanged;

    public async Task<EpgRefreshResult> RefreshAsync(
        string source,
        TimeSpan refreshInterval,
        CancellationToken cancellationToken = default)
    {
        string? normalizedSource = NormalizeSource(source);
        if (normalizedSource is null)
        {
            return EpgRefreshResult.Failure("Enter an HTTP/HTTPS XMLTV address or an existing local XMLTV file.");
        }

        string sourceKey = CreateSourceKey(normalizedSource);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan interval = TimeSpan.FromMinutes(Math.Clamp(refreshInterval.TotalMinutes, 5, 10_080));
        EpgCacheState? cached = await repository.GetCacheStateAsync(sourceKey, cancellationToken);
        if (cached is not null && cached.ExpiresUtc > now)
        {
            Activate(sourceKey);
            return EpgRefreshResult.Success(true, "EPG cache is still current.", cached.ProgramCount);
        }

        try
        {
            return File.Exists(normalizedSource)
                ? await RefreshFileAsync(normalizedSource, sourceKey, now, interval, cancellationToken)
                : await RefreshHttpAsync(normalizedSource, sourceKey, cached, now, interval, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or System.Xml.XmlException)
        {
            LogRefreshFailed(sourceKey, exception);
            if (cached is not null && cached.ProgramCount > 0)
            {
                Activate(sourceKey);
                return EpgRefreshResult.Failure(
                    "The EPG source could not be refreshed. Cached guide data remains available.",
                    cached.ProgramCount);
            }

            return EpgRefreshResult.Failure("The EPG source could not be loaded or is not valid XMLTV data.");
        }
    }

    public Task<IReadOnlyList<EpgChannelSchedule>> GetGuideAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default)
    {
        string? sourceKey = Volatile.Read(ref _activeSourceKey);
        return sourceKey is null || endUtc <= startUtc
            ? Task.FromResult<IReadOnlyList<EpgChannelSchedule>>([])
            : repository.GetGuideAsync(sourceKey, startUtc.ToUniversalTime(), endUtc.ToUniversalTime(), cancellationToken);
    }

    public Task<EpgNowNext> GetNowNextAsync(
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        string? sourceKey = Volatile.Read(ref _activeSourceKey);
        return sourceKey is null || string.IsNullOrWhiteSpace(channelId)
            ? Task.FromResult(new EpgNowNext(null, null))
            : repository.GetNowNextAsync(sourceKey, channelId, nowUtc.ToUniversalTime(), cancellationToken);
    }

    private async Task<EpgRefreshResult> RefreshFileAsync(
        string path,
        string sourceKey,
        DateTimeOffset now,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        await using FileStream file = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using Stream content = WrapContent(file, path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase));
        return await ParseAndStoreAsync(content, sourceKey, now, interval, null,
            File.GetLastWriteTimeUtc(path), cancellationToken);
    }

    private async Task<EpgRefreshResult> RefreshHttpAsync(
        string address,
        string sourceKey,
        EpgCacheState? cached,
        DateTimeOffset now,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, address);
        if (cached?.EntityTag is not null && EntityTagHeaderValue.TryParse(cached.EntityTag, out EntityTagHeaderValue? tag))
        {
            request.Headers.IfNoneMatch.Add(tag);
        }
        if (cached?.LastModifiedUtc is not null)
        {
            request.Headers.IfModifiedSince = cached.LastModifiedUtc;
        }

        HttpClient client = httpClientFactory.CreateClient("Epg");
        using HttpResponseMessage response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
        {
            await repository.TouchAsync(sourceKey, now, now.Add(interval), cancellationToken);
            Activate(sourceKey);
            return EpgRefreshResult.Success(true, "EPG cache was validated with the source.", cached.ProgramCount);
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumDecompressedBytes)
        {
            throw new InvalidDataException("The EPG response is too large to process safely.");
        }

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        bool isGzip = response.Content.Headers.ContentEncoding.Any(value =>
            value.Equals("gzip", StringComparison.OrdinalIgnoreCase)) ||
            address.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        await using Stream content = WrapContent(responseStream, isGzip);
        return await ParseAndStoreAsync(
            content,
            sourceKey,
            now,
            interval,
            response.Headers.ETag?.ToString(),
            response.Content.Headers.LastModified,
            cancellationToken);
    }

    private async Task<EpgRefreshResult> ParseAndStoreAsync(
        Stream stream,
        string sourceKey,
        DateTimeOffset now,
        TimeSpan interval,
        string? entityTag,
        DateTimeOffset? lastModified,
        CancellationToken cancellationToken)
    {
        await using SizeLimitedReadStream limited = new(stream, MaximumDecompressedBytes);
        EpgParseResult parsed = await parser.ParseAsync(limited, cancellationToken);
        if (parsed.Programs.Count == 0)
        {
            throw new InvalidDataException("The XMLTV source did not contain usable programs.");
        }

        EpgCacheState state = new(
            sourceKey, now, now.Add(interval), entityTag, lastModified, parsed.Programs.Count);
        await repository.ReplaceAsync(sourceKey, parsed, state, cancellationToken);
        LogRefreshCompleted(sourceKey, parsed.Channels.Count, parsed.Programs.Count, parsed.SkippedPrograms);
        Activate(sourceKey);
        return EpgRefreshResult.Success(
            false,
            $"Loaded {parsed.Programs.Count:N0} programs across {parsed.Channels.Count:N0} channels.",
            parsed.Programs.Count);
    }

    private void Activate(string sourceKey)
    {
        Volatile.Write(ref _activeSourceKey, sourceKey);
        EpgChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Stream WrapContent(Stream stream, bool isGzip) =>
        isGzip ? new GZipStream(stream, CompressionMode.Decompress, leaveOpen: false) : stream;

    private static string? NormalizeSource(string source)
    {
        string value = source?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return null;
        }

        if (File.Exists(value))
        {
            return Path.GetFullPath(value);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
               uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.AbsoluteUri
            : null;
    }

    private static string CreateSourceKey(string source) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

    [LoggerMessage(
        EventId = 7201,
        Level = LogLevel.Information,
        Message = "EPG source {SourceKey} refreshed with {ChannelCount} channels, {ProgramCount} programs, and {SkippedCount} skipped programs.")]
    private partial void LogRefreshCompleted(string sourceKey, int channelCount, int programCount, int skippedCount);

    [LoggerMessage(EventId = 7202, Level = LogLevel.Warning, Message = "EPG source {SourceKey} refresh failed.")]
    private partial void LogRefreshFailed(string sourceKey, Exception exception);
}
