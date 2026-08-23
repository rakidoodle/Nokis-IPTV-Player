# Movies and VOD

The Movies screen loads VOD supplied by an authorized Xtream profile. It provides virtualized category and movie lists, a poster/detail area, and an embedded LibVLC video surface.

When supplied by the provider, MyIPTV displays poster, title, category, year or release value, rating, description, and duration. Missing fields use readable placeholders. MyIPTV does not scrape or contact external metadata services.

Select a movie and choose:

- **Play** to start from the beginning.
- **Continue watching** to resume from locally saved progress; the button is enabled only when progress exists.
- **Add favorite** or **Remove favorite** to update the stable-ID favorite record.

Playback history stores the movie's stable profile/content identity and time position, not its stream URL. When playback starts or resumes, the credential-bearing URL stays inside the provider catalog and playback boundary. Diagnostic representations redact it.

Large catalogs use recycling virtualization and category filtering reuses lightweight movie card ViewModels. Provider updates, favorite changes, and background history checkpoints preserve the selected category/movie where possible.
