# Recently watched and continue watching

MyIPTV records a compact local history when playback starts. Each entry contains the profile ID, content type, provider content ID, display title, last-watched time, and—where appropriate—the playback position and duration. Playback URLs, credentials, tokens, channel logos, program descriptions, and other unnecessary personal data are not written to history.

VOD progress is saved on playback state changes, every 15 seconds while media is playing, and once more during graceful application shutdown. The player keeps the last known time before a stop or failure so stopping playback does not reset saved progress to zero.

The Home screen lists up to ten recent items. A loaded movie or episode with saved progress offers **Continue watching** and resumes at that position. Live channels and completed or zero-position items offer **Watch again**. If the corresponding profile catalog is not currently loaded, the history remains visible but playback is disabled until the profile is reconnected.

History persists in `%LocalAppData%\MyIPTV\myiptv.db`. Deleting a profile removes its history. The Settings data controls added in Phase 18 provide a **Clear watch history** operation.
