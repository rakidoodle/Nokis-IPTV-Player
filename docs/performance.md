# Performance

MyIPTV is designed to remain responsive with playlists containing tens of thousands of entries.

## Current safeguards

- M3U and XMLTV inputs are parsed asynchronously from streams with cancellation and explicit size limits.
- Search waits for a 300 ms debounce, cancels obsolete requests, snapshots catalogs off the WPF thread, and retains only the top requested results while scanning.
- Live TV builds its category index once per catalog publication; choosing a group reuses the indexed array.
- Provider catalogs are published atomically so screens never observe partially loaded datasets.
- ListBox-based catalogs use logical scrolling, recycling virtualization, and bounded detail panels.
- SQLite guide queries use bounded time windows and indexed channel/time columns.

## Verification

Automated development fixtures parse 20,000 M3U entries and search 50,000 catalog entries. The large search test requires 50 alphabetically ranked results within a conservative five-second ceiling while the overall test runner enforces a ten-second timeout. These ceilings catch obvious regressions without pretending to replace profiling on target Windows hardware.

Future optimizations should be driven by measured UI, allocation, or database traces. More complex incremental paging is appropriate only when provider size or device measurements show the existing snapshot-and-virtualize design is insufficient.
