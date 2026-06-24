# Allocations / Database View

This is a map of the whole database. On connection, Internals Viewer displays every page
in every data file, colour-coded by the object (table or index) that owns it. It shows
how a database is physically laid out — where objects reside, how much space they occupy,
and how fragmented they are.

> **Image placeholder — allocation-overview**
> The full Allocations view: command bar, allocation map, a tooltip, and the details grid below.
>
> _To be added. Source: `temp-screenshots/Database_allocations_with_tooltip.png`; crop: trim OS title bar, keep full width._

> **Concept: pages and extents**
> SQL Server stores everything in fixed-size chunks called **pages**. A page is **8 KB** —
> the smallest unit the engine reads or writes. Eight contiguous pages (64 KB) form an
> **extent**, and SQL Server allocates space mostly an extent at a time. Each coloured
> square in the map is a page; the map is a picture of how pages and extents have been
> allocated.

> **Concept: allocation**
> *Allocation* is the reservation of pages for an object. As a table grows, SQL Server
> allocates more pages and extents to it. This view reads SQL Server's allocation
> bookkeeping (the GAM, SGAM, IAM and PFS maps) and renders it visually, so a region of
> one colour is a run of pages belonging to the same index.

The view has three parts: the **Command Bar**, the **Allocation Map**, and the
**Allocation Details** grid.

## Command Bar

> **Image placeholder — allocation-command-bar**
> The command bar row: Tooltip, Allocation Info, PFS, Query, Refresh toggles plus the page-address box on the right.
>
> _To be added. Source: `temp-screenshots/Database_allocations_with_tooltip.png`; crop: full width, command bar row only._

| Control | What it does |
| --- | --- |
| **Tooltip** | Toggles hover tooltips on the map. When on, hovering over a page shows the object name, page and extent numbers, and PFS status. |
| **Allocation Info** | Shows or hides the Allocation Details grid at the bottom. |
| **PFS** | Overlays **Page Free Space** information on the map (see the PFS panel below). |
| **Refresh** | Re-reads the allocations from the database. |
| **Query** | Opens the [Query](Query.md) view. *(SQL Server connections only.)* |
| **Page address box** *(top right)* | Enter a page address as `FileId:PageId` (e.g. `1:9936`) and press Enter to open that page in the [Page](Pages.md) view. Right-click for **Copy DBCC Command to Clipboard**, which copies the `DBCC PAGE` command for the entered address. |

## Allocation Map

The large coloured area is the map. Each small square is one **page**; runs of the same
colour are pages belonging to the same table or index. The **colour matches the Key**
shown in the details grid below, identifying the owner of each region.

- **Zoom** with **Ctrl + mouse wheel**.
- **Scroll** with the scroll bar or mouse wheel.
- **Click a page** to open it in the [Page](Pages.md) view.
- Selecting an object in the details grid **highlights the scroll bar** to indicate where
  that object's pages are located.

> **Image placeholder — allocation-tooltip**
> Close-up of a map tooltip showing Page, Extent, PFS and object name.
>
> _To be added. Source: `temp-screenshots/Database_allocations_with_tooltip.png`; crop: tight around the tooltip with some map context._

### Reading fragmentation

Because the map is laid out in physical page order, fragmentation is visible directly: an
object whose pages form a few solid blocks is contiguous, while one whose colour is
scattered across the map is fragmented.

### The PFS overlay

Enabling **PFS** overlays page-fullness and IAM information on the map.

> **Image placeholder — allocation-pfs**
> The map with the PFS overlay on, showing the "I" IAM markers and the lighter free-space shading on pages.
>
> _To be added. Source: `temp-screenshots/Database_allocations_with_pfs_zoomed.png`; crop: the map area showing several "I" markers._

> **Concept: PFS (Page Free Space)**
> SQL Server maintains **PFS** pages that record, for every page, roughly how full it is
> and whether it is allocated. The engine uses this to find a page with room for an
> insert. In the overlay, a lighter shaded band on each page represents its free space —
> fuller pages show less shading. The tooltip states it precisely, e.g.
> `0x40 Allocated | 0% Full`.

> **Concept: IAM pages and the "I" markers**
> An **IAM (Index Allocation Map)** page is a bookkeeping page that tracks which extents
> belong to a particular table or index within a region of the file. Each object has a
> *chain* of IAM pages, which is how SQL Server records ownership of scattered extents. In
> the PFS overlay, IAM pages are marked with an **"I"**.

## Allocation Details

The grid at the bottom lists every object that has pages in the database, one row per
index (or heap).

> **Image placeholder — allocation-grid**
> The details grid with its columns and the Filter box.
>
> _To be added. Source: `temp-screenshots/Database_allocations_with_tooltip.png`; crop: full width, grid region only._

- **Filter** results with the box at the top right.
- **Sort** by selecting a column header.
- **Select** an object by clicking its row (highlighting its pages on the map); click
  again to deselect.

| Column | Meaning |
| --- | --- |
| **Key** | The colour used for this object on the allocation map. |
| **Object Name** | The table (or view) the data belongs to. |
| **Index Name** | The specific index, or the heap. |
| **Index Type** | **Heap**, **Clustered**, or **Non-Clustered** (see panel below). |
| **Page Count** | Total number of pages allocated to the object. |
| **Root Page** | The index's root (entry) page — click to open it in the [Page](Pages.md) view. |
| **First Page** | The first data/leaf page — click to open it. |
| **First IAM Page** | The first page in the object's IAM chain — click to open it. |
| **Index** | Select **View** to open the object in the [Index](Indexes.md) view. |

> **Concept: heap vs. clustered vs. non-clustered**
> A **heap** is a table with no clustered index — its rows are stored in no particular
> order. A **clustered index** stores the table's rows in order of the index key, so the
> index *is* the table. A **non-clustered index** is a separate structure that points back
> to the rows. The type determines how an object's pages are laid out and linked, which
> can then be examined in the [Index](Indexes.md) view.

> **Concept: page addresses (`FileId:PageId`)**
> Page links throughout Internals Viewer use the form `(1:9936)` — file 1, page 9936. A
> database can have several files, so the file id identifies which file the page resides
> in. This notation appears in the grid, in tooltips, and in the [Page](Pages.md) and
> [Index](Indexes.md) views.
