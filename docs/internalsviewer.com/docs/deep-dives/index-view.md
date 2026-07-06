# How the Index view works

The Index view draws an entire index as a tree of pages. Nothing in the database stores that tree as a structure that can simply be read out - there is no "list of pages in this index, level by level". What the database provides is a single entry point and pages that point at other pages. The Index view discovers the tree the same way the storage engine navigates it.

## Starting point - the root page

Every index's entry points are stored in `sys.sysallocunits` (see [How the database is loaded](/docs/deep-dives/loading-a-database)), and the one that matters here is **Root Page** - the single page at the top of the B-Tree. This is the address shown in the Allocation Info table, and it is where the enumeration starts.

## Enumerating the pages

The tree is discovered level by level - a breadth-first walk from the root:

1. **Read every page in the current level** (just the root, on the first pass). The reads within a level are independent, so they run in parallel - up to 16 pages at a time.

2. **Decode the index records on each page.** This is the same [index record](/docs/reference/index-records) decode the Page Viewer uses. Every record on an index page above the leaf contains a **Down Page Pointer** - the address of the child page covering that record's key range.

3. **The child addresses become the next level**, in the order their parents listed them. Each child node records its parent, and the parent records its children - these links are what the view draws as connecting lines.

4. **Repeat** until a level produces no children.

Only index pages above the leaf contribute down page pointers - a page whose header `Index Level` is 0 is a leaf and has nothing below it. Leaf pages (index pages of a non-clustered index, data pages of a clustered index) are still read - their page type, `Previous Page`, and `Next Page` are needed for the display - but they end the walk.

So the enumeration reads every page of the index exactly once: the tree you see is not an approximation or a metadata summary, it is the actual physical structure, discovered pointer by pointer.

### De-duplication

Pages are tracked by address in a dictionary as they are discovered. If a page address turns up again - which can happen at the boundaries between key ranges - the existing node is reused and just gains an extra parent link, rather than appearing in the tree twice. This is also what keeps the walk safe: a page can never be read or expanded more than once.

### Two level numbers

There are two level numberings in play, counting in opposite directions:

- The **display level** counts down from the root: root = 0, its children = 1, and so on. This is assigned during the walk and controls the vertical position in the view.
- The page header's **`Index Level`** counts up from the leaf: leaf = 0, root = highest. This is what the storage engine stores.

They meet in the middle - a three level index has display levels 0/1/2 and header levels 2/1/0.

## Drawing and navigating

Each discovered node carries what the view needs: page address, page type, level, its position within the level, and the parent/child links. Levels are laid out in discovery order - which, because children are collected in key order from their parents, is also key order across each level.

Clicking a page in the tree reads that page again and decodes its records in full, showing the key values and Down Page Pointers in the details panel - each one clickable to continue into the Page Viewer.

During query replay the same tree becomes a canvas: `physical_page_read` events carry page addresses, and each address that belongs to the index lights its node up as the playhead passes (see [How query tracing works](/docs/deep-dives/query-tracing)).

> [!NOTE]
> Because the enumeration reads every page in the index, opening the Index view for a large index reads the whole index - the same I/O as a full scan. The parallel level-by-level loading keeps this fast, but on a very large index expect it to take a moment.

## In the source

- `InternalsViewer.Internals/Services/Indexes/IndexService.cs` - the breadth-first enumeration
- `InternalsViewer.Internals/Services/Records/RecordService.cs` - decoding index records for the down page pointers
- `InternalsViewer.UI.App/Controls/Index/IndexControl.xaml.cs` - the tree rendering
