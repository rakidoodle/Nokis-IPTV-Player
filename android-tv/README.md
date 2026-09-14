# Noki’s IPTV for Android TV & Google TV

A native Kotlin TV app based on Noki’s macOS IPTV player. Android TV and Google TV first; requires Android 8.0 (API 26) or later. The app supplies a player, not a channel subscription.

## Install

Download the APK from the [combined release](https://github.com/rakidoodle/Nokis-IPTV-Player/releases/tag/cross-platform-preview-2026.09.14). Local packaging writes to `dist/`. Copy it to your TV and open it with a compatible installer, or use Android Debug Bridge:

```sh
adb install -r dist/Noki-IPTV-TV-0.1.3.apk
```

Open **Noki’s IPTV** from your TV’s apps. This is a debug-signed development build, not a Google Play release. Keep the signing key if you need future APKs to update this installation without uninstalling it.

## Add a source from your phone

1. On the TV choose **Scan QR from phone**, or **Add source → Use phone / show QR**.
2. Connect the phone to the same trusted network as the TV. Scan the code with its camera.
3. In the phone page choose M3U, Xtream Codes, or Stalker. Paste the URL and enter any provider credentials, then choose **Send to TV**.
4. Review the received details on the TV and choose **Save & load**.

A session expires after five minutes and accepts one source. Cancel or leaving the app closes the listener. To update a source, open **Settings → Edit → Use phone / show QR**. Enter the complete replacement details on your phone. This is a one-time transfer to the TV, not continuous account synchronization.

No cloud service or phone app is required. The QR fragment contains an ephemeral pairing token and public key; it does not contain provider credentials. The phone encrypts the submission with AES-256-GCM and wraps the key using RSA-OAEP-SHA256. The page and bundled JavaScript are served over local HTTP, so use a trusted network: this does not provide protection against an active attacker replacing the page. Guest Wi-Fi, client isolation, VPN routing, and some mesh configurations can prevent local pairing.

## Features

- M3U URL/file import with categories, logos, EPG metadata, relative HTTP links, and request headers.
- Xtream live, movie, and series catalogs; seasons and episodes; provider XMLTV guide.
- Stalker handshake, registered MAC/device fields, paginated catalogs, fresh stream resolution, and portal guide.
- Live TV, Movies, Series, Favorites, TV Guide, source settings, search, and category filters.
- Source-specific favorites, encrypted source/catalog/guide storage, and saved VOD positions.
- XMLTV and gzip XMLTV; explicit guide URL override; six-hour refresh while open.
- Media3 playback with hardware decoding where supported, embedded captions/audio selection, external SRT/VTT/ASS files, and seekable VOD.
- A startup watchdog and bounded stall recovery: 20-second playback startup, eight-second sustained stall, up to three retries, budget reset after 30 seconds of healthy playback. Provider resolution has a separate 30-second timeout.

Fullscreen controls, including the Library / Options buttons and channel name, hide after 3.5 seconds of playback inactivity. A D-pad or OK press reveals them without also activating a hidden action.

Live connections that unexpectedly end are re-resolved and restarted, with a maximum of three recovery attempts and increasing delays. A failed link refresh uses the same retry budget. Movies and episodes can still finish normally.

Large M3U and Xtream catalogs are parsed incrementally. Catalog and guide caches use compressed, authenticated encrypted chunks to avoid full JSON copies in memory; cache saves are serialized and coalesced. Existing sources and favorites are retained. Large legacy caches are fetched again automatically.

Each source has **Include Movies (VOD)** and **Include Series** options in TV setup and phone setup. Both default to On for existing sources. Xtream/Stalker skip disabled catalog requests. M3U remains one playlist download; recognized video files follow the Movies option, and a separate Series option is unavailable for M3U. Save & load applies changes and refreshes the source.

Categories use a scrollable dropdown. During playback, Back hides visible controls first, including while paused; Back again with controls hidden returns to the library. Background guide failures appear only as a status in TV Guide and automatically retry after 30 minutes; manual Refresh guide remains available.

## Remote controls

| Button | Action |
| --- | --- |
| D-pad | Move focus; navigate player controls |
| OK / Select | Activate focused action; reveal player controls |
| Back | Close dialog, hide visible playback controls, then leave playback on another press; return through library, then exit confirmation |
| Play / Pause | Control playback |
| Channel +/− or Media Next/Previous | Switch live channels in the current category |
| Left / Right in the player | Seek when the stream permits |
| Volume | Device / TV volume |

Playback pauses when the app goes into the background. Source setup, guide, favorites, and playback are designed to work without a mouse or touchscreen. A TV file picker is required for local playlists and external subtitle files; use source URLs if the TV has no picker.

## Build

Requires JDK 17, Android SDK Platform 35, Build Tools 35.0.0, and network access for the first Gradle dependency download. The checked-in Gradle wrapper pins Gradle 8.11.1; dependencies are pinned in the build files.

Set `JAVA_HOME` to your JDK, and set `ANDROID_HOME` or create an untracked `local.properties` containing `sdk.dir=/your/android/sdk`.

```sh
./gradlew :app:assembleDebug :app:testDebugUnitTest :app:lintDebug
./gradlew :app:connectedDebugAndroidTest   # a booted TV emulator / test device
scripts/package.sh
```

Build intermediates go under `~/.cache/noki-iptv-build/<project-path-hash>/` to avoid ExFAT AppleDouble metadata breaking Android resource processing. `scripts/package.sh` copies the final APK to the project’s `dist/` folder.

## Compatibility and limits

Real provider behavior and codec support vary. Media3 supports the HTTP HLS/DASH and progressive formats used by many IPTV providers, plus its supported RTSP profiles. RTMP, UDP, RTP-only links and VLC-specific codec behavior are not supported by this initial build; unsupported links show a playback error. No DRM credentials or proprietary Stalker extensions are implemented.

The fixture tests do not establish compatibility with your actual subscription, TV hardware decoder, receiver audio formats, or physical remote. See `TEST-REPORT-0.1.3.md` for the checks actually completed. There is no recording, catch-up, Chromecast sender mode, cloud sync, or phone/tablet layout in this TV-first release.

The Mac app, its saved sources, and its Keychain credentials are not modified or migrated.

## Dependencies

AndroidX / Media3, Kotlin, kotlinx libraries, OkHttp, Coil, ZXing, and node-forge retain their upstream licenses. The bundled node-forge license is in `app/src/main/assets/FORGE-LICENSE.txt`. See `THIRD-PARTY-NOTICES.md`.
