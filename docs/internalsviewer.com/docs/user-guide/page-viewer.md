# Page Viewer

The Page Viewer displays a single 8 KB database page, decoded.

It has three parts:

- **Raw data** - the 8192 bytes of the page in hexadecimal, colour coded to show where each structure is
- **Slots** - the page's slot offset array, listing the Page Header and each record with its offset
- **Decoded information** - the fields of the selected structure and their values

When a page opens the Page Header is selected. Clicking a slot decodes that record; clicking a field in the decoded information highlights its bytes in the raw data.

## Opening pages

Pages can be opened by:

- Clicking a page on the [Allocation Map](/docs/introduction/database-view)
- Clicking an entry point (Root Page / First Page / First IAM Page) in the Allocation Info table
- Clicking any page address link, formatted as `(File Id:Page Id)`, anywhere in the application
- Typing an address into the page address box and pressing enter - `(1:704)`, `1:704`, and `704` (File 1 assumed) are all accepted

::: tip
Page address links open in the same tab. **Shift + click** opens the page in a separate tab - useful for keeping the current page open while following a pointer.
:::

The Back and Forward buttons move through the history of viewed pages, and Refresh re-reads the page from the database.

## Navigating

Page addresses in the decoded information are links - `Next Page` and `Previous Page` in the header, down page pointers and RIDs in records - so a page chain or an index can be walked by clicking through.

The **Index** button opens the index the current page belongs to in the [Index Viewer](/docs/introduction/index-viewer), showing the page in the context of its B-Tree.

## Decoding data

Selecting a range of bytes in the raw data shows a decode of those bytes in the applicable data types, and hovering over highlighted bytes shows a tooltip with the offset range and decoded value.

The status bar at the bottom shows the current offset and which structure the cursor is over.

See the [Reference](/docs/reference/page-header) section for the structures the Page Viewer decodes - the [Page Header](/docs/reference/page-header), [Data Records](/docs/reference/data-records), [Index Records](/docs/reference/index-records), and [Compression](/docs/reference/compression) structures.
