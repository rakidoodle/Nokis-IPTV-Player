# IPTV profiles

The Profiles screen manages IPTV sources that you own or are authorized to access.

## Supported profile types

- **M3U Playlist** accepts a local `.m3u` or `.m3u8` file, or a remote HTTP/HTTPS playlist URL.
- **Xtream API** accepts an HTTP/HTTPS server address, username, and password.
- **Stalker / Ministra Portal** accepts an HTTP/HTTPS portal address plus the username and password issued for a supported subscriber REST connection.

M3U URLs may contain the common `username` and `password` query parameters supplied by a provider. MyIPTV removes those parameters before saving profile metadata, protects the credentials with Windows DPAPI, and reconstructs the request only when editing, testing, or loading that profile. URL user-info and token/key parameters remain rejected. Xtream and Stalker credentials belong in the dedicated fields.

## Using the profile manager

1. Open **Profiles** from the navigation sidebar.
2. Select **Add** and enter a descriptive profile name.
3. Choose the connection type and complete its fields.
4. Select **Test Connection** to run a safe diagnostic.
5. Select **Save** to keep the profile metadata, or **Connect** to save, authenticate where applicable, load the provider catalog, and activate it. Provider loading can be canceled while it is running.

Select a saved profile to edit, test, connect, or delete it. Deletion requires confirmation.

## Connection diagnostics

- A local M3U test verifies that the file exists and starts with the required `#EXTM3U` header.
- A remote M3U test requests the playlist and verifies its header.
- Connecting an M3U profile performs the full parse and reports imported, malformed, and duplicate entry counts.
- An Xtream quick test verifies base-server reachability. **Connect** performs authenticated catalog loading.
- A Stalker/Ministra quick test verifies base-server reachability. **Connect** uses the supported username/password REST v2 interface. Device-bound or MAC-based portal access is deliberately rejected; see [Stalker / Ministra compatibility](stalker-ministra.md).

Diagnostics report friendly errors and do not write addresses, usernames, passwords, or response bodies to logs.

## Credential security

Profile names, types, server addresses, and usernames are stored in SQLite. Passwords are never stored in SQLite, settings files, or logs. They are serialized into a separate credential file and encrypted with Windows Data Protection API (DPAPI) using the current Windows user account.

The same Windows account on the same Windows installation can decrypt the credentials after MyIPTV restarts. Other Windows accounts cannot. Moving the encrypted files to another computer or losing the Windows user profile normally makes them unrecoverable, in which case enter the password again. Deleting a profile removes its protected credential file.
