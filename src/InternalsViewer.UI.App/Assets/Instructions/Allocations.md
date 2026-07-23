# Allocations

The [Allocations](view:Allocations) pane is the allocation map scoped to the query - each block is a page, and as the
replay runs, pages light up at the moment they are read. A scan sweeps through the table's extents in order, while a
seek touches a scattered handful of pages.

- **Ctrl + mouse wheel** zooms from the whole file down to individual pages
- **Buffer Pool** and **PFS** overlay page status on the map
- **Heatmap** shades pages by how often they were read - useful for spotting pages hit repeatedly from the buffer pool
- **Auto-Scroll** follows the current event as the timeline plays
- When locks are captured, locked pages are drawn with a border coloured by lock category
