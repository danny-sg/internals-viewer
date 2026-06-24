# Index View

This view renders an index as the tree it is, from the single root page at the top down
to the leaf level at the bottom. It shows the structure behind an index — the number of
levels, how the pages link together, and how the data is physically organised.

> **Image placeholder — index-tree**
> The index drawn as a tree: one root page at the top fanning out to rows of pages below.
>
> _To be added. Source: `temp-screenshots/Index_view.png`; crop: the tree area, root down through a few levels._

> **Concept: the B-tree index**
> SQL Server indexes are **B-trees**: a single **root** page at the top, optional
> **intermediate** levels, and the **leaf** level at the bottom holding the indexed data
> (or pointers to it). To find a row, the engine starts at the root and follows
> **down-page pointers** level by level — only a few page reads even in a large table.
> This view renders that structure.

> **Concept: the leaf level**
> For a **clustered** index, the leaf level *is* the table data, stored in key order. For
> a **non-clustered** index, the leaf level holds the index key plus a pointer back to the
> row — either a **RID** (row identifier, for a heap) or the **clustered key**. The detail
> pane exposes these pointers.

> **Warning: index size**
> Every non-leaf page in the index is decoded to render the tree, so avoid opening very
> large indexes. Use a smaller index (or a sample database) when examining structure.

The view has three parts: the **Command Bar**, the **Index View**, and the **Detail**
pane.

## Command Bar

> **Image placeholder — index-header**
> The header bar showing index name, table, index type, Object Id and Index Id.
>
> _To be added. Source: `temp-screenshots/Index_view.png`; crop: the header/detail strip at the top._

- **Refresh** — re-reads and redraws the index.
- **Details** — the index identity: **Index Name / Table Name**, **Index Type**,
  **Object Id** and **Index Id**.

## Index View (the tree)

A graphical view of the index from its root (entry point) at the top to the leaf level at
the bottom. The lines between pages represent the physical links — the down-page pointers
— so the diagram maps how the index is connected.

- **Zoom** with the mouse wheel.
- **Pan** using the scroll bars or by dragging.
- **Click a page** to open the Detail pane for it; the selected page is highlighted.
- Clicking it again hides the detail.

> **Image placeholder — index-selected**
> The tree zoomed out with one page selected/highlighted and the Detail pane open beside it.
>
> _To be added. Source: `temp-screenshots/Index_view_zoomed_out_with_page_selected.png`; crop: trim OS title bar, keep full width._

## Detail pane

The Detail pane decodes the rows on the selected page. The content depends on the level
and type of the page:

- On non-leaf pages, each row holds an index key and a **down-page pointer** to the page
  one level below.
- On non-clustered leaf pages, each row holds the key and a **RID** (row identifier)
  pointing to the row.

Navigation:

- **Click a Page Address** to jump to that page within the tree — it scrolls to it,
  highlights it, and selects it in the Detail pane.
- **Shift + click** a Page Address to open it in the [Page](Pages.md) view instead.
- **Page Address / Previous Page / Next Page** are shown for the selected page.
  - **Hover** over these values to see where the page sits in the index.
  - **Shift + click** to open the page in the [Page](Pages.md) view.

> **Concept: previous and next — the doubly-linked leaf level**
> Pages at the same level of an index are chained together with **previous-page** and
> **next-page** pointers, forming a doubly-linked list. This allows SQL Server to scan a
> range of rows by following the chain sideways rather than walking the tree for each row,
> and is relevant to both range scans and fragmentation.

## See also

- [Allocations](Allocations.md) — locate an index and select **View** to open it here.
- [Pages](Pages.md) — open an individual index page to see its raw bytes decoded.
