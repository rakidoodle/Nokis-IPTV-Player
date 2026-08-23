# Live TV browser

The Live TV screen combines four areas:

- Category list with per-category channel counts
- Channel list for the selected category
- LibVLC video player and transport controls
- Current/next program information when EPG data is available

Choose a category, choose a channel, and select **Play selected channel**. Categories named only with whitespace are shown as **Uncategorized**. Category matching is case-insensitive, so provider values such as `News` and `news` are grouped together.

Both lists use WPF UI virtualization in recycling mode. Only the rows visible on screen have visual controls, which prevents a playlist containing tens of thousands of entries from creating tens of thousands of WPF elements. Filtering produces lightweight arrays of existing channel records and never performs network work.

The channel's stream URL is passed directly through the provider-independent playback boundary. It is redacted from `ToString()` output and is never displayed or logged. When an XMLTV guide is loaded, the selected channel's EPG ID supplies current and next program titles; readable placeholders remain when no mapping or program is available.
