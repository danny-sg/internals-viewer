# Database View

A database will open to the Database view.

## Allocation Map

The Allocation Map is a visualization of the physical layout of each database data file.

Each block represents a page, the 8 KB unit that the storage engine uses to manage data. Pages are managed in units of eight called extents, covering 64 KB.

Allocations are managed with individual pages and extents.

The Allocation Map colour codes objects in the database. 

::: tip
Clicking on a page will open it in the [Page Viewer](/docs/introduction/page-viewer)
:::

::: details How this works
The Allocation Map is a render of the IAM (Index Allocation Map) chains for all objects.

Internals Viewer decodes and reads the internal tables and follows the IAM chains for each object, using the First IAM then following via the Next Page address.
:::

### Tooltip 

Toggling the Tooltip button will show a tooltip when hovering over a database page. It will show the Page Id, Extent Id, the PFS status (see below) of the page, and the object the page has been allocated to.

### PFS (Page Free Space)

The PFS button will toggle an overlay of the PFS information on the Allocation Map. 

PFS, or Page Free Space pages are a way that SQL Server tracks allocations. The PFS stores information about each page in the database, including:

- Allocation status
- Space used in the page
- If the page contains ghost records
- If the page is part of a mixed extent
- If the page is an IAM page

On the Allocation Map IAM pages are represented with an I.

The space usage is represented by an overlay.

::: details How this works
PFS pages store the status of every page as a byte. A single PFS page covers 8088 bytes/pages. The first PFS is always at Page 1 in a database file. If a file spans more than 8088 pages the PFS repeats at this internal (page 1, then 8088, 16176 etc.)

Internals Viewer reads the PFS chain using the size of the file and the PFS interval of 8088.

See the source code for more information on how the PFS byte is decoded.
:::

## Allocation Info

The Allocation Info is a table of the indexes and tables in the database. It gives a key to the colour codes used on the Allocation Map and it will also highlight the object when selected. If the object is not currently visible it will show where it is on the scrollbar.

The Filter input can be used to filter the table to search via name.

The Allocation Info includes the Object Name, Table Name, Index Type (Clustered/Non-Clustered/Heap), the number of pages used, the entry points into the table or index, and also a pointer to the first IAM (Index Allocation Map) for the object.

### Entry Points

The entry points give information on how to find where a table or index is physically stored. 

| Index Type    | Root Page      |  First Page | First IAM
| ------------- | -------------- | ----------- | --------- |
| Clustered     | :white_check_mark: | :white_check_mark: | :white_check_mark: |
| Non-Clustered | :white_check_mark: | :white_check_mark: | :white_check_mark: |
| Heap          | :x:                | :x:                | :white_check_mark: |

::: tip
Clicking on an entry point will open the page in the [Page Viewer](/docs/introduction/page-viewer)
:::

The three entry points for any object are:

#### Root Page

This is the root page and start point of an index if the object is a clustered or non-clustered index.

An index seek would start from this point and traverse the index to find data.

#### First Page

This is the first data page of a table with a clustered index, or the leaf level of an non-clustered index.

Subsequent pages can be traversed using the Next Page and Previous Page (double linked list) values in the page header.

Heaps do not use First Page.

#### First IAM Page

SQL Server tracks object allocations using IAM (Index Allocation Map) pages. Extents are tracked in a bitmap, one bit per extent. One IAM covers around 64,000 extents. If tracking is needed for more that this amount further IAMs are chained together, linked via the page header.

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

