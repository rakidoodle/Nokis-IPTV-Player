# Accessibility

MyIPTV uses native WPF controls so Windows keyboard navigation and UI Automation semantics remain available to assistive technology. The main navigation, lists, fields, player controls, and action buttons expose descriptive accessible names. Page titles are marked as level-one headings.

## Keyboard use

- Press `Tab` and `Shift+Tab` to move between controls.
- Use arrow keys to move within navigation and content lists.
- Press `Enter` or `Space` to activate the focused control.
- Press `Ctrl+F` to focus global search and `Escape` to leave it.
- Use the labelled full-screen control while media is playing; `Escape` exits full screen.

All custom interactive styles draw a two-pixel accent focus outline. Check boxes and sliders have a minimum 28 device-independent-pixel focus area.

## Display

The layout uses device-independent units and standard WPF font scaling. The sidebar collapses at narrower window sizes, text wraps on descriptive surfaces, and both themes use semantic foreground/background colors reviewed for readable contrast.

## Known limits

Video captions and audio descriptions depend on tracks supplied by the authorized media source. MyIPTV exposes available subtitle and audio tracks but cannot create missing accessibility media.
