using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Services;

public sealed class ProfileValidator : IProfileValidator
{
    private static readonly string[] SensitiveQueryNames =
    [
        "username",
        "user",
        "password",
        "pass",
        "token",
        "auth",
        "key",
    ];

    public ProfileValidationResult Validate(ProfileDraft draft, bool requireCredentials)
    {
        ArgumentNullException.ThrowIfNull(draft);

        string name = draft.Name.Trim();
        if (name.Length is < 1 or > 100)
        {
            return ProfileValidationResult.Failure("Profile name must be between 1 and 100 characters.");
        }

        string address = draft.ServerAddress.Trim();
        if (address.Length is < 1 or > 2048)
        {
            return ProfileValidationResult.Failure("Enter a valid server URL or local playlist path.");
        }

        if (draft.ConnectionType == ProfileConnectionType.M3uPlaylist && IsLocalPath(address))
        {
            string extension = Path.GetExtension(address);
            return extension.Equals(".m3u", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
                ? ProfileValidationResult.Success()
                : ProfileValidationResult.Failure("Local playlists must use the .m3u or .m3u8 extension.");
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return ProfileValidationResult.Failure("Only HTTP/HTTPS URLs are supported for remote profiles.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return ProfileValidationResult.Failure("Do not place usernames or passwords inside the URL.");
        }

        if (ContainsSensitiveQuery(uri.Query))
        {
            return ProfileValidationResult.Failure(
                "This URL appears to contain a password or token. Enter credentials in their dedicated fields instead.");
        }

        if (draft.ConnectionType is ProfileConnectionType.XtreamApi or ProfileConnectionType.StalkerPortal &&
            (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
        {
            return ProfileValidationResult.Failure("Server and portal URLs must not contain a query string or fragment.");
        }

        if (draft.ConnectionType == ProfileConnectionType.XtreamApi)
        {
            if (string.IsNullOrWhiteSpace(draft.Username))
            {
                return ProfileValidationResult.Failure("Username is required for an Xtream profile.");
            }

            if (requireCredentials && string.IsNullOrEmpty(draft.Password))
            {
                return ProfileValidationResult.Failure("Password is required for an Xtream profile.");
            }
        }

        return ProfileValidationResult.Success();
    }

    private static bool IsLocalPath(string address) =>
        Path.IsPathFullyQualified(address) ||
        (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.IsFile);

    private static bool ContainsSensitiveQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return false;
        }

        foreach (string component in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string name = Uri.UnescapeDataString(component.Split('=', 2)[0]);
            if (SensitiveQueryNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
