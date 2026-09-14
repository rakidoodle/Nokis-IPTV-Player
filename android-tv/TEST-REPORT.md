# Noki’s IPTV for Android TV / Google TV — 0.1.0

Verification date: 10 September 2026. Local debug build; not published to Google Play.

## Environment

- macOS Apple Silicon build host, JDK 17, Gradle 8.11.1, Android Gradle Plugin 8.9.3.
- Compile / target SDK 35; minimum SDK 26 (Android 8.0).
- Android TV ARM64 emulator, Android 14 / API 34, 1920 × 1080.
- Media3 1.8.0. Media fixture: generated 20-second H.264/AAC MP4 from the Mac project's test fixture.

## Automated checks

18 JVM tests pass:

- M3U metadata, quoted groups, relative URLs, custom headers, BOM, metadata reset, duplicate handling, HLS preservation, invalid input, and source-specific identities.
- XMLTV text/time-zone parsing, gzip guides, and rejection of DTD/external entity input.
- Source validation, Xtream authentication/catalogs/episodes/credential encoding, and Stalker pagination/fresh stream links using fixture HTTP servers.
- Startup timeout, short/sustained stalls, intentional pauses, seeking, bounded retries, and retry-budget reset with a simulated monotonic clock.

10 on-device instrumentation tests pass:

- Android Keystore encryption round trip, ciphertext inspection for fixture secrets, encrypted cache read/write/delete.
- Pairing submission decryption, invalid-token rejection, cross-origin rejection, and one-use enforcement.
- Tampered encrypted submission rejection.
- Locally served phone page and no-cache headers.
- The actual bundled phone JavaScript running in Android WebView encrypts and sends a source successfully.
- XMLTV parsing on Android and DTD rejection.
- Actual rendered video frame and audio/video format detection, pause, seek, resumed playback, and stop.
- D-pad activation and Back navigation through the source editor.
- D-pad QR setup and cancellation.
- End-to-end source import, current programme display, favorites, guide, playback, Media Next channel switching, and return to the guide using local fixtures.

Android lint completes without errors. Remaining warnings concern pinned dependency updates, optional Kotlin convenience APIs, and the intentional TV landscape orientation; they are not hidden behind a lint baseline.

## Visual checks

Inspected emulator captures of the library and player with generated fixture channels. Verified readable dark-theme text, highlighted focus, source forms, and the QR setup layout. The bundled artwork comes from the user's Mac project. Screenshots in `dist/screenshots/` show either the empty app or clearly named test content.

## Packaging

`dist/Noki-IPTV-TV-0.1.0.apk` is the debug-signed installable package. A SHA-256 companion file and verification outputs are included. The packaged APK is installed on the Android TV emulator for the final launch check.

## Not established by these tests

- Physical Android TV / Google TV device performance, hardware codec coverage, HDMI audio, or a particular physical remote.
- Scanning with a physical phone camera, iOS Safari, and guest/mesh Wi-Fi routing. The actual phone page's encryption and transfer were exercised in Android WebView.
- A live subscriber account for M3U, Xtream, or Stalker; proprietary portal extensions.
- Real network-outage recovery, long-running live streams, frozen-video-with-advancing-audio detection, or behavior under very large provider catalogs. The watchdog tests simulate clock/progress conditions.
- All subtitle formats and styles on physical devices. Embedded track controls and external file loading are implemented; exhaustive subtitle interoperability is pending.
- Background recording, DRM, catch-up, Chromecast sending, cloud synchronization, and a mobile touch layout are not part of this release.

Use a dedicated emulator/test device for connected tests. No real provider credentials are used by the fixtures, and the Mac project's saved sources and Keychain were not accessed.
