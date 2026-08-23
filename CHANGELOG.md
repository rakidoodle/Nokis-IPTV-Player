# Changelog

## 1.2.0-beta.3 - 2026-08-23

- Fix the rotating loading image storyboard name-scope exception.
- Ignore stale buffering callbacks after playback has already started, preventing the overlay from hiding active video while audio plays.
- Restore the exact pre-mute volume when unmuting and keep mute state synchronized independently of LibVLC callback timing.

## 1.2.0-beta.2 - 2026-08-23

- Rename the application to Noki's IPTV Player.
- Add Fluent-inspired Midnight and Ocean themes alongside refreshed dark and light palettes.
- Add a rotating branded loading indicator to Live TV.
- Move the volume slider to zero on mute and restore the previous level on unmute.
- Make full screen cover the Windows taskbar and restore the prior window state on exit.
- Scroll search-selected channels into view automatically.
- Reattach the native video surface after returning to Live TV so active playback remains visible.
- Open and autoplay live-channel favorites from the Favorites page.
- Show connection-specific profile fields, labels, and placeholders for M3U, Xtream, and Stalker.
- Normalize copied M3U links containing escaped ampersands and use playlist-compatible request headers.
- Add authorized MAC-address authentication for compatible Stalker portals.
- Remove the profile credential-protection notice from the editor.
- Reduce the Windows x64 package to the required LibVLC runtime and remove the unused LibVLC HTTP-interface scripts.

## 1.1.0 - 2026-08-23

- Prevent duplicate profile names using trimmed, case-insensitive matching.
- Accept provider-issued M3U username/password query links while keeping credentials out of SQLite and logs.
- Restore Windows-protected credentials when an existing profile is selected for editing.
- Tolerate common Xtream response variations such as numeric string fields, keyed catalog objects, and empty-object catalogs.
- Simplify connection type names to M3U, Xtream, and Stalker.
- Add resizable Live TV category and channel panels, clearer video/audio/subtitle controls, and a labeled full-screen action.
- Correct volume and mute synchronization, including restoring a usable volume when unmuting from zero.
- Add custom minimize, maximize/restore, and close controls to the top navigation area.
- Add the new MyIPTV logo and Windows application icon.
