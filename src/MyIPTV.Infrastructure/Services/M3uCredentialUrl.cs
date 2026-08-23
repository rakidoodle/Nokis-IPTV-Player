using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Services;

internal static class M3uCredentialUrl
{
    private static readonly string[] UsernameNames = ["username", "user"];
    private static readonly string[] PasswordNames = ["password", "pass"];

    public static M3uAddressParts Split(string address)
    {
        address = Normalize(address);
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return new(address, null, false);
        }

        List<KeyValuePair<string, string>> retained = [];
        string? username = null;
        string? password = null;
        bool foundCredentials = false;
        foreach (string component in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = component.Split('=', 2);
            string name = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
            string value = pair.Length == 2
                ? Uri.UnescapeDataString(pair[1].Replace('+', ' '))
                : string.Empty;
            if (UsernameNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                username = value;
                foundCredentials = true;
            }
            else if (PasswordNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                password = value;
                foundCredentials = true;
            }
            else
            {
                retained.Add(new(name, value));
            }
        }

        UriBuilder builder = new(uri)
        {
            Query = string.Join('&', retained.Select(item => Pair(item.Key, item.Value))),
        };
        ProfileCredentials? credentials = foundCredentials && !string.IsNullOrEmpty(password)
            ? new ProfileCredentials(username, password)
            : null;
        return new(builder.Uri.AbsoluteUri, credentials, foundCredentials);
    }

    public static string Normalize(string address) =>
        address.Trim()
            .Trim('"', '\'', '<', '>')
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("\\&", "&", StringComparison.Ordinal);

    public static string Add(string address, ProfileCredentials credentials)
    {
        M3uAddressParts parts = Split(address);
        if (!Uri.TryCreate(parts.SanitizedAddress, UriKind.Absolute, out Uri? uri))
        {
            return address;
        }

        List<string> query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        if (!string.IsNullOrWhiteSpace(credentials.Username))
        {
            query.Add(Pair("username", credentials.Username));
        }

        query.Add(Pair("password", credentials.Password));
        UriBuilder builder = new(uri) { Query = string.Join('&', query) };
        return builder.Uri.AbsoluteUri;
    }

    private static string Pair(string name, string value) =>
        $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
}

internal sealed record M3uAddressParts(
    string SanitizedAddress,
    ProfileCredentials? Credentials,
    bool HadCredentialParameters);
