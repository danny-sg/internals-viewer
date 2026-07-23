# Allocations

The Query view's Allocations pane is the same [Allocation Map](/docs/user-guide/allocations) as the Database view, scoped to the pages the query touches and linked to the [Timeline](/docs/user-guide/query/Timeline) - as the replay runs, pages light up as they are read.

![Allocations pane during query replay](/docs/user-guide/images/query-view-query-allocations-cropped.png)

## Command bar

- **Buffer Pool** / **PFS** - overlay the page's buffer pool or PFS status on the map, same as the [Database view overlays](/docs/user-guide/allocations#overlay)
- **Heatmap** - shades pages by the intensity of repeated reads, mostly useful for spotting pages hit repeatedly from the buffer pool
- **Auto-Scroll** - scrolls the map to follow the page location of the current event as the timeline plays

::: warning
Auto-Scroll can cause rapid flickering as the view jumps between distant pages - turn it off if you're photosensitive.
:::

## Lock borders

When locks are [captured](/docs/user-guide/query#events-menu), a border is drawn around each locked page showing its lock status at the playhead's current time. The border colour matches the lock's category colour - see [Locks](/docs/user-guide/query/Locks#categories-and-colours).

Intent lock modes are dimmed, same as on the Lock band. If a page has more than one lock type held on it, the most exclusive type is shown.
