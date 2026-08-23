# Security review

Phase 27 reviewed credential storage, logs, SQLite data, HTTP clients, TLS behavior, temporary files, and user-facing failures.

## Controls in place

- Passwords are stored outside SQLite in per-profile files protected by Windows DPAPI for the current Windows user. Plaintext and protected byte buffers are cleared after use.
- Logs pass through a sanitizer that redacts bearer credentials, password/token assignments, credential-bearing URL queries, authorization headers, cookies, and API-key headers.
- Provider HTTP logging is disabled so framework diagnostics cannot record full request URLs containing credentials.
- The database stores profile metadata and catalog metadata, but not provider passwords, session tokens, or playable URLs.
- Standard .NET certificate validation remains enabled. The application has no certificate-bypass callback and never silently accepts an invalid certificate.
- Network requests have a bounded timeout, support cancellation, stream large responses, and enforce 64 MiB response limits for provider JSON and decompressed EPG data.
- Credential writes use a temporary file followed by an atomic replacement. Disposable EPG/cache files remain inside the application data directory.
- Centralized exception handling shows generic, safe messages while technical details go through the sanitizer.

## Limitations and recommendations

Some legitimate legacy IPTV providers expose only HTTP. MyIPTV allows those addresses for compatibility, but HTTP does not protect traffic or credentials in transit. Prefer HTTPS services with valid certificates whenever available. Anyone with access to the same unlocked Windows account can run software as that user; DPAPI is not a defense against a fully compromised account.

Stalker/Ministra support is limited to normal bearer-session authentication supplied by an authorized portal. MyIPTV does not spoof device identities, bypass subscriptions, disable TLS validation, or circumvent DRM.

Dependency vulnerability status is checked with NuGet's transitive vulnerability audit as part of release acceptance.
