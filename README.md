# MyIPTV

MyIPTV is a Windows desktop media-player project for connecting to IPTV sources that the user is authorized to access.

The repository currently contains the verified .NET 10/WPF application foundation, MVVM navigation, dependency injection, configuration, settings, logging, managed HTTP clients, and a versioned SQLite database. IPTV providers and playback are not implemented yet.

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

## Local application data

Runtime data is stored under `%LocalAppData%\MyIPTV`:

- `myiptv.db` — versioned SQLite application database
- `settings.json` — non-sensitive user preferences only
- `Cache` — reserved for disposable cached data
- `Logs` — reserved for application log files

IPTV passwords and authentication tokens must never be written to these ordinary settings or database records. Windows-protected credential storage will be introduced in its dedicated security phase.

## Testing dependency

Tests use the Microsoft-supported MSTest framework. MSTest is actively maintained and distributed under the MIT license.

See [Runtime dependencies](docs/dependencies.md) for package versions, purposes, and licenses.

## Security and legal usage

Do not place IPTV passwords, tokens, complete credential-bearing URLs, or real accounts in source code or tests. Future credentials will be stored using Windows-protected storage and excluded from logs and the application database.

Only connect MyIPTV to media services and streams that you own or are authorized to access. The project must not be used to bypass subscriptions, authentication, DRM, or server security.
