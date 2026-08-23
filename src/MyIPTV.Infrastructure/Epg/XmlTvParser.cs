using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Epg;

public sealed partial class XmlTvParser : IXmlTvParser
{
    public async Task<EpgParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        XmlReaderSettings settings = new()
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            XmlResolver = null,
        };

        List<EpgChannel> channels = [];
        List<PendingProgram> pendingPrograms = [];
        int skipped = 0;
        using XmlReader reader = XmlReader.Create(stream, settings);
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.LocalName == "channel")
            {
                XElement element = await ReadElementAsync(reader, cancellationToken);
                string? id = TrimToNull(element.Attribute("id")?.Value);
                string? name = TrimToNull(element.Elements("display-name").FirstOrDefault()?.Value);
                if (id is not null)
                {
                    channels.Add(new(id, name ?? id));
                }
            }
            else if (reader.LocalName == "programme")
            {
                XElement element = await ReadElementAsync(reader, cancellationToken);
                if (TryMapProgram(element, out PendingProgram? program))
                {
                    pendingPrograms.Add(program!);
                }
                else
                {
                    skipped++;
                }
            }
        }

        EpgProgram[] programs = CompletePrograms(pendingPrograms, ref skipped);
        HashSet<string> declaredChannelIds = channels.Select(channel => channel.Id).ToHashSet(StringComparer.Ordinal);
        channels.AddRange(programs
            .Select(program => program.ChannelId)
            .Distinct(StringComparer.Ordinal)
            .Where(id => !declaredChannelIds.Contains(id))
            .Select(id => new EpgChannel(id, id)));
        return new(
            channels.DistinctBy(channel => channel.Id, StringComparer.Ordinal).ToArray(),
            programs,
            skipped);
    }

    private static async Task<XElement> ReadElementAsync(
        XmlReader reader,
        CancellationToken cancellationToken)
    {
        using XmlReader subtree = reader.ReadSubtree();
        await subtree.MoveToContentAsync();
        return await XElement.LoadAsync(subtree, LoadOptions.None, cancellationToken);
    }

    private static bool TryMapProgram(XElement element, out PendingProgram? program)
    {
        program = null;
        string? channelId = TrimToNull(element.Attribute("channel")?.Value);
        string? title = TrimToNull(element.Elements("title").FirstOrDefault()?.Value);
        if (channelId is null || title is null ||
            !TryParseTimestamp(element.Attribute("start")?.Value, out DateTimeOffset start))
        {
            return false;
        }

        DateTimeOffset? stop = TryParseTimestamp(element.Attribute("stop")?.Value, out DateTimeOffset parsedStop)
            ? parsedStop
            : null;
        string? description = TrimToNull(element.Elements("desc").FirstOrDefault()?.Value);
        program = new(channelId, start.ToUniversalTime(), stop?.ToUniversalTime(), title, Normalize(description));
        return true;
    }

    private static EpgProgram[] CompletePrograms(List<PendingProgram> pending, ref int skipped)
    {
        List<EpgProgram> completed = [];
        foreach (IGrouping<string, PendingProgram> group in pending
            .GroupBy(program => program.ChannelId, StringComparer.Ordinal))
        {
            PendingProgram[] ordered = group.OrderBy(program => program.StartUtc).ToArray();
            for (int index = 0; index < ordered.Length; index++)
            {
                PendingProgram program = ordered[index];
                DateTimeOffset end = program.EndUtc ??
                    (index + 1 < ordered.Length ? ordered[index + 1].StartUtc : program.StartUtc.AddMinutes(30));
                if (end <= program.StartUtc)
                {
                    skipped++;
                    continue;
                }

                completed.Add(new(
                    program.ChannelId,
                    program.StartUtc,
                    end,
                    program.Title,
                    program.Description));
            }
        }

        return completed.OrderBy(program => program.StartUtc).ThenBy(program => program.ChannelId).ToArray();
    }

    internal static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        string? text = TrimToNull(value);
        if (text is null)
        {
            return false;
        }

        string[] parts = WhitespaceRegex().Split(text);
        string digits = parts[0];
        if (digits.Length is < 8 or > 14 || digits.Length % 2 != 0 || !digits.All(char.IsDigit))
        {
            return false;
        }

        digits = digits.PadRight(14, '0');
        string zone = parts.Length > 1 ? NormalizeTimeZone(parts[1]) : "+00:00";
        if (zone.Length == 5 && (zone[0] == '+' || zone[0] == '-'))
        {
            zone = zone.Insert(3, ":");
        }

        return DateTimeOffset.TryParseExact(
            $"{digits} {zone}",
            "yyyyMMddHHmmss zzz",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out timestamp);
    }

    private static string NormalizeTimeZone(string zone) => zone.ToUpperInvariant() switch
    {
        "Z" or "UTC" or "GMT" => "+00:00",
        "BST" => "+01:00",
        _ => zone,
    };

    private static string? Normalize(string? value) => value is null
        ? null
        : WhitespaceRegex().Replace(value, " ").Trim();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record PendingProgram(
        string ChannelId,
        DateTimeOffset StartUtc,
        DateTimeOffset? EndUtc,
        string Title,
        string? Description);
}
