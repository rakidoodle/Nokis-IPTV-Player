using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Providers.M3U;

public sealed partial class M3uPlaylistParser : IM3uPlaylistParser
{
    internal const int MaximumChannels = 100_000;
    internal const int MaximumLineLength = 32_768;
    internal const long MaximumTextCharacters = 64L * 1024 * 1024;

    private static readonly HashSet<string> SupportedStreamSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        "rtmp",
        "rtmps",
        "rtsp",
        "rtsps",
        "udp",
        "rtp",
        "srt",
    };

    public async Task<PlaylistParseResult> ParseAsync(
        Stream stream,
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The playlist stream must be readable.", nameof(stream));
        }

        cancellationToken.ThrowIfCancellationRequested();

        using StreamReader reader = new(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 16_384,
            leaveOpen: true);

        string? header = await reader.ReadLineAsync(cancellationToken);
        if (!IsM3uHeader(header))
        {
            return PlaylistParseResult.Invalid("Playlist does not begin with a valid #EXTM3U header.");
        }

        List<IptvChannel> channels = [];
        HashSet<string> streamUrls = new(StringComparer.Ordinal);
        PendingEntry? pendingEntry = null;
        int skippedEntries = 0;
        int duplicateEntries = 0;
        long totalCharacters = header?.Length ?? 0;

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalCharacters += line.Length;
            if (totalCharacters > MaximumTextCharacters)
            {
                return PlaylistParseResult.Invalid("Playlist is too large to import safely.");
            }

            if (line.Length > MaximumLineLength)
            {
                skippedEntries++;
                pendingEntry = null;
                continue;
            }

            string trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (trimmedLine.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                if (pendingEntry is not null)
                {
                    skippedEntries++;
                }

                pendingEntry = ParseMetadata(trimmedLine);
                continue;
            }

            if (trimmedLine.StartsWith('#'))
            {
                continue;
            }

            if (!IsSupportedStreamUrl(trimmedLine))
            {
                skippedEntries++;
                pendingEntry = null;
                continue;
            }

            if (!streamUrls.Add(trimmedLine))
            {
                duplicateEntries++;
                pendingEntry = null;
                continue;
            }

            if (channels.Count >= MaximumChannels)
            {
                return PlaylistParseResult.Invalid(
                    $"Playlist exceeds the supported limit of {MaximumChannels:N0} channels.");
            }

            int channelNumber = channels.Count + 1;
            string name = FirstNonEmpty(
                pendingEntry?.TvgName,
                pendingEntry?.DisplayName,
                $"Channel {channelNumber:N0}");
            string group = FirstNonEmpty(pendingEntry?.GroupTitle, "Uncategorized");
            string? epgId = NullIfWhiteSpace(pendingEntry?.TvgId);
            string? logoUrl = NormalizeOptionalValue(pendingEntry?.TvgLogo, 2_048);
            string id = CreateStableId(profileId, epgId, trimmedLine);

            channels.Add(new IptvChannel(
                id,
                profileId,
                Truncate(name, 512),
                trimmedLine,
                logoUrl,
                Truncate(group, 256),
                epgId is null ? null : Truncate(epgId, 512)));
            pendingEntry = null;
        }

        if (pendingEntry is not null)
        {
            skippedEntries++;
        }

        return PlaylistParseResult.Success(channels, skippedEntries, duplicateEntries);
    }

    private static PendingEntry ParseMetadata(string line)
    {
        string payload = line["#EXTINF:".Length..];
        int commaIndex = FindFirstUnquotedComma(payload);
        string attributes = commaIndex >= 0 ? payload[..commaIndex] : payload;
        string? displayName = commaIndex >= 0 ? NullIfWhiteSpace(payload[(commaIndex + 1)..]) : null;
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AttributePattern().Matches(attributes))
        {
            string value = match.Groups["quoted"].Success
                ? match.Groups["quoted"].Value
                : match.Groups["single"].Success
                    ? match.Groups["single"].Value
                    : match.Groups["bare"].Value;
            values[match.Groups["key"].Value] = value;
        }

        return new PendingEntry(
            GetValue(values, "tvg-id"),
            GetValue(values, "tvg-name"),
            GetValue(values, "tvg-logo"),
            GetValue(values, "group-title"),
            displayName);
    }

    private static int FindFirstUnquotedComma(string value)
    {
        char quote = '\0';
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (current is '\'' or '"')
            {
                quote = quote == '\0' ? current : quote == current ? '\0' : quote;
            }
            else if (current == ',' && quote == '\0')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsSupportedStreamUrl(string value)
    {
        string uriPart = value.Split('|', 2)[0];
        return value.Length <= 8_192 &&
               Uri.TryCreate(uriPart, UriKind.Absolute, out Uri? uri) &&
               SupportedStreamSchemes.Contains(uri.Scheme);
    }

    private static bool IsM3uHeader(string? value) =>
        value?.TrimStart('\uFEFF', ' ', '\t').StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase) == true;

    private static string CreateStableId(Guid profileId, string? epgId, string streamUrl)
    {
        byte[] input = Encoding.UTF8.GetBytes($"{profileId:N}\n{epgId}\n{streamUrl}");
        try
        {
            return Convert.ToHexString(SHA256.HashData(input));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    private static string? GetValue(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) ? NullIfWhiteSpace(value) : null;

    private static string FirstNonEmpty(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!.Trim();

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptionalValue(string? value, int maximumLength)
    {
        string? normalized = NullIfWhiteSpace(value);
        return normalized is null ? null : Truncate(normalized, maximumLength);
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    [GeneratedRegex(
        "(?<key>[A-Za-z0-9_-]+)\\s*=\\s*(?:\"(?<quoted>[^\"]*)\"|'(?<single>[^']*)'|(?<bare>[^\\s,]+))",
        RegexOptions.CultureInvariant)]
    private static partial Regex AttributePattern();

    private sealed record PendingEntry(
        string? TvgId,
        string? TvgName,
        string? TvgLogo,
        string? GroupTitle,
        string? DisplayName);
}
