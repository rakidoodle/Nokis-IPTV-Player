# Stalker / Ministra compatibility

Noki's IPTV Player supports authorized Stalker portals using the MAC address issued for the subscriber's own set-top-box profile. It performs the standard portal handshake, keeps the short-lived bearer token in memory, and reads the subscriber's live channel list. Compatible Ministra REST accounts from earlier versions remain supported internally.

## Supported portal behavior

- A portal root such as `https://provider.example/stalker_portal`
- The password-grant endpoint at `auth/token`
- The authenticated REST v2 live-channel resource at `api/users/{user-id}/tv-channels`
- Direct HTTP or HTTPS channel URLs returned for the authenticated user

Portal URLs ending in `/c`, `/c/index.html`, `/portal.php`, or `/server/load.php` are normalized to the portal root before the REST endpoints are addressed.

## Deliberate limitations

Only use a MAC address assigned to an account or device you are authorized to access. The app does not discover, invent, harvest, or bypass provider identities, subscriptions, TLS, or DRM.

Some Ministra deployments put the REST API on a separate operator-configured virtual host or require a licensed official player. Those configurations cannot be inferred safely from a normal portal URL. Ask the service operator for a supported REST endpoint, an M3U playlist, or Xtream-compatible access instead.

## Transport security

HTTPS is strongly recommended. HTTP remains accepted for authorized local or legacy deployments, but credentials and bearer tokens are not protected from interception by the network when HTTP is used. Certificate validation is never disabled.

## References

- [Infomir: portal access by login and password](https://wiki.infomir.eu/eng/ministra-tv-platform/ministra-installation-guide/faq/how-to-organize-the-access-to-the-portal-by-login-and-password)
- [Infomir: Ministra configuration and API security settings](https://wiki.infomir.eu/eng/ministra-tv-platform/ministra-installation-guide/configuration-file)
- [Stalker Middleware developer discussion: REST v2 password authentication and user channel resource](https://groups.google.com/g/stalker-middleware/c/tgmDnB66FC4)
