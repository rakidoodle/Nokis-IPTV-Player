# Xtream profiles

MyIPTV supports normal authenticated access to an Xtream-compatible service that you are authorized to use.

## Supported operations

Connecting an Xtream profile performs these standard Player API operations:

- account authentication
- live TV categories and streams
- movie/VOD categories and items
- series categories and series
- series details, seasons, and episodes on demand
- short EPG responses for later guide integration

Provider DTOs remain inside the Xtream module and are mapped into provider-independent Core models. No retry loop is used; a failed request returns a friendly error and can be tried again manually.

## Security behavior

The legacy Player API requires the username and password in query parameters, and playback addresses commonly contain them in path segments. MyIPTV therefore:

- builds those addresses only in memory;
- uses a dedicated `HttpClient` with framework URI logging removed;
- never logs request URIs, response bodies, usernames, passwords, or playback addresses;
- redacts stream addresses from model string representations;
- bounds each JSON response to 64 MiB;
- keeps passwords in Windows DPAPI-protected storage;
- uses normal TLS certificate validation and never accepts invalid certificates.

Prefer an HTTPS provider address. With plain HTTP, credentials and catalog traffic are not encrypted in transit and can be observed by the network. This is a limitation of the provider configuration, not something application-side encryption can correct.

The full Live TV, movie, series, and guide interfaces are delivered in their later UI phases. During Phase 8, their screens show the loaded item counts after a successful connection.
