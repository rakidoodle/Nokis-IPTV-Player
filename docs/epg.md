# XMLTV program guide

MyIPTV accepts an HTTP/HTTPS XMLTV address or an existing local `.xml`/`.xmltv` file on the Program Guide screen. Gzip-compressed HTTP responses and local `.gz` files are supported. The parser follows the XMLTV project's [documented DTD](https://github.com/XMLTV/xmltv/blob/master/xmltv.dtd): `programme` entries map through their `channel` attribute, start/stop times are half-open intervals, and a missing numeric timezone is treated as UTC.

## Channel mapping

Live channels map to XMLTV by their playlist/provider EPG ID (`tvg-id` for M3U). If no EPG ID is supplied, MyIPTV tries the channel display name. For dependable mapping, make the channel's EPG ID exactly match the XMLTV `<channel id="…">` and `<programme channel="…">` value.

The parser reads channel display names plus program title, description, start, and stop. If `stop` is absent, the next program's start is used; a final program without a stop receives a conservative 30-minute duration. Invalid entries are skipped without discarding valid entries. Numeric offsets, UTC/GMT, and BST are recognized. Unknown timezone abbreviations are skipped because silently guessing could display a guide at the wrong time.

## Cache and refresh behavior

Parsed channels and programs are cached in SQLite. The Guide screen currently uses a six-hour refresh interval. A refresh before expiry reads the cache without downloading the source again. After expiry, HTTP sources use `ETag` and `Last-Modified` validators when supplied; a `304 Not Modified` response extends the cache. If a refresh fails, existing cached data stays available.

The parser streams XML, prohibits DTD/entity expansion, limits decompressed input to 64 MiB, and converts every accepted timestamp to UTC. The UI converts UTC to the current Windows local timezone for display. HTTP framework logging is disabled for the dedicated EPG client; only a SHA-256 source identifier and aggregate counts are logged, never the source address or query string.

For safe local testing, enter the absolute path to `samples\demo-guide.xml`. The fixture uses non-functional synthetic channel and program names.
