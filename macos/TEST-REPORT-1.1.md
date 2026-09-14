# Noki’s IPTV Player for MacOS — 1.1.0 verification

Tested on Apple Silicon/macOS 26 with VLCKit 3.7.3. Local build only; nothing published or deployed.

## Changes

- Removed the duplicate title oval. A single, fixed-size toolbar group contains the refresh and add icons. Loading replaces the refresh icon inside its existing bounds.
- Source and EPG URLs now use high-contrast, multiline fields. The source sheet is wider and long pasted links wrap.
- Isolated the progress timeline from the rest of the interface. Other playback properties publish only actual changes; the timer runs every 500 ms in common run-loop modes.
- Enabled automatic hardware-decoder selection and a 2-second network buffer, increasing to at most 5 seconds during retries.
- Added a monotonic watchdog for stalled time and frozen displayed frames. Brief buffering and intentional pauses are tolerated. A sustained stall reconnects after 8 seconds; startup is bounded at 20 seconds after link resolution.
- Reconnection resolves a fresh provider link, preserves VOD position and external subtitles, shows “Reconnecting…”, and caps consecutive retries at three. Thirty seconds of healthy playback resets the budget.

## Automated results

41 core checks passed (`.build/core-test-1.1.txt`). These include the existing provider/parser/EPG/persistence checks plus watchdog scenarios for short buffering, sustained stalls, frozen video with advancing audio time, audio-only streams, intentional pauses, seeking, startup timeout, errors, stable-playback budget reset, and live streams with no usable playback clock.

26 actual VLCKit playback checks passed (`Tests/Fixtures/playback-results-1.1.json`). These cover video/audio, captions, seeking, fullscreen, control visibility, unavailable files/retries, recovery, VOD resume, subtitle restoration, pause protection, and confirming that progress updates do not repeatedly publish whole-player changes.

The watchdog’s timing decisions were exercised with a simulated monotonic clock. Actual local-media reconnection/resume used the same recovery path. Real network-outage recovery is not claimed as tested by these fixtures.

## Visual checks

- Inspected the toolbar at normal window size: both icons fit inside the oval with balanced spacing and no duplicate title capsule.
- Pasted a long dummy URL through the native clipboard into source settings. Confirmed the full value remained readable across four wrapped lines.
- Cancelled the test form without saving or modifying user sources.

## Packaging

The final 39 MB DMG passed hdiutil integrity verification and its SHA-256 check. It was mounted read-only and copied to a clean internal directory. Deep/strict code-signature verification passed. The copied executable matched the tested build byte-for-byte and passed all 26 playback checks. Total: 67 passing automated checks.

## Limits

Provider-side congestion and network outages can still interrupt video. Hardware decoding depends on codec support. Existing saved sources and credentials were preserved. This update’s playback/recovery tests used local fixtures; live-provider validation is not newly claimed. The DMG is locally ad-hoc signed, not notarized.
