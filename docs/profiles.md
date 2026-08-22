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
5. Select **Save** to keep the profile metadata, or **Connect** to save and activate it. For M3U profiles, **Connect** imports the channel catalog and can be canceled while it is running.

Select a saved profile to edit, test, connect, or delete it. Deletion requires confirmation.

## Connection diagnostics

- A local M3U test verifies that the file exists and starts with the required `#EXTM3U` header.
- A remote M3U test requests the playlist and verifies its header.
- Connecting an M3U profile performs the full parse and reports imported, malformed, and duplicate entry counts.
- Xtream and Stalker/Ministra tests currently verify base-server reachability only. Provider authentication and catalog requests arrive in later provider phases.

Diagnostics report friendly errors and do not write addresses, usernames, passwords, or response bodies to logs.

## Credential security

Profile names, types, server addresses, and usernames are stored in SQLite. Passwords are never stored in SQLite, settings files, or logs. They are serialized into a separate credential file and encrypted with Windows Data Protection API (DPAPI) using the current Windows user account.

The same Windows account on the same Windows installation can decrypt the credentials after MyIPTV restarts. Other Windows accounts cannot. Moving the encrypted files to another computer or losing the Windows user profile normally makes them unrecoverable, in which case enter the password again. Deleting a profile removes its protected credential file.
