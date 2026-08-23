# Noki's IPTV Player

Noki's IPTV Player is a modern Windows desktop player for IPTV and media sources that you own or are authorized to use. It is built with C#, .NET 10, WPF, MVVM, SQLite, and LibVLC.

The application separates the interface from provider, database, credential, and playback code. In plain English, a ViewModel contains a screen's behavior while its XAML View controls what you see. Dependency injection connects those parts without hiding global state inside the application.

## Features

- Multiple M3U/M3U8, Xtream-compatible, and authorized MAC-based Stalker/Ministra profiles
- Duplicate-name protection and Windows-protected credentials that are restored when editing a profile
- Local and remote playlists with streaming, cancellable parsing
- Live TV with resizable category/channel panels, full screen, volume, track selection, and current/next program details
- Movie and series browsers with provider metadata, seasons, episodes, favorites, and resume progress
- XMLTV EPG with safe parsing, caching, timezone conversion, now/next data, and a TV guide
- Debounced global search across channels, movies, series, episodes, and categories
- Persistent favorites, recently watched items, and non-sensitive preferences
- Dark, light, Midnight, and Ocean themes, responsive resizing, keyboard navigation, accessible names, and visible focus
- Safe development library under **Settings > Data** using non-functional `example.invalid` addresses
- Structured local logs with credential, token, header, and URL-query redaction
- Friendly error states for network, provider, malformed-data, database, and playback failures
- Bounded background search and recycling virtualization designed for catalogs up to 50,000 entries

## Requirements

To run the packaged version:

- 64-bit Windows 10 or Windows 11
- About 500 MB free disk space after extraction
- An IPTV playlist or account that you are authorized to use (optional for the safe demo library)

The self-contained package already includes .NET and LibVLC. To develop the project, also install:

- .NET 10 SDK
- Git
- Visual Studio 2022 or Visual Studio Code is optional; PowerShell is sufficient

## Development Setup

Open PowerShell in the repository folder and run:

```powershell
dotnet --info
dotnet restore .\MyIPTV.sln
dotnet build .\MyIPTV.sln
```

NuGet is .NET's package manager. `dotnet restore` downloads the exact libraries listed in `Directory.Packages.props`; it never downloads IPTV content.

Runtime data is created under `%LocalAppData%\MyIPTV`. Source code, tests, and build output do not contain real IPTV accounts.

## Running the Application

For normal use with the release ZIP:

1. Extract `Nokis-IPTV-Player-1.2.0-beta.1-win-x64.zip` to a folder you control.
2. Open the extracted folder and double-click `MyIPTV.App.exe`.
3. Open **Profiles**, choose **Add**, select the correct connection type, and enter the details issued by your provider.
4. Choose **Test Connection**, then **Connect** to load the catalog.
5. Browse **Live TV**, **Movies**, **Series**, or **Guide** and select an item to play.

To explore without an account, open **Settings**, find **Data**, and choose **Load demo library**. Demo addresses are intentionally non-playable.

To run from source:

```powershell
dotnet run --project .\src\MyIPTV.App\MyIPTV.App.csproj
```

## Building

Create a normal developer build:

```powershell
dotnet restore .\MyIPTV.sln
dotnet build .\MyIPTV.sln
```

Create the verified production configuration:

```powershell
dotnet clean .\MyIPTV.sln -c Release
dotnet restore .\MyIPTV.sln
dotnet build .\MyIPTV.sln -c Release --no-restore
```

The solution enables nullable checks, current recommended .NET analyzers, deterministic output, and code-style analysis. Version metadata is centralized in `Directory.Build.props`.

## Testing

Run the complete synthetic test suite:

```powershell
dotnet test .\MyIPTV.sln
```

Release acceptance uses:

```powershell
dotnet test .\MyIPTV.sln -c Release --no-build
dotnet list .\MyIPTV.sln package --vulnerable --include-transitive
```

Tests cover M3U parsing and malformed input, provider mapping, XMLTV, SQLite repositories and migrations, search, favorites, URL validation, credential storage boundaries, redaction, settings, catalog persistence, errors, and large-library behavior. They use only generated data and reserved example domains. See [Testing](docs/testing.md).

## Packaging

The recommended release is a self-contained Windows x64 ZIP. A framework-dependent package is smaller but requires the matching .NET Desktop Runtime. An installer or MSIX adds installation and upgrade behavior but needs additional signing and deployment decisions.

