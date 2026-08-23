# Architecture

MyIPTV starts as a modular monolith: one desktop program whose responsibilities are divided into projects.

## Dependency direction

```text
MyIPTV.App ───────────────► MyIPTV.Core
     │
     └────► MyIPTV.Infrastructure ─────► MyIPTV.Core

MyIPTV.Tests ─────────────► Core and Infrastructure
```

- `MyIPTV.App` owns WPF Views, XAML, ViewModels, navigation, and application startup.
- `MyIPTV.Core` owns provider-independent models, interfaces, and business rules.
- `MyIPTV.Infrastructure` implements persistence, networking, credentials, IPTV providers, and playback integrations.
- `MyIPTV.Tests` contains automated tests using synthetic data only.

Core does not reference the user interface, database, network, or media player. This keeps business rules independently testable and prevents provider-specific behavior from spreading through the application.

## Startup flow

1. WPF creates `App`.
2. The .NET Generic Host loads configuration and constructs the dependency-injection container.
3. Configuration values are validated before services are used.
4. SQLite applies any embedded migrations that have not already run.
5. Non-sensitive user settings are loaded.
6. Navigation resolves `HomeViewModel`, and WPF displays its matching View.
7. Closing the window stops and disposes the Host cleanly.

Application configuration describes deployment-time behavior such as network timeouts. User settings describe preferences such as theme and volume. Keeping them separate prevents ordinary preferences from becoming an accidental credential store.

The Settings ViewModel reads and atomically writes the non-sensitive JSON preference record. Startup applies the saved theme, player volume, aspect ratio, and selected start page before presenting the main window. EPG preferences are shared with the Guide screen through the same service. Cache maintenance is limited to the resolved application cache directory; credential and database directories are separate and never included.

## Diagnostic logging

Microsoft logging abstractions carry structured events from startup, profile connection, parsing, playback, EPG, database, and exception boundaries. A daily JSON-lines file provider writes information-or-higher events under the local Logs directory. Before serialization it sanitizes rendered messages and exceptions, removing bearer credentials, sensitive key/value assignments, and every HTTP query string. Provider-specific clients additionally remove framework HTTP loggers so complete legacy authentication URLs never enter the pipeline.

File logging is intentionally fail-safe: an unavailable log directory reports only a generic type to the debugger and never interrupts the application. Technical logs remain local and should still be reviewed before being shared.

## Credential boundary

`ICredentialService` keeps password handling independent from profile and provider code. Its Windows implementation serializes the smallest required credential payload, protects it with DPAPI `CurrentUser` scope, and atomically writes one opaque file per profile under `%LocalAppData%\MyIPTV\Credentials`.

SQLite contains profile metadata but no password or token columns. Logs contain only a profile identifier and operation result; usernames, passwords, protected bytes, and credential-bearing URLs are excluded. Plaintext serialization buffers are cleared after encryption or decryption. If a protected file is missing, damaged, or belongs to another Windows account, the service returns no credentials and the UI asks for the password again.

## M3U import flow

`IM3uPlaylistParser` reads playlist text incrementally from a stream, maps provider metadata into provider-independent `IptvChannel` records, and returns counts instead of logging individual entries. `IPlaylistImportService` owns local-file and remote-HTTP loading. Remote responses use `ResponseHeadersRead`, so the whole source file is not buffered before parsing.

Successful imports atomically replace that profile's entries in `IChannelCatalog`. The catalog is currently in memory; reconnect after an application restart to import it again. Persistent channel tables and indexes belong to the later database phase. The Live TV browser and global search read immutable catalog snapshots.

## Content providers

`IContentProvider` is the provider-independent catalog boundary. The M3U implementation delegates to the streaming playlist importer. The Xtream implementation authenticates, maps provider DTOs into Core models, and atomically replaces the live and media catalogs only after every required response succeeds.

Xtream's legacy API requires credentials in request query strings and playback paths. Its dedicated named `HttpClient` removes all framework HTTP loggers, while application logs contain only profile IDs and item counts. Response bodies, request URIs, credentials, and generated playback URLs are never logged. Credential-bearing model `ToString()` methods redact stream URLs.

