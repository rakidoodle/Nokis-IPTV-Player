using System.Text.RegularExpressions;

namespace MyIPTV.Infrastructure.Logging;

public static partial class LogSanitizer
{
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string sanitized = SensitiveHeaderPattern().Replace(value, "$1: [REDACTED]");
        sanitized = BearerPattern().Replace(sanitized, "Bearer [REDACTED]");
        sanitized = SensitiveAssignmentPattern().Replace(sanitized, match => $"{match.Groups[1].Value}=[REDACTED]");
        sanitized = UrlQueryPattern().Replace(sanitized, "$1?[REDACTED]");
        return sanitized;
    }

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"(?im)^(Authorization|Proxy-Authorization|Cookie|Set-Cookie|X-Api-Key|Api-Key)\s*:\s*[^\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveHeaderPattern();

    [GeneratedRegex(@"(?i)\b(password|passwd|pwd|token|access_token|session|secret|auth)\s*[=:]\s*([^\s,;]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentPattern();

    [GeneratedRegex(@"(?i)\b(https?://[^\s?]+)\?[^\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlQueryPattern();
}
