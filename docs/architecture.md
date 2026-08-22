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

## Credential boundary

`ICredentialService` keeps password handling independent from profile and provider code. Its Windows implementation serializes the smallest required credential payload, protects it with DPAPI `CurrentUser` scope, and atomically writes one opaque file per profile under `%LocalAppData%\MyIPTV\Credentials`.

SQLite contains profile metadata but no password or token columns. Logs contain only a profile identifier and operation result; usernames, passwords, protected bytes, and credential-bearing URLs are excluded. Plaintext serialization buffers are cleared after encryption or decryption. If a protected file is missing, damaged, or belongs to another Windows account, the service returns no credentials and the UI asks for the password again.
