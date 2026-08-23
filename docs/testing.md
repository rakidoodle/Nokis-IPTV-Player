# Testing

The automated suite uses MSTest and only local or synthetic fixtures. It does not contact a real IPTV subscription or depend on an external service.

Run the complete suite from the repository root:

```powershell
dotnet test MyIPTV.sln
```

## Required coverage matrix

| Area | Representative verification |
|---|---|
| M3U parsing | metadata, missing metadata, malformed entries, duplicate streams, missing header, stable IDs, cancellation, 20,000 entries |
| Playlist loading | local and fake-HTTP streams, invalid/empty playlist result |
| Xtream mapping | authentication, categories, live/VOD/series DTO mapping, episode mapping, malformed JSON |
| Ministra mapping | REST authentication, playable channels, unsupported legacy portal behavior |
| XMLTV | numeric timezone parsing, inferred stop time, invalid records, XXE/DTD prohibition, cache/now-next |
| Database | migrations, profiles, catalog replacement, favorites, history, EPG, settings fallback, secret-free schema |
| Search/filter | every content kind, prefix ranking, limits, cancellation, 50,000-entry catalog, case-insensitive category filtering |
| Favorites/history | composite stable IDs, restart persistence, removal, resume position, coordinator checkpoints |
| URL validation | credentials in query/user-info, unsupported schemes, relative addresses, valid local playlist path |
| Credential redaction | DPAPI ciphertext, damaged credential behavior, redacting models, log sanitization, metadata-only catalog storage |
| UI logic | navigation, start page, theme, responsive sidebar, player controls, EPG, movies, series, Home summaries |

Tests use `example.invalid` or fake message handlers for non-functional addresses. Performance ceilings are deliberately conservative so normal CI variability does not create false failures. Release validation reruns this same suite using the Release configuration.
