# Test report — Noki’s IPTV Player for MacOS 1.0.0

Environment: Apple Silicon, macOS 26.6.2, Swift 6.3.3 (Swift 5 language mode), VLCKit 3.7.3. Local ad-hoc signing; no publishing or deployment.

## Automated core checks

29 checks passed. Coverage includes M3U metadata and relative URLs, stream headers, HLS recognition, invalid playlists, XMLTV parsing and timezone offsets, malformed XML, Xtream authentication/catalogs/episode grouping, encoded credentials, Stalker pagination/token renewal/link resolution/guide retrieval, favorite persistence, and credential-free library metadata.

Command: `Scripts/test.sh`. Results: `.build/core-test-results.txt`.

## Playback and interface

A generated 20-second H.264/AAC fixture and SRT/VTT captions are used for actual VLCKit playback. Testing is performed using fixtures in `/tmp/noki-iptv-qa` to avoid removable-volume access prompts interfering with automation.

Playback checks cover video/audio tracks and progressing time, seeking, pause, subtitle import/selection/off, volume/mute, fullscreen entry/exit, auto-hiding and restored controls, loading indicators, bounded retries, recovery, and stop. All 20 playback checks passed. Results are recorded in `Tests/Fixtures/playback-results.json` (49 passing automated checks in total).

Light and dark native layouts have been visually inspected, including a reduced window size of approximately 900 × 680 pixels. Controls and navigation remained visible and usable. Reduce Motion handling was inspected in code; the system preference itself was not changed. Video rendering and visible SRT/VTT subtitles were confirmed. The source editor successfully imported a local M3U and fetched its XMLTV guide. Keychain saved credentials and an encrypted catalog cache. The generated “Local QA” source has been removed; user sources are preserved.

Issues found and corrected during testing:
- Buffering state could remain reported by VLC during active playback. The indicator now accounts for progressing playback.
- Credential/cache lookup during initial app construction could block window creation. Saved-source loading now runs in the background.
- Missing media files are rejected before opening; streams stalled during opening/buffering have bounded retries.
- Favorites for episodes are retained across catalog refreshes.
- External-drive metadata interfered with codesigning. Packaging now signs from a clean internal temporary directory.

## Live providers

User-configured sources detected: “PH” (M3U) and “Sports” (Stalker). Live validation is blocked at macOS Keychain authorization, before provider requests can be verified. The user has been asked to complete the system prompt. Neither source is reported as live-verified. No Xtream live credentials were supplied; Xtream compatibility is currently fixture-tested only.

## Installer

DMG creation and `hdiutil verify` passed. The DMG was mounted read-only, its app copied to a clean internal directory, and `codesign --verify --deep --strict` passed. The copied Apple Silicon app launched successfully without a separate VLC installation and passed all 20 playback checks. This build is not Developer ID signed or notarized. macOS 14 is the minimum build target, but this Mac is the only OS/hardware combination exercised.
