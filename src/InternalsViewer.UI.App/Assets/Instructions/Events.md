# Events

The [Events](view:Events) pane is the raw data behind the timeline - every captured event in a list, in time order.
This is where to find the precise detail behind a tick on the timeline: which page a read touched, what lock mode was
taken, or what the engine was waiting on.

- The search box filters across every field, and columns sort by clicking their headers
- Multi-page reads have an expander showing the individual pages within the read
- Page addresses are links - click to open the page as a tab in this view, or **Shift + click** for a separate
  top-level tab
- The status bar at the bottom counts the events by type
- Selecting a time range on the timeline highlights the events inside it
