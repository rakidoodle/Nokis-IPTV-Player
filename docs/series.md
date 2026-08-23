# Series and episodes

The Series screen browses shows already published by a connected provider. Selecting a show loads its detailed episode catalog on demand, then groups episodes into numbered seasons.

## Using the browser

1. Connect an authorized Xtream profile from Settings.
2. Open **Series** and choose a show.
3. Choose a season and episode.
4. Select **Play episode** to start from the beginning, or **Continue** when saved progress is available.

The details panel shows provider-supplied artwork, release information, rating, synopsis, and episode metadata when available. A show can be added to Favorites using its stable provider identity. Episode progress is refreshed from local history without changing the selected season or episode.

## Loading and cancellation

The initial series list is read from the in-memory media catalog. Full episode details are requested through the matching `IContentProvider` only when they are not already loaded. Changing shows cancels an obsolete request, and a failed or empty provider response is shown as a friendly status instead of partially replacing the current catalog.

## Privacy

Favorites and watch history store profile/content identifiers, display titles, timestamps, and playback progress. They do not store episode stream URLs, passwords, access tokens, or complete credential-bearing requests. Playback is limited to sources the user is authorized to access.
