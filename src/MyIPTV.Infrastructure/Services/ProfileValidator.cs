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

        if (ContainsDisallowedSensitiveQuery(uri.Query, draft.ConnectionType))
        {
            return ProfileValidationResult.Failure(
                "This URL appears to contain a password or token. Enter credentials in their dedicated fields instead.");
        }

        M3uAddressParts m3uParts = draft.ConnectionType == ProfileConnectionType.M3uPlaylist
            ? M3uCredentialUrl.Split(address)
            : new M3uAddressParts(address, null, false);
        if (m3uParts.HadCredentialParameters &&
            (m3uParts.Credentials is null || string.IsNullOrWhiteSpace(m3uParts.Credentials.Username)))
        {
            return ProfileValidationResult.Failure(
                "M3U links with credentials must include both a username and password.");
        }

        if (draft.ConnectionType is ProfileConnectionType.XtreamApi or ProfileConnectionType.StalkerPortal &&
            (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
        {
            return ProfileValidationResult.Failure("Server and portal URLs must not contain a query string or fragment.");
        }

        if (draft.ConnectionType is ProfileConnectionType.XtreamApi or ProfileConnectionType.StalkerPortal)
        {
            if (string.IsNullOrWhiteSpace(draft.Username))
            {
                return ProfileValidationResult.Failure(
                    draft.ConnectionType == ProfileConnectionType.XtreamApi
                        ? "Username is required for an Xtream profile."
                        : "Username is required for a supported Ministra REST profile.");
            }

            if (requireCredentials && string.IsNullOrEmpty(draft.Password))
            {
                return ProfileValidationResult.Failure(
                    draft.ConnectionType == ProfileConnectionType.XtreamApi
                        ? "Password is required for an Xtream profile."
                        : "Password is required for a supported Ministra REST profile.");
            }
        }

        return ProfileValidationResult.Success();
    }

    private static bool IsLocalPath(string address) =>
        Path.IsPathFullyQualified(address) ||
        (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.IsFile);

    private static bool ContainsDisallowedSensitiveQuery(
        string query,
        ProfileConnectionType connectionType)
    {
        if (string.IsNullOrEmpty(query))
        {
            return false;
        }

        foreach (string component in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string name = Uri.UnescapeDataString(component.Split('=', 2)[0]);
            bool isM3uAccountField = connectionType == ProfileConnectionType.M3uPlaylist &&
                                     (name.Equals("username", StringComparison.OrdinalIgnoreCase) ||
                                      name.Equals("user", StringComparison.OrdinalIgnoreCase) ||
                                      name.Equals("password", StringComparison.OrdinalIgnoreCase) ||
                                      name.Equals("pass", StringComparison.OrdinalIgnoreCase));
            if (!isM3uAccountField && SensitiveQueryNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

}
