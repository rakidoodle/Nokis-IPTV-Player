# Stalker / Ministra compatibility

MyIPTV supports the documented subscriber REST flow that authenticates with a normal username and password, receives a short-lived OAuth bearer token, and reads the authenticated user's live channel list. Session tokens remain in memory and are redacted from object text and logs. The dedicated HTTP client also disables framework request logging because authorization headers are sensitive.

## Supported portal behavior

- A portal root such as `https://provider.example/stalker_portal`
- The password-grant endpoint at `auth/token`
- The authenticated REST v2 live-channel resource at `api/users/{user-id}/tv-channels`
- Direct HTTP or HTTPS channel URLs returned for the authenticated user

Portal URLs ending in `/c`, `/c/index.html`, `/portal.php`, or `/server/load.php` are normalized to the portal root before the REST endpoints are addressed.

## Deliberate limitations

The legacy STB interface is device-bound and commonly requires a MAC address, set-top-box model, serial number, or device-specific client identity. MyIPTV does not emulate a MAG device, invent or harvest MAC addresses, copy an existing device identity, or call the legacy handshake interface. A portal that exposes only that interface receives a clear unsupported-portal diagnostic and its existing local catalog is left untouched.

Some Ministra deployments put the REST API on a separate operator-configured virtual host or require a licensed official player. Those configurations cannot be inferred safely from a normal portal URL. Ask the service operator for a supported REST endpoint, an M3U playlist, or Xtream-compatible access instead.

## Transport security

HTTPS is strongly recommended. HTTP remains accepted for authorized local or legacy deployments, but credentials and bearer tokens are not protected from interception by the network when HTTP is used. Certificate validation is never disabled.

## References

- [Infomir: portal access by login and password](https://wiki.infomir.eu/eng/ministra-tv-platform/ministra-installation-guide/faq/how-to-organize-the-access-to-the-portal-by-login-and-password)
- [Infomir: Ministra configuration and API security settings](https://wiki.infomir.eu/eng/ministra-tv-platform/ministra-installation-guide/configuration-file)
- [Stalker Middleware developer discussion: REST v2 password authentication and user channel resource](https://groups.google.com/g/stalker-middleware/c/tgmDnB66FC4)
