# Logging and diagnostics

MyIPTV writes one structured JSON-lines log per UTC day to `%LocalAppData%\MyIPTV\Logs`. The Settings screen shows the exact location.

Logged events include application startup, profile connection outcomes, playlist and provider item-count summaries, playback failures, EPG refresh outcomes, database migration activity, and unexpected exceptions. Each record contains a UTC timestamp, severity, event identifier, category, message, and optional exception details.

## Redaction boundary

The file provider removes bearer credentials, sensitive assignments such as `password=`, `token=`, and `secret=`, and query strings from HTTP/HTTPS URLs before writing. The Xtream, Stalker/Ministra, IPTV, and EPG HTTP clients also disable framework HTTP logging because legacy authentication protocols can place secrets in request addresses.

Application code must never intentionally log:

- passwords, tokens, cookies, or authorization headers;
- full authentication or playback URLs;
- protected credential bytes or decrypted credential objects;
- response bodies that might contain provider account data.

Redaction is defense in depth, not permission to pass secrets to the logger. Logs remain local but may contain profile IDs, content IDs, item counts, file-system locations, and stack traces. Review a log before sharing it publicly.
