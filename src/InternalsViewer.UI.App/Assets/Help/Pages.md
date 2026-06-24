# Page View

The Page view shows a single 8 KB page byte for byte, alongside a full decode of what
those bytes mean. Where the [Allocations](Allocations.md) view is the map of the database,
the Page view is the detail of a single location.

> **Image placeholder — page-overview**
> The full Page view: hex viewer on the left, the slot/offset table in the middle, and the page details on the right.
>
> _To be added. Source: `temp-screenshots/Page_view.png`; crop: trim OS title bar, keep full width._

> **Concept: the page**
> A **page** is the fundamental unit of storage in SQL Server — exactly **8 KB (8192
> bytes)**. Table rows, index entries, allocation maps and the database boot record all
> reside on pages, and the engine always reads and writes whole pages. This view shows one
> page at a time.

> **Concept: the anatomy of a page**
> Regardless of what it stores, every page has the same structure:
> - a **96-byte header** describing the page (its type, owning object, row count, free
>   space, links to neighbouring pages);
> - a body of **data** following the header;
> - a **slot / offset array** growing backwards from the end of the page, where each entry
>   records where a row begins, allowing variable-length rows to be located without
>   scanning.
>
> Page *types* fill the body differently, which is why the decode on the right changes
> between pages.

The view has four parts: the **Command Bar**, the **Hex Viewer**, the **Offset Table**,
and the **Details** pane.

## Command Bar

- **Page Address** — the current page, as `FileId:PageId` (e.g. `1:9936`).
  - Enter a different address to navigate to another page.
  - **Right-click** for options such as the matching **`DBCC PAGE`** command.
- **Back** / **Forward** — step to the previous / next page by id (page id −1 / +1).
- **Refresh** — reloads the current page.
- **Index** — if the page belongs to a clustered or non-clustered index, opens the
  [Index](Indexes.md) view for it.
- **Page Details** — the owning **Object Type**, **Object Id** and **Index Id**.

## Hex Viewer

The left pane is the raw page: all 8192 bytes shown as hex.

> **Image placeholder — page-hex**
> Close-up of the hex viewer with bytes colour-coded by component.
>
> _To be added. Source: `temp-screenshots/Page_view.png`; crop: the hex pane on the left._

- Bytes are **colour-coded** to indicate the currently selected item and its components.
- **Hover** over bytes to see the page offset and what the data represents, shown along
  the bottom of the hex viewer.
- **Select bytes** with the text caret to decode them into different data types — useful
  for determining what a stretch of raw bytes represents.

> **Image placeholder — page-decode**
> A data page with a slot selected: highlighted bytes in the hex viewer and the matching row decode on the right.
>
> _To be added. Source: `temp-screenshots/Page_view_with_slot_selected_and_data_decode.png`; crop: trim OS title bar, keep full width._

> **Note:** The Details pane decodes the page for you, so reading hex directly is rarely
> necessary. It is useful for confirming how a row's null bitmap, fixed-length columns and
> variable-length columns align against the bytes on disk.

## Offset Table

The middle pane lists the selectable parts of the page: the **header** and each **slot**
(row), with each slot's offset shown as both a number and hex.

- **Click a slot** to highlight it in the hex viewer and select it in the Details pane.
- On pages with **row or page compression**, the **Compression** elements also appear here
  as selectable options.

> **Concept: the slot**
> A **slot** is a row's position on the page. Because rows can vary in length, SQL Server
> keeps the slot array at the end of the page as an index of where each row starts
> (row 0 at offset X, row 1 at offset Y, and so on). Selecting a slot selects a specific
> row to decode.

## Details

The right pane decodes the selected item. The content depends on the **page type**.

### Data and Index pages

- A full **decode of the row** — internal bytes and column values — shown as **Name (a
  description of the component) / Value**.
- Descriptions of **status flag bits** and other decoded flags (for example, compression
  settings).
- **Click an item** to underline its bytes in the hex viewer and scroll to that offset.
- Encoded **page addresses** (down-page pointers) and **row identifiers (RIDs)** can be
  clicked to open that page.
  - **Hold Shift** to open the page in a new tab.

### Allocation pages (IAM, GAM, SGAM, PFS)

Some pages hold SQL Server's allocation bookkeeping rather than user data. For these, the
Details pane shows the allocation header plus a small map view of the page's bitmap.

> **Image placeholder — page-iam**
> An IAM page: the IAM Header decode with its single-page slots and start page.
>
> _To be added. Source: `temp-screenshots/Page_view_allocation_page.png`; crop: trim OS title bar, keep full width._

> **Image placeholder — page-iam-map**
> The Allocations tab of an allocation page, showing the bitmap rendered as a map with a tooltip.
>
> _To be added. Source: `temp-screenshots/Page_view_allocation_page_with_map.png`; crop: trim OS title bar, keep full width._

- **Click the allocation map** to open that page in the viewer (**Shift** for a new tab).

> **Concept: GAM, SGAM, IAM, PFS — the allocation maps**
> SQL Server tracks free and used space with several bitmap pages:
> - **GAM** (Global Allocation Map) — which extents are free vs. allocated.
> - **SGAM** (Shared GAM) — which extents are mixed and have free pages.
> - **PFS** (Page Free Space) — per-page allocation status and how full each page is.
> - **IAM** (Index Allocation Map) — which extents belong to a given table or index.
>
> These pages let the engine find space quickly. Opening them here shows the bitmaps that
> drive the [Allocations](Allocations.md) map.

## See also

- [Allocations](Allocations.md) — click any page on the map to open it here.
- [Indexes](Indexes.md) — Shift + click a page in the index tree to inspect it here.
