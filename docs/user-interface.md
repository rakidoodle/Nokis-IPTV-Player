# User interface

## Application shell

The WPF shell contains three persistent areas:

- a left navigation rail for Home, Live TV, Movies, Series, Favorites, Guide, and Settings;
- a header with library search and a theme switch;
- a status/player strip along the bottom.

The center swaps ViewModels through `INavigationService`. WPF selects the matching View through typed data templates, keeping navigation logic out of code-behind.

## Responsive behavior

At widths of 960 device-independent pixels or more, the sidebar is 232 pixels wide and displays icons with labels. Below 960 pixels, it becomes an 80-pixel icon rail. The application has a minimum size of 760 by 560 so primary controls remain usable.

## Themes

Colors are semantic resources rather than hard-coded control colors. `Theme.Dark.xaml` and `Theme.Light.xaml` define the palettes, while `Styles.xaml` defines reusable focus, button, navigation, search, and card styles. Theme selection is saved in the non-sensitive settings file and restored during startup.

## Accessibility

- Navigation uses a standard `ListBox`, supporting Tab and arrow-key operation.
- Interactive controls have explicit accessible names and tooltips where useful.
- Focused controls receive a high-contrast accent outline.
- `Ctrl+F` moves focus directly to global search; `Escape` releases it.
- Check boxes and sliders use the same visible focus treatment as buttons and fields.
- Pages expose level-one headings and readable empty-state descriptions.
- Text and controls use scalable WPF device-independent units.
- Dark and light semantic palettes use strengthened secondary text contrast.

## Content states

`StatePanel` provides empty, loading, and error presentations. Provider screens currently display intentional empty states because profile management and IPTV import are implemented in later phases.
