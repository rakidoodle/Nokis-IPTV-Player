# Favorites

Noki's IPTV Player can favorite live channels, movies, and series. Choose an item in its browser and select **Add favorite**. The button changes to **Remove favorite** when the selected item is already saved. Choosing a saved live-channel favorite opens Live TV and starts playback automatically.

Favorites are stored in the local SQLite database under `%LocalAppData%\MyIPTV\myiptv.db`. Each row uses the profile ID, content type, and provider content ID as a stable composite key. A display title and addition time are also stored; stream URLs, provider passwords, access tokens, and session data are not stored in the favorites table.

Deleting a profile removes its favorites. Ordinary application restarts do not remove favorites, although the corresponding provider profile must be connected again before its stream can be opened.
