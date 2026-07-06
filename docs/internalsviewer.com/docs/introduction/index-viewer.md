# Index Viewer

The Index Viewer visualizes an entire index as a tree of pages - the root page at the top, connected level by level down to the leaf. It is the actual physical structure of the index, built by reading every page and following the down page pointers from the root.

## Opening an index

- Click the **View** link in the Index column of the Allocation Info table
- Click the **Index** button in the [Page Viewer](/docs/introduction/page-viewer) toolbar when viewing a page that belongs to an index
- Right-click an index in the Plan lane of the [Query](/docs/introduction/query) timeline

The header bar shows the index name, index type (Clustered / Non-Clustered), the table, and the Object and Index Ids.

## Navigating

Clicking a page in the tree shows its details:

- **Page Address**, **Previous Page**, and **Next Page** - pages within each index level are doubly linked
- The decoded index records - the key values, and for pages above the leaf the **Down Page Pointer** to the page covering each key range

Page addresses and down page pointers are links into the Page Viewer, so an index can be walked from root to leaf - the same path an index seek takes.

::: tip
The tree can be far bigger than the screen - zoom in and out with **Ctrl + mouse wheel**.

**Shift + click** a page in the tree to open it directly in the Page Viewer.
:::

## During query replay

When opened from the [Query](/docs/introduction/query) timeline, the Index Viewer is linked to the trace - as the replay runs, each page lights up at the moment the engine reads it. A scan sweeps across the leaf level in order; a seek lights a single root-to-leaf path.

::: details How this works
The tree is discovered with a breadth-first walk from the index's root page, decoding the index records on each page for their down page pointers - reading every page of the index exactly once.

See [How the Index view works](/docs/deep-dives/index-view) for the details.
:::
