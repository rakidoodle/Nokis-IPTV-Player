# Noki’s IPTV TV — 0.1.1

Verification date: 14 September 2026.

This update addresses the reported persistent fullscreen toolbar and live channels stopping after a short connection.

## Changes

- The Library button, Options button, channel name, and native playback controls hide after 3.5 seconds of playback inactivity. The timer also works when a toolbar button has focus.
- D-pad / OK reveals hidden controls without activating a hidden action. Paused playback and open option dialogs remain usable.
- An unexpected end of a live connection now triggers a fresh provider link and reconnection. Movies and episodes retain normal end behavior.
- Recovery uses a maximum of three attempts, with increasing delays. A transient provider-link refresh failure consumes that same budget instead of immediately ending recovery.
- Increased playback buffering and HTTP connection/read timeouts. Pause/background intent and VOD recovery positions are preserved.
- Version code increases to 2; application ID and signing key are unchanged so the APK can update the original installation.

## Validation

19 JVM tests and 11 Android TV instrumentation tests passed (30 total). Android lint completed with no errors. APK signature and SHA-256 were verified; the signing certificate matches version 0.1.0.

Results and package verification are saved in `dist/verification-0.1.1/`. The regression suite includes live EOF versus normal VOD completion, a deliberately failed fresh-link request, bounded reconnection, and fullscreen toolbar hide/reveal with remote focus on Options. Existing provider, pairing, storage, guide, and playback tests remain part of the full suite.

Tests use an Android TV Android 14 ARM64 emulator and local fixture streams. They do not establish that every provider-side failure or physical TV codec issue is resolved. The affected subscriber stream was not available for direct testing.

Install `dist/Noki-IPTV-TV-0.1.1.apk` over the existing app; do not uninstall first if you want to retain saved sources and favorites.
