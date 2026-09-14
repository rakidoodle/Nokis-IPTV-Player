# Noki’s IPTV Player for MacOS

Version 1.1.0. Native Apple Silicon IPTV player for macOS 14 or newer. The current build is tested on macOS 26.6.2. Includes VLCKit; no separate VLC installation is needed.

Download the DMG from the [combined release](https://github.com/rakidoodle/Nokis-IPTV-Player/releases/tag/cross-platform-preview-2026.09.14).

## Use

Open the DMG and drag the app to Applications. This local build is ad-hoc signed, not notarized. macOS may require opening it using Finder’s Open action or Privacy & Security → Open Anyway. Do not disable Gatekeeper globally.

The toolbar keeps its loading spinner inside the refresh button. Source and EPG links use visible, multiline fields so long pasted URLs remain readable.

Use **Add Source** to enter an M3U playlist URL/file, an Xtream server URL and credentials, or a Stalker portal and the MAC registered with your provider. Source passwords and URLs are saved in macOS Keychain. After a development rebuild, macOS may ask you to authorize access again; complete its Keychain prompt to reuse saved sources. Catalog caches are encrypted using a separate Keychain key. Do not commit provider credentials to this project.

Use the sidebar for Live TV, Movies, Series, Favorites, and TV Guide. Click a channel or movie to play; click a series to choose a season and episode. Click stars to save favorites. EPG program favorites save the corresponding channel. Favorites belong to their source.

The player supports fullscreen, volume, play/pause, subtitles, and seeking when the stream permits it. Hardware decoding is requested when available. Playback progress updates are isolated from the library to reduce interface work. Network caching starts at 2 seconds and rises to at most 5 seconds during recovery. A sustained 8-second stall automatically reconnects the current stream; VOD resumes near its last position and externally loaded subtitles are restored. Startup has a 20-second watchdog after stream resolution. Recovery stops after three unsuccessful attempts, and the retry budget resets after 30 seconds of healthy playback. Intentional pauses do not reconnect. Provider/network problems can still cause interruptions. Fullscreen controls appear on pointer movement or keyboard use. Space toggles play/pause; Escape leaves fullscreen; Control-Command-F toggles fullscreen. Load external SRT/VTT/ASS subtitles through the caption menu, or select an embedded track.

Guide URLs may be XMLTV or gzip-compressed XMLTV. Playlist EPG metadata and Xtream/Stalker guide endpoints are used automatically; an EPG URL in source settings overrides them. Guides refresh every six hours while the app is open, with manual refresh available. Provider compatibility, guide coverage, subtitle languages, and series metadata depend on your service. Stalker portals with proprietary authentication extensions may require additional work.

## Build locally

Requirements: Apple Silicon Mac, Apple command-line developer tools (Swift, macOS SDK, codesign, hdiutil), Python 3 for the dependency downloader. No Xcode project or Homebrew install is required.

1. `python3 Scripts/download_vlc.py`
2. `tar -xf Vendor/VLCKit-3.7.3-complete.tar.xz -C Vendor`
3. `Scripts/test.sh`
4. `Scripts/build.sh`
5. `Scripts/package.sh`

The official pinned dependency is VLCKit 3.7.3, artifact `VLCKit-3.7.3-319ed2c0-79128878.tar.xz` from `https://download.videolan.org/cocoapods/prod/`. See `Vendor/DEPENDENCY.txt` for its verified SHA-256. The downloader uses byte ranges to tolerate slow connections, validates range lengths, and enforces the pinned SHA-256 before extraction.

The icon is generated from Assets/Brand.png. Regenerate using a Python environment with Pillow: `python3 Scripts/icons.py`, then `iconutil -c icns Assets/AppIcon.iconset -o Assets/AppIcon.icns`. The supplied artwork is preserved; the favicon is a companion asset because native macOS windows do not use favicons.

## Tests

`Scripts/test.sh` runs provider, playlist, guide, and persistence fixtures without real credentials.

`swift Scripts/generate_fixture.swift Tests/Fixtures` creates local H.264/AAC video and subtitle fixtures. Copy the three media/caption fixtures to `/tmp/noki-iptv-qa`, then run the built app executable with `--smoke-test /tmp/noki-iptv-qa`. This avoids removable-drive consent prompts during automated playback tests. It exercises playback, seeking, pause, subtitles, volume, fullscreen, and control visibility and writes `playback-results.json` in that directory. This testing mode is only activated by an explicit command-line argument.

Real-provider testing is separate. Enter your source credentials in the app, then verify Live TV, VOD, Series, EPG, and captions for each provider type available to you. See TEST-REPORT.md for actual completed and pending checks.

## Licenses and distribution

VLCKit and its bundled dependencies retain their upstream license notices inside the framework. VLCKit is dynamically linked, and the app bundles the upstream LGPL license. VLCKit source: https://code.videolan.org/videolan/VLCKit (3.7.3 / 319ed2c0); libVLC source: https://code.videolan.org/videolan/vlc (79128878). Preserve notices and corresponding-source obligations if redistributing binaries. Public preview downloads are provided in this repository’s Releases.
