# Database

MyIPTV uses a versioned SQLite database at `%LocalAppData%\MyIPTV\myiptv.db`. Embedded migrations are ordered by numeric prefix, applied transactionally, and recorded in `schema_migrations`, so an existing installation can advance without recreating user state.

## State tables

- `profiles` — connection type and non-secret profile metadata;
- `channels`, `movies`, `series`, `episodes` — safe provider catalog metadata and indexed browse fields;
- `favorites` — stable profile/content keys and display title;
- `watch_history` — stable keys, playback timestamps, position, and duration;
- `epg_channels`, `epg_programs`, `epg_cache` — bounded XMLTV cache and refresh metadata;
- `settings` — serialized non-sensitive application preferences;
- `app_metadata` and `schema_migrations` — database/application version state.

Foreign keys cascade profile deletion into owned catalog, favorite, and history rows. Browse and guide indexes cover profile/category/name, series/season/episode ordering, and EPG channel/time windows.

## Catalog persistence boundary

Runtime catalogs contain playback addresses so the player can open an authorized stream. Some provider protocols embed a username and password in those paths. The database schema therefore has no stream, playback, poster, or logo URL columns. The background persistence coordinator copies only identifiers and descriptive metadata after a catalog changes.

Saved metadata supports durable state, diagnostics, and future offline catalog evolution, but MyIPTV reconnects to the provider after restart before playback so it can reacquire current authorized addresses. Passwords and tokens remain in DPAPI-protected credential files, never in SQLite.

## Preferences

SQLite is the primary settings record. The atomic `settings.json` file remains a non-sensitive compatibility fallback and recovery copy. Both representations are restricted to display, playback, EPG, and navigation preferences; neither may contain provider credentials.