Stalker/Ministra support stays isolated under `Infrastructure/Providers/Stalker`. It uses the documented username/password REST flow, keeps bearer sessions in memory, and atomically publishes direct HTTP/HTTPS live channels. Legacy MAG/STB interfaces that depend on MAC addresses, serial numbers, or device emulation are deliberately outside the provider boundary and return a clear compatibility diagnostic.

## Playback boundary

`IPlaybackService` exposes provider-independent playback state and controls. `LibVlcPlaybackService` owns the native LibVLC lifetime, media disposal, track discovery, and sanitized playback logging. `IPlaybackVideoSource` is the narrow bridge used by the WPF `VideoView`; Core never references a VideoLAN type.

Playback requests redact stream URLs from their diagnostic text. The engine logs only stable content IDs and content types, does not attach LibVLC diagnostic logging, and never records native media locations.

The Live TV ViewModel reads immutable snapshots from `IChannelCatalog`, builds case-insensitive category summaries, and filters existing channel records without copying stream data. WPF category and channel lists use recycling virtualization so visual-tree size follows the viewport rather than catalog size.

## Search boundary

`ISearchService` exposes provider-independent catalog search. `CatalogSearchService` snapshots the channel and media catalogs, performs cancellable matching away from the UI thread, ranks title prefixes before title substrings, and clamps result counts. The window ViewModel adds a 300 ms debounce and cancels obsolete queries as the user types.

Search results contain stable catalog identifiers, labels, and content kinds only. Stream URLs and provider credentials never cross into the search result model. Selecting a result navigates through `INavigationService`; live-channel results also select the matching channel in the browser.

## Favorites persistence

`IFavoriteRepository` keeps favorite behavior independent from SQLite. Its SQLite implementation uses `(profile_id, content_type, content_id)` as a composite primary key and stores only the display title and addition timestamp alongside that key. It never persists a playback URL or provider secret. ViewModels subscribe to favorite changes so Live TV, movie, series, and Favorites screens stay synchronized. Deleting a profile removes its orphaned favorites.

## Playback history

`IWatchHistoryRepository` persists minimal playback history behind a provider-independent interface. `PlaybackHistoryCoordinator` is a hosted service that observes player state, checkpoints active VOD every 15 seconds, and flushes current progress during graceful shutdown. The database is initialized before hosted services start, preventing startup races with migrations.

History keys use the same stable profile/content identity as favorites. Stored rows contain a title, timestamp, position, and optional duration, but no stream location or credential. A resume request resolves the current stream from the in-memory catalog and gives LibVLC only the saved start position.

## EPG pipeline

`IXmlTvParser` incrementally reads XML with DTD and external entities prohibited, normalizes accepted timestamps to UTC, and produces provider-independent channel/program records. `IEpgService` validates local or HTTP sources, limits decompressed data, performs conditional HTTP refresh, and activates cached guide data. The source address is represented in SQLite and logs only by a SHA-256 identifier.

`IEpgRepository` atomically replaces a source's cached channels and programs and uses indexed UTC fields for guide and now/next lookups. The Guide ViewModel requests a bounded time window, while Live TV maps the selected channel's EPG ID to a current/next lookup. Local-time conversion occurs only in display ViewModels.

## VOD browser

The movie browser joins immutable `IMediaCatalog` snapshots with stable favorite and watch-history keys. Its card ViewModels contain provider metadata and state needed by the UI, while the underlying credential-bearing playback URL remains on the redacting Core movie model and is passed only to `IPlaybackService` when Play or Continue Watching is invoked.

Categories and movie rows use recycling virtualization. Background history notifications marshal refreshes to the captured UI synchronization context and preserve the current category/movie selection where possible.

## Series browser

The series browser starts with the immutable catalog summary and requests full episode details only after a series is selected. It resolves the owning profile to an `IContentProvider`, so provider-specific API calls and authentication remain outside the UI layer. Newer selections cancel obsolete detail requests and catalog publication is suppressed from recursively restarting the active load.

Episodes are grouped into ordered season ViewModels and joined to watch history by stable profile and episode identifiers. Play and Continue Watching pass the currently authorized catalog URL directly to the playback boundary; favorites and history persist no stream addresses or credentials. Virtualized series and episode lists keep the visual tree bounded for large libraries.
