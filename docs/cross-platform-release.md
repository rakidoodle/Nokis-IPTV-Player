# macOS 1.1.0 + Android TV 0.1.3 preview

This release adds native macOS and Android TV / Google TV applications alongside the existing Windows player.

## Downloads

- **Android TV / Google TV:** `Noki-IPTV-TV-0.1.3.apk` — Android 8.0+.
- **macOS:** `Noki-IPTV-1.1.0-arm64.dmg` — Apple Silicon, macOS 14+.
- **Both:** `Nokis-IPTV-macOS-AndroidTV-preview.zip` — both installers and instructions.
- **Checksums:** `SHA256SUMS.txt`.

[Windows 1.2.0-beta.6 remains available here](https://github.com/rakidoodle/Nokis-IPTV-Player/releases/tag/v1.2.0-beta.6).

The APK is debug-signed. The macOS build is ad-hoc signed and not notarized; installation may require Privacy & Security → Open Anyway. These are preview downloads, not app-store releases. Install Android over the existing version to retain sources and favorites.

## Android highlights

- Remote control navigation, category dropdown, encrypted phone setup via QR.
- Per-source Movies (VOD) and Series download options.
- Back hides visible playback controls first, including when paused.
- Quiet background guide errors and a 30-minute retry interval.
- Streaming catalog import and encrypted caching tested with 50,000 channels.
- Bounded live-stream recovery and fullscreen controls that auto-hide.

## macOS highlights

- Native SwiftUI interface with bundled VLCKit.
- M3U, Xtream, and supported Stalker sources; movies, series, favorites, and XMLTV guides.
- Keychain-backed credentials, encrypted caches, fullscreen playback and subtitles.
- Bounded stall recovery with VOD resume.

No channels, accounts, subscriptions, or private credentials are supplied. Use your own authorized sources. Platform features differ; consult each platform README. Source archives include all three projects; build dependencies and private signing keys are excluded.

Android 0.1.3 previously passed 21 JVM tests and 14 TV instrumentation scenarios. macOS 1.1.0 previously passed 41 core and 26 local playback checks. These tests use fixtures and do not guarantee every provider or device.
