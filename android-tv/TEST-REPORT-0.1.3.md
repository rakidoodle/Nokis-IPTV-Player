# Noki’s IPTV TV — 0.1.3

## Changes

- Background guide-refresh failures no longer open a modal. A status is shown in TV Guide while existing schedules are retained. Automatic retries wait at least 30 minutes; manual refresh remains available.
- Categories use a bounded, scrollable dropdown with remote focus, selection, and Back dismissal. Invalid category selections reset when the available categories change.
- During playing or paused video, Back first hides visible controls. Back with controls already hidden returns to the library. Options and error dialogs retain their own dismissal behavior.
- Each source stores Include Movies (VOD) and Include Series settings, defaulting to enabled for compatibility. Xtream and Stalker skip requests for disabled catalogs. M3U filters recognized movie entries during parsing; it still downloads the shared playlist and does not expose a separate series catalog.
- Both TV source editing and the encrypted phone setup form expose the applicable options. Save & load refreshes the catalog with the chosen settings.
- Version code 4, version 0.1.3; application ID and signing identity are unchanged.

## Verification

21 JVM tests passed. All 14 Android instrumentation scenarios passed across the full run and final focused rerun (35 total scenarios); lint reported zero errors. The initial full run passed the dropdown and paused-Back checks but exposed a main-thread URL construction error in the new guide-failure test fixture. That fixture was corrected. The final six-test device run passed, including guide-failure UI, category selection, paused Back behavior, 50,000-channel search, QR navigation, and encrypted phone transfer with VOD/Series disabled. Earlier full-run results retain the storage, 50,000-channel encrypted cache, and live recovery checks. Evidence is in `dist/verification-0.1.3/`. Tests use synthetic local sources and an Android TV Android 14 ARM64 emulator. The user's physical TV and subscription are not directly tested.
