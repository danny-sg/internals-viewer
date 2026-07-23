# Allocations

A database opens to the Allocations view. It has two parts - the Allocation Map showing the physical layout of the database, and the Allocation Info table listing the objects in it.

## Allocation Map

The Allocation Map is a visualization of the physical layout of each database data file.

Each block represents a [page](https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide), the 8 KB unit the storage engine uses to manage data. Pages are grouped into units of eight called extents, covering 64 KB. Extents are the unit SQL Server allocates space in, and the Allocation Map colour codes each page by the object it is allocated to.

::: tip
Clicking on a page will open it in the [Page Viewer](/docs/user-guide/page-viewer)

Use the mouse wheel or scrollbar to scroll up and down the database file.

The Allocation Map can be zoomed in and out with **Ctrl + mouse wheel**.
:::

::: details How this works
The Allocation Map is a render of the IAM (Index Allocation Map) chains for all objects.

Internals Viewer decodes and reads the internal tables and follows the IAM chains for each object, using the First IAM then following via the Next Page address.

The `In-row data` allocation unit type is used for the map.
:::

### Tooltip

Toggling the Tooltip button will show a tooltip when hovering over a database page. It will show the Page Id, Extent Id, the PFS status (see below) of the page, and the object the page has been allocated to.

![Allocation map with tooltip](/docs/tutorial/images/screenshots/Database_allocations_with_tooltip.png)

### Overlay

The **Overlay** menu adds a layer of extra information on top of the Allocation Map:

![Overlay menu](/docs/user-guide/images/database-allocations-view-overlay-menu.png)

- **GAM** [Global Allocation Map](https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide#gam-and-sgam-pages), tracking which extents are allocated
- **SGAM** Shared Global Allocation Map, tracking mixed extents with free pages
- **PFS** - Page Free Space, see below.
- **Buffer Pool** - see below
- **DCM** / **BCM** - the Differential Changed Map, tracking extents changed since the last full backup, and the Bulk Changed Map, tracking extents changed by minimally logged operations since the last log backup

Once selected, the overlay's name replaces **Overlay** on the toolbar - click it again to switch to a different overlay or turn it off.

### Buffer Pool

The [Buffer Pool](https://learn.microsoft.com/en-us/sql/relational-databases/memory-management-architecture-guide#buffer-management) is SQL Server's in-memory cache of database pages. The Buffer Pool overlay marks a small tick in the corner of each page that is currently held in it:

![Allocation map with Buffer Pool overlay](/docs/user-guide/images/database-allocations-view-buffer-pool-cropped.png)

Pages in the Buffer Pool can be _clean_, meaning they have not been modified, or _dirty_, meaning they have been modified and changes have not yet been written to disk (but will have been written to the transaction log).

- **Cyan** - the page is clean
- **Red** - the page is dirty

::: tip
This is a good way to see write behaviour in action - modify some data, and the changed pages show as dirty (red) in the Buffer Pool overlay until SQL Server flushes them back to disk, e.g. by running `CHECKPOINT`. See [Log Records](/docs/user-guide/query/LogRecords) for why modified pages can stay dirty in memory long after the query finishes.
:::

### PFS (Page Free Space)

[PFS (Page Free Space)](https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide#pfs-pages) pages track the status of every page in the database, one byte per page, including:

- Allocation status
- Space used in the page
- If the page contains ghost records
- If the page is part of a mixed extent
- If the page is an IAM page

The PFS overlay is best viewed zoomed in:

![Allocation map with PFS overlay](/docs/user-guide/images/database-allocations-view-pfs-zoomed-cropped.png)

- **Space Free** - a bar filled to show how full the page is: Empty, 50%, 80%, 95%, or 100%
- **Ghost Record** - a green ghost icon marks a page containing ghost records (rows deleted but not yet cleaned up)
- **IAM Page** - marked with an **I**
- **Is Allocated** - allocated pages are shaded. Unallocated pages are left blank

The full PFS status for a page is also available on the [tooltip](#tooltip).

::: details How this works
PFS pages store the status of every page as a single byte, so one PFS page covers 8088 pages. The first PFS is always at Page 1 in a database file. If a file spans more than 8088 pages the PFS repeats at this interval (page 1, then 8088, 16176 etc.)

Internals Viewer reads the PFS chain using the size of the file and the PFS interval of 8088.

See the source code for more information on how the PFS byte is decoded.
:::

## Allocation Info

The Allocation Info is a table of the indexes and tables in the database, shown below the Allocation Map. The **Allocations** toggle on the toolbar shows and hides it.

It gives a key to the colour codes used on the Allocation Map. Selecting an object highlights its pages on the map, and if the object is not currently visible its position is marked on the map's scrollbar. **Shift + click** selects multiple objects to highlight together, and clicking a selected object again deselects it.

The Filter input filters the table by name, and the columns can be sorted by clicking their headers.

The Allocation Info includes the Object Name, Index Name, Index Type (Clustered/Non-Clustered/Heap), the number of pages used, and the entry points into the table or index.

### Entry Points

The entry points give information on how to find where a table or index is physically stored.

| Index Type    | Root Page          | First Page         | First IAM          |
| ------------- | ------------------ | ------------------ | ------------------ |
| Clustered     | :white_check_mark: | :white_check_mark: | :white_check_mark: |
| Non-Clustered | :white_check_mark: | :white_check_mark: | :white_check_mark: |
| Heap          | :x:                | :x:                | :white_check_mark: |

::: tip
Clicking on an entry point will open the page in the [Page Viewer](/docs/user-guide/page-viewer)

For indexes, the **View** link in the Index column opens the whole index in the [Index View](/docs/user-guide/index-view)
:::

The three entry points for any object are:

#### Root Page

This is the root page and start point of an index if the object is a clustered or non-clustered index.

An index seek would start from this point and traverse the index to find data.

#### First Page

This is the first data page of a table with a clustered index, or the first leaf level page of a non-clustered index.

Subsequent pages can be traversed using the Next Page and Previous Page (double linked list) values in the page header.

Heaps do not use First Page.

#### First IAM Page

SQL Server tracks object allocations using [IAM (Index Allocation Map)](https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide#iam-pages) pages. Extents are tracked in a bitmap, one bit per extent. One IAM covers around 64,000 extents. If tracking is needed for more than this amount further IAMs are chained together, linked via the page header.

> 63,904 bits = 7,988 bytes = 1 page (8,192 bytes) less page header/overhead.

::: details How this works
Entry points are stored in the system base table `sys.sysallocunits`.

Base tables cannot be queried unless using a DAC (Dedicated Admin Connection). Internals Viewer reads the base table directly.

`sys.sysallocunits` is the basis of the `sys.system_internals_allocation_units` view.

The page address values are in binary format. They can be decoded using the undocumented `sys.fn_PhysLocFormatter` function.

```sql
SELECT *
      ,sys.fn_PhysLocFormatter(first_page)     AS decoded_first_page
      ,sys.fn_PhysLocFormatter(root_page)      AS decoded_root_page
      ,sys.fn_PhysLocFormatter(first_iam_page) AS decoded_first_iam_page
FROM   sys.system_internals_allocation_units
```

:::
