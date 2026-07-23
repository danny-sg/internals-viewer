# Page Viewer

The Page Viewer displays a single 8 KB database page, decoded.

![Page Viewer with the Page Header selected](/docs/user-guide/images/page-view-header-selected.png)

It has three parts:

- **Raw data** - the 8192 bytes of the page in hexadecimal, colour coded to show where each structure is
- **Slots** - the page's slot offset array, listing the Page Header and each record with its offset
- **Decoded information** - the fields of the selected structure and their values

When a page opens the Page Header is selected. Clicking a slot decodes that record, and clicking a field in the decoded information highlights its bytes in the raw data. Clicking a selected slot again deselects it.

The header bar shows which object the page belongs to - the table, the index name and type, and the Object and Index Ids. Clicking an Id copies it to the clipboard.

## Opening pages

Pages can be opened by:

- Clicking a page on the [Allocation Map](/docs/user-guide/allocations)
- Clicking an entry point (Root Page / First Page / First IAM Page) in the Allocation Info table
- Clicking any page address link, formatted as `(File Id:Page Id)`, anywhere in the application
- Typing an address into the page address box and pressing enter - `(1:704)`, `1:704`, and `704` (File 1 assumed) are all accepted

::: tip
Page address links open in the same tab. **Shift + click** opens the page in a separate tab - useful for keeping the current page open while following a pointer.
:::

The Back and Forward buttons move through the history of viewed pages, and Refresh re-reads the page from the database.

::: tip Finding the page for a row
To go from a row to its page, the (undocumented) `%%physloc%%` virtual column returns the row's physical location, and `sys.fn_PhysLocFormatter` formats it as `(File Id:Page Id:Slot Id)`:

```SQL
SELECT sys.fn_PhysLocFormatter(%%physloc%%) AS RowLocation
      ,*
FROM   dbo.HeapTable
```

Paste the address into the page address box to open the page, and the Slot Id picks out the row's record.
:::

## Navigating

Page addresses in the decoded information are links - `Next Page` and `Previous Page` in the header, down page pointers and RIDs in records - so a page chain or an index can be walked by clicking through.

The **Index** button opens the index the current page belongs to in the [Index View](/docs/user-guide/index-view), showing the page in the context of its B-Tree.

::: tip
Right-clicking the page address box gives some extra shortcuts:

- **Copy DBCC PAGE command to clipboard** - builds a ready-to-run `DBCC PAGE` command for the current page, with a submenu for each dump option (0 to 3), for comparing with what SQL Server itself reports
- **Page + 1** / **Page - 1** - step to the physically adjacent page in the file
:::

## Decoding data

![Page Viewer with a record slot selected](/docs/user-guide/images/page-view-slot-selected.png)

Selecting a range of bytes in the raw data shows a decode of those bytes in the applicable data types, each with a copy button. Hovering over highlighted bytes shows a tooltip with the offset range and decoded value.

The status bar at the bottom shows the current offset and which structure the cursor is over.

Values in the decoded information also have a copy button, and pointer values (page addresses and RIDs) are links.

See the [Reference](/docs/reference/page-header) section for the structures the Page Viewer decodes - the [Page Header](/docs/reference/page-header), [Data Records](/docs/reference/data-records), [Index Records](/docs/reference/index-records), and [Compression](/docs/reference/compression) structures.

## PFS pages

Opening a PFS page adds a **PFS** tab alongside **Page Header**, rendering the PFS byte for every page the PFS page covers - up to 8088 pages (see [PFS](/docs/user-guide/allocations#pfs-page-free-space)):

![PFS page with the PFS tab selected](/docs/user-guide/images/page-view-pfs-page-cropped.png)

This is the same overlay used on the Allocation Map, scoped to just this PFS page's range - useful for confirming exactly which page a PFS byte belongs to.

Other allocation page types - IAM, GAM, SGAM, DCM, BCM - similarly add an **Allocations** tab that renders their bitmap. See [Allocation pages](/docs/tutorial/2-viewing-pages#step-6-allocation-pages) in the tutorial for a walkthrough.

## Log operations

When a page is opened from a query traced with **Trace** mode on, a **Log Operations** panel shows the transaction log records that changed the page, with the ability to apply and unapply them to see the page's history. See [Log Records](/docs/user-guide/query/LogRecords) in the Query section.