Build the ZIP and SHA-256 checksum with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-win-x64.ps1
```

Output is written to `artifacts\`. Single-file bundling and trimming are disabled because WPF and LibVLC require reflection and native companion files. See [Windows packaging](docs/packaging.md).

## Project Structure

```text
MyIPTV.sln
├── src/
│   ├── MyIPTV.App/             WPF Views, ViewModels, navigation, startup
│   ├── MyIPTV.Core/            Models, interfaces, provider-independent rules
│   └── MyIPTV.Infrastructure/  SQLite, security, providers, HTTP, EPG, playback
├── tests/MyIPTV.Tests/         Synthetic automated tests
├── samples/                    Safe non-functional development playlist
├── scripts/                    Repeatable Windows packaging
└── docs/                       Architecture and feature documentation
```

MVVM keeps screen logic out of code-behind. Interfaces define what a service does; infrastructure classes provide the implementation. DTOs are temporary shapes used to read provider responses without coupling those responses to the user interface. See [Architecture](docs/architecture.md).

## Supported IPTV Sources

- **M3U/M3U8:** local files and HTTP/HTTPS playlists, including provider-issued username/password query links; common `tvg-id`, `tvg-name`, `tvg-logo`, and `group-title` metadata
- **Xtream-compatible APIs:** normal username/password authentication, live/VOD/series catalogs, episodes, categories, and available short EPG data
- **Stalker/Ministra:** authorized username/password REST v2 portals that return direct HTTP/HTTPS channels
- **XMLTV:** local files and HTTP/HTTPS XML or XML.GZ sources

Provider implementations and server versions vary. Legacy MAG/STB flows requiring MAC/device impersonation are deliberately unsupported. DRM-protected streams depend on rights and playback support supplied by the provider. See [M3U](docs/m3u-playlists.md), [Xtream](docs/xtream.md), and [Stalker/Ministra](docs/stalker-ministra.md).

## Security

- Passwords are encrypted in per-profile files with Windows DPAPI `CurrentUser` protection.
- Passwords, bearer sessions, playable URLs, and authentication tokens are excluded from SQLite.
- Log messages and exceptions redact sensitive assignments, HTTP authorization/cookie/API-key headers, bearer values, and URL queries.
- Default .NET TLS certificate validation is never disabled.
- Provider HTTP diagnostics are suppressed where legacy APIs place credentials in request URLs.
- XML parsing prohibits DTDs and external entities; remote responses and decompressed EPG data have size limits.

Prefer HTTPS because plain HTTP cannot protect credentials or media traffic in transit. DPAPI protects stored files from other Windows accounts, but it cannot defend an already compromised, unlocked account. See [Security review](docs/security-review.md) and [Logging](docs/logging.md).

## Troubleshooting

- **The app does not start:** extract the entire ZIP before running; do not launch the executable from inside the archive. Review `%LocalAppData%\MyIPTV\Logs`.
- **Windows shows a warning:** this local build is not code-signed. Verify `artifacts\SHA256SUMS.txt` against the ZIP before running it.
- **Connection test fails:** confirm the address, account, internet connection, and subscription status. Provider-issued M3U username/password query links are accepted and separated into Windows-protected storage; other embedded tokens remain blocked.
- **A certificate error appears:** the server certificate is invalid or untrusted. MyIPTV will not bypass it; contact the provider.
- **Channels load but do not play:** the source may be offline, expired, DRM-protected, or use a codec/protocol unsupported by the bundled LibVLC runtime.
- **EPG is empty:** check the XMLTV source and channel IDs, then refresh the Guide. Provider channel names and XMLTV IDs must map correctly.
- **Credentials fail after moving PCs:** DPAPI data belongs to the original Windows account and installation. Enter the password again on the new PC.
- **Reset disposable data:** use **Settings > Data** to clear cache or watch history. Profile deletion separately removes its encrypted credential.

See [Error handling](docs/error-handling.md), [Settings](docs/settings.md), and [Release checklist](docs/release-checklist.md).

## Legal Usage

Only connect MyIPTV to playlists, accounts, media services, and streams that you own or are explicitly authorized to access. Do not use it to bypass subscriptions, authentication, geographic restrictions, DRM, or server security, and do not use another person's credentials. You are responsible for complying with your provider agreement and applicable law.

MyIPTV does not discover accounts, scrape credentials, harvest MAC addresses, emulate subscriber devices to obtain access, or provide media services of its own.

## Open-Source Licenses

MyIPTV uses open-source components including .NET, CommunityToolkit.Mvvm, Microsoft.Extensions, Microsoft.Data.Sqlite, MSTest, LibVLCSharp, and the VideoLAN LibVLC Windows runtime. The Microsoft and toolkit dependencies are MIT-licensed; LibVLCSharp and LibVLC are LGPL-2.1-or-later.

Redistributors must retain and satisfy the notices and license terms of every bundled dependency, especially the LGPL terms for VideoLAN components. See [Runtime dependencies and licenses](docs/dependencies.md) for exact versions and purposes. The repository does not grant rights to third-party IPTV content, branding, or service credentials.
