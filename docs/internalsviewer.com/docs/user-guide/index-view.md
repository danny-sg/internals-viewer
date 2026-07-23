# Index View

The Index View visualizes an entire index as a tree of pages - the root page at the top, connected level by level down to the leaf. It is the actual physical structure of the index, built by reading every page and following the down page pointers from the root.

## Opening an index

- Click the **View** link in the Index column of the Allocation Info table
- Click the **Index** button in the [Page Viewer](/docs/user-guide/page-viewer) toolbar when viewing a page that belongs to an index
- Right-click an index in the Plan lane of the [Query](/docs/user-guide/query) timeline

The header bar shows the index name, index type (Clustered / Non-Clustered), the table, and the Object and Index Ids.

## Navigating

Clicking a page in the tree shows its details:

- **Page Address**, **Previous Page**, and **Next Page** - pages within each index level are doubly linked
- The decoded index records - the key values, and for pages above the leaf the **Down Page Pointer** to the page covering each key range

Page addresses and down page pointers are links into the Page Viewer, so an index can be walked from root to leaf - the same path an index seek takes.

::: tip
The tree can be far bigger than the screen. The view opens zoomed to fit - zoom in and out with **Ctrl + mouse wheel**, drag to pan around, and use **Zoom to Fit** to get back to the whole tree.

The level of detail adapts to the zoom - zoomed right in, each page shows its address and Previous/Next links as well as its records.

**Shift + click** a page in the tree to open it directly in the Page Viewer.
:::

Hovering over a page address anywhere in the details panel - a Down Page Pointer, Previous Page, Next Page - highlights the matching page in the tree, making it easy to spot where a pointer leads before clicking it:

![Index View with a page highlighted from hovering a page address](/docs/user-guide/images/index-view-page-detail-with-hover.png)

## During query replay

When opened from the [Query](/docs/user-guide/query) timeline, the Index Viewer is linked to the trace - as the replay runs, each page lights up at the moment the engine reads it. A scan sweeps across the leaf level in order, while a seek lights up a single root-to-leaf path.

![Index View during query replay](/docs/user-guide/images/query-index-view-index-animation.png)

::: details How this works
The tree is discovered with a breadth-first walk from the index's root page, decoding the index records on each page for their down page pointers - reading every page of the index exactly once.

See [How the Index view works](/docs/deep-dives/index-view) for the details.
:::
