# M3U and M3U8 playlists

MyIPTV can import a local `.m3u`/`.m3u8` file or a remote HTTP/HTTPS playlist that you are authorized to use.

## Importing

1. Open **Profiles** and add an **M3U Playlist** profile.
2. Enter a remote URL or use **Browse** to choose a local playlist.
3. Select **Test Connection** for a quick header check.
4. Select **Connect** to parse and import all playable channel entries.
5. Use **Cancel Import** if a long-running import should stop.

The status panel reports the imported channel count and how many malformed or duplicate entries were skipped. The Phase 7 catalog is held in memory, so reconnect the profile after restarting MyIPTV. Channel browsing is added in Phase 11.

## Supported metadata

The parser recognizes:

- `#EXTM3U`
- `#EXTINF`
- `tvg-id`
- `tvg-name`
- `tvg-logo`
- `group-title`

Quoted values, commas inside quoted values, UTF-8 byte-order marks, missing optional metadata, and extra M3U directives are handled. If a channel has no name, MyIPTV assigns a neutral `Channel N` name. Missing groups become `Uncategorized`.

Common HTTP, HTTPS, RTMP, RTSP, UDP, RTP, and SRT stream addresses are recognized. Playback support still depends on the media engine introduced in Phase 10.

## Safety and performance limits

- Playlist content is read asynchronously and incrementally.
- Imports accept up to 100,000 playable channels and 64 MiB of playlist text.
- Individual lines longer than 32,768 characters are treated as malformed.
- Exact duplicate stream addresses are imported once.
- Stable channel IDs are derived with SHA-256 from the profile, EPG ID, and stream address.
- Stream addresses and channel metadata are never written to logs.
- Cancellation tokens stop local parsing and remote downloads cooperatively.

Malformed entries are skipped without crashing the import. A missing header, oversized playlist, inaccessible source, or playlist with no playable channels produces a friendly error.
