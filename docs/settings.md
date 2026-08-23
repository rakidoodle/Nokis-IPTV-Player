# Settings

The Settings screen groups non-sensitive preferences and local data controls in one keyboard-accessible, scrollable view.

## General

- **Start page** chooses the first library screen shown on the next launch.
- **Theme** switches among dark, light, Midnight, and Ocean Fluent-inspired palettes when settings are saved.
- **Language** currently provides English (`en-US`). The stored language identifier and resource boundary are ready for future localization.
- **Remember last profile** controls whether a future profile-selection workflow may restore the previous choice. Credentials remain independently protected by Windows DPAPI.

## Player

Default volume and aspect ratio apply immediately on save and again at startup. Hardware decoding is a preference for new player sessions. Reconnect behavior records whether playback failures should offer a reconnect action; it does not silently loop or retry an unauthorized address.

## EPG

The XMLTV source, refresh interval, and local/UTC display preference are shared with the Program Guide. Accepted source and parsing security rules are described in [XMLTV program guide](epg.md).

## Data and About

**Clear cache** removes only disposable files beneath `%LocalAppData%\MyIPTV\Cache`. **Clear watch history** requires confirmation and removes saved progress, but not profiles or favorites. The screen displays the exact database and log directories and can open them in Windows Explorer.

Preferences are atomically stored in `%LocalAppData%\MyIPTV\settings.json`. Passwords, bearer tokens, session secrets, and credential-bearing URLs must never be added to this file.
