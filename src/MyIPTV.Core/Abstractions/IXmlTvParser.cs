using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IXmlTvParser
{
    Task<EpgParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}
