# MyIPTV

MyIPTV is a Windows desktop media-player project for connecting to IPTV sources that the user is authorized to access.

The repository currently contains the verified .NET 10/WPF project foundation. IPTV providers and playback are not implemented yet.

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

## Testing dependency

Tests use the Microsoft-supported MSTest framework. MSTest is actively maintained and distributed under the MIT license.

## Security and legal usage

Do not place IPTV passwords, tokens, complete credential-bearing URLs, or real accounts in source code or tests. Future credentials will be stored using Windows-protected storage and excluded from logs and the application database.

Only connect MyIPTV to media services and streams that you own or are authorized to access. The project must not be used to bypass subscriptions, authentication, DRM, or server security.
