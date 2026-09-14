# Noki’s IPTV TV — 0.1.2

Verification date: 14 September 2026.

Addresses memory pressure while loading and caching large channel catalogs. The user’s physical-device crash log was not available, so the exact crash exception is unconfirmed.

## Changes

- M3U URL/file parsing and XMLTV/gzip parsing read streams instead of allocating whole response byte arrays and strings. The existing 64 MiB response limit remains enforced while reading.
- Xtream channel/movie/series arrays are decoded one record at a time instead of retaining a full JSON tree alongside the catalog.
- Catalog caches stream compressed JSON through 64 KiB AES-GCM chunks. Each chunk is bound to its file nonce and sequence; an authenticated end marker detects truncation. Sources/favorites retain their existing encrypted storage format.
- Cache saves use one conflated queue, preventing simultaneous catalog/guide serialization. Forced refresh skips loading the old cache. Old caches larger than 8 MiB and unreadable caches are fetched again rather than repeatedly allocating them.
- Channel ID hashing avoids per-byte string formatting while preserving IDs and favorites.
- Version code 3, version 0.1.2. Fullscreen controls and bounded live-stream recovery from 0.1.1 are retained.

## Validation

The 50,000-channel storage stress test passed on a 192 MiB Java heap: import, encrypted save, full cache equality after reopening, and rejection of a truncated cache. The encrypted compressed fixture cache was 2,816,529 bytes. All 19 JVM tests and all 13 Android instrumentation scenarios passed across the full device run and the isolated remote rerun (32 scenarios total). Lint reported zero errors. The TV UI test loaded 50,000 channels and displayed the last entry through search.

The first device runs exposed test issues: a non-void stress-test declaration, a search assertion matching both the field and result, and QR navigation interference from saved fixture sources whose servers had stopped. These were corrected; the final isolated four-test remote run passed. The full device report and successful remote rerun are retained separately in `dist/verification-0.1.2/`.

APK signature, version code 3, and SHA-256 were verified. The signing certificate matches 0.1.1.

Install the new APK over the existing application to retain sources and favorites. Tests use synthetic local fixtures and an Android TV Android 14 ARM64 emulator; they do not verify the user’s subscriber catalog or physical TV.
