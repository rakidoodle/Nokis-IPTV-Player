# MyIPTV

MyIPTV is a Windows desktop media-player project for connecting to IPTV sources that the user is authorized to access.

The repository currently contains the verified .NET 10/WPF application foundation, profile management, secure credentials, provider loading, native media playback, a virtualized channel browser, and global catalog search.

## Foundation features

- Generic Host controls startup and graceful shutdown.
- Constructor dependency injection creates services and ViewModels.
- MVVM navigation keeps screen logic out of window code-behind.
- Validated application configuration comes from `appsettings.json`.
- Non-sensitive user preferences are written atomically to local JSON.
- SQLite migrations create and version the local database safely.
- Managed `HttpClient` instances have a validated 30-second timeout.
- Central exception handling logs technical details and shows friendly messages.
- Responsive media-style navigation adapts from a labeled sidebar to a compact icon rail.
- Dark and light themes persist between application restarts.
- Home, Live TV, Movies, Series, Favorites, Guide, and Settings screens are keyboard accessible.
- Reusable empty, loading, and error states provide consistent feedback.
- IPTV profiles support local or remote M3U playlists, Xtream API services, and Stalker/Ministra portals.
- Profile metadata is stored in SQLite while credentials are encrypted for the current Windows user and redacted from diagnostics.
- Connection tests validate local M3U headers, remote playlists, and provider server reachability.
- A cancellable, streaming M3U/M3U8 importer handles common channel metadata, malformed entries, duplicates, and large playlists.
- Authorized Xtream profiles authenticate and load live TV, movie, series, episode, category, and short-EPG data through a credential-safe provider boundary.
- Authorized Ministra REST profiles authenticate with username/password and load live channels without device or MAC impersonation.
- Bundled LibVLC playback supports HTTP/HTTPS IPTV media, transport controls, reconnect, aspect ratios, tracks, subtitles, and full screen.
- The Live TV screen groups channels by category, virtualizes large lists, and plays the selected authorized stream.
- Debounced global search finds loaded channels, movies, series, episodes, and categories without exposing stream addresses.
- SQLite-backed favorites persist channels, movies, and series by stable provider IDs.
- Recently watched history saves VOD progress and offers Continue Watching from Home.
- XMLTV EPG supports timezone-safe parsing, channel mapping, current/next programs, conditional refresh, and a virtualized guide.
- The VOD browser provides categories, posters, provider metadata, playback, favorites, and Continue Watching.

## Requirements

- Windows 10 or Windows 11, x64
- .NET 10 SDK
- Git
- Visual Studio Code or Visual Studio (optional; command-line builds are supported)

## Build and test

From PowerShell in the repository root:

```powershell
dotnet restore
dotnet build
dotnet test
```

Run the starter application with:

```powershell
dotnet run --project .\src\MyIPTV.App\MyIPTV.App.csproj
```

## Project structure

- `src/MyIPTV.App` — WPF user interface and application startup
- `src/MyIPTV.Core` — models, interfaces, and provider-independent rules
- `src/MyIPTV.Infrastructure` — database, network, security, provider, and playback implementations
- `tests/MyIPTV.Tests` — automated tests using safe synthetic data
- `docs` — engineering and user documentation

See [Architecture](docs/architecture.md) for the dependency design.
See [User interface](docs/user-interface.md) for navigation, themes, responsive behavior, and accessibility.
See [IPTV profiles](docs/profiles.md) for profile setup, connection testing, and credential security.
See [M3U playlists](docs/m3u-playlists.md) for supported metadata, importing, limits, and current catalog behavior.
See [Xtream profiles](docs/xtream.md) for supported API operations and security limitations.
See [Stalker / Ministra compatibility](docs/stalker-ministra.md) for the supported REST interface and deliberate legacy-device limitations.
See [Media playback](docs/playback.md) for controls, formats, WPF behavior, and VideoLAN licensing.
See [Live TV browser](docs/live-tv.md) for category filtering, playback, and large-list behavior.
See [Global search](docs/search.md) for matching, ranking, keyboard use, and current catalog limits.
See [Favorites](docs/favorites.md) for persistence, stable IDs, and stored data.
See [Recently watched](docs/recently-watched.md) for saved progress, resume behavior, and privacy.
See [XMLTV program guide](docs/epg.md) for source setup, channel mapping, caching, and timezone behavior.
See [Movies and VOD](docs/movies.md) for metadata, playback, favorites, and resume behavior.

## Local application data

Runtime data is stored under `%LocalAppData%\MyIPTV`:

- `myiptv.db` — versioned SQLite application database
- `settings.json` — non-sensitive user preferences only
- `Cache` — reserved for disposable cached data
- `Logs` — reserved for application log files
- `Credentials` — DPAPI-encrypted credential files, one per profile

IPTV passwords and authentication tokens are never written to settings, logs, or SQLite records. Each credential file is encrypted with Windows Data Protection API (DPAPI) using `CurrentUser` scope. Windows manages the encryption key, so the file can only be decrypted by the same Windows account on the same Windows installation. Credentials normally need to be entered again after moving the application data to another computer, reinstalling Windows, or losing the Windows user profile.

## Testing dependency

Tests use the Microsoft-supported MSTest framework. MSTest is actively maintained and distributed under the MIT license.

See [Runtime dependencies](docs/dependencies.md) for package versions, purposes, and licenses.

## Security and legal usage

Do not place IPTV passwords, tokens, complete credential-bearing URLs, or real accounts in source code or tests. Credentials are stored using Windows DPAPI protection and excluded from logs and the application database. Deleting a profile also deletes its protected credential file.

Only connect MyIPTV to media services and streams that you own or are authorized to access. The project must not be used to bypass subscriptions, authentication, DRM, or server security.
