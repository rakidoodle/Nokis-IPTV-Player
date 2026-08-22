# IPTV profiles

The Profiles screen manages IPTV sources that you own or are authorized to access.

## Supported profile types

- **M3U Playlist** accepts a local `.m3u` or `.m3u8` file, or a remote HTTP/HTTPS playlist URL.
- **Xtream API** accepts an HTTP/HTTPS server address, username, and password.
- **Stalker / Ministra Portal** accepts an HTTP/HTTPS portal address and optional username and password.

URLs containing embedded credentials or sensitive query parameters are rejected. Enter credentials in the dedicated fields instead.

## Using the profile manager

1. Open **Profiles** from the navigation sidebar.
2. Select **Add** and enter a descriptive profile name.
3. Choose the connection type and complete its fields.
4. Select **Test Connection** to run a safe diagnostic.
5. Select **Save** to keep the profile metadata, or **Connect** to save, test, and mark it as the active profile for the current run.

Select a saved profile to edit, test, connect, or delete it. Deletion requires confirmation.

## Connection diagnostics

- A local M3U test verifies that the file exists and starts with the required `#EXTM3U` header.
- A remote M3U test requests the playlist and verifies its header.
- Xtream and Stalker/Ministra tests currently verify base-server reachability only. Provider authentication and catalog requests arrive in later provider phases.

Diagnostics report friendly errors and do not write addresses, usernames, passwords, or response bodies to logs.

## Phase 5 credential behavior

Profile names, types, server addresses, and usernames are stored in SQLite. Passwords are kept only in memory for the running application and are never stored in SQLite, settings files, or logs. Closing MyIPTV clears them, so they must be entered again after restart.

Phase 6 will replace the temporary in-memory credential service with Windows-protected persistent storage.
