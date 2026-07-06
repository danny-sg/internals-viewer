# Part 1 - Connecting and allocations

In this part we'll cover:

- Connecting to a database
- Creating tables, inserting rows, and viewing allocations
- How to find object entry points

> [!TIP]
> All of the SQL used in the tutorial can be downloaded as a single script: <a href="/internals-viewer/internals-viewer-tutorial.sql" download="internals-viewer-tutorial.sql">internals-viewer-tutorial.sql</a>

## Step 1 - Create the database

First create a new database:

```SQL
CREATE DATABASE InternalsViewerTutorial
GO

USE InternalsViewerTutorial
GO
```

Connect to this database in Internals Viewer:

![01 Connect](/docs/tutorial/images/01-connect-to-database.png)

When connected the database will be empty.

![01 initial allocation view](/docs/tutorial/images/01-initial-view.png)

All of the allocations so far are system objects that SQL Server uses to manage the database. These are the same objects Internals Viewer reads for information about the database.

## Step 2 - Add a table

Next we'll create a table. This table will be a _heap_ meaning it does not have a clustered index.

```SQL
CREATE TABLE dbo.HeapTable
(
    NumberField    INT          NOT NULL
   ,TextField      VARCHAR(100) NOT NULL
   ,FixedTextField CHAR(10)     NOT NULL
)
GO
```

Refresh the database with the Refresh button.

![01 database refresh button](/docs/tutorial/images/01-database-refresh-button.png)

There will be no changes to the database so far as no data has been inserted and the allocations for this table haven't started.

## Step 3 - Insert data

Add data with the following SQL:

```SQL
INSERT INTO dbo.HeapTable VALUES
    (100, 'This is the first row',      'Row 1')
   ,(200, 'And this is the second row', 'Row 2')
```

Refresh the database with the Refresh button and Internals Viewer will show where the table data has been stored:

![01 database post allocations](/docs/tutorial/images/01-after-initial-insert.png)

This shows an extent (8 pages) has been allocated to the table:

![01 allocated extent](/docs/tutorial/images/01-allocated-extent.png)

Each page is 8 KB (8192 bytes), so this extent covers 64 KB.

SQL Server used to begin allocations for an object with up to eight single pages from mixed extents. Since SQL Server 2016 the `MIXED_PAGE_ALLOCATION` database option is off by default, meaning single page allocations are not used and extents are _uniform_, i.e. all eight pages belong to the same object.

> [!TIP]
> The allocation map can be zoomed in and out with **Ctrl + mouse wheel**.

If you toggle the Tooltip button and put the cursor over the extent it will tell you the Page and Extent number and the allocated object

![01 tooltip](/docs/tutorial/images/01-tooltip.png)

> [!NOTE]
> The allocation status of each page is tracked by PFS (Page Free Space) pages. The PFS would show that only the first page of the extent is allocated.
>
> The PFS can be added to the allocation map by toggling the PFS toolbar button.

## Step 4 - Finding the data

There are a couple of ways to view the data.

The Allocation Info table includes entry points to an object.

![01 entry points](/docs/tutorial/images/01-entry-points.png)

> [!TIP]
> Pages are addressed by File Id giving the database file the page is in, and Page Id, giving the page index. The physical location in the file is Page Id \* 8192 bytes.
>
> Page Address in Internals Viewer is formatted as (File Id:Page Id). If you see it in this format in the application you can click on it to open that page.
>
> An address can also include a slot formatted as (File Id:Page Id:Slot Id), this gives a pointer to a particular record in a page. It is the format RID (Row Identifier) pointers take.

**Root Page** is the start point of an index. B-Tree indexes go from the _root_ page to the _leaf_ pages.

As this table is a heap (no clustered index) there is no index structure. The page address of File 0, Page 0 - `(0:0)` - means an empty value.

**First Page** is where the table data starts. We'll see later how this first page leads to the other pages.

**First IAM Page** is the location of the start of the IAM chain that tracks the object allocation.

To view the data we need to either click on the first page on the allocation map, or click on the _First Page_ link in the Allocation Info table.

![01 open page](/docs/tutorial/images/01-open-page.png)

This will open up the Page in the Page Viewer.

## Step 5 - Viewing the Page

The Page Viewer has three parts:

- Raw data displaying the 8192 bytes that make up the page in hexadecimal format.
- Page slots to switch to the different records on the page.
- Decoded information to show where on the page the various data structures are and how they can be represented.

![01 Page Viewer](/docs/tutorial/images/01-page-viewer.png)

When the page first loads the Page Header is selected. See the [Page Header](/docs/reference/page-header) reference for the fields.

Clicking on Slots 0 and 1 displays the two rows that were inserted:

![01 page rows](/docs/tutorial/images/01-page-row.png)

The raw data is colour coded to show where the different elements of the row are stored.

The decoded information shows a combination of fields the database uses to store information about the row, including things like `Column Count` and `Column Count Offset` and also the actual data, for example the value in column `TextField`.

Clicking on an item in the decoded information will highlight where it is in the raw data.

Selecting bytes in the raw data will show a decode for the data selected represented in various applicable data types.

![01 decoded data](/docs/tutorial/images/01-decoded-data.png)

Next add more rows via SQL

```SQL
DECLARE @RowNumber INT = 1;

WHILE @RowNumber <= 100
BEGIN
    INSERT INTO dbo.HeapTable
    VALUES
    (
        100 + @RowNumber
       ,CONCAT('This is row ', @RowNumber)
       ,CONCAT('Row ', @RowNumber)
    )

    SET @RowNumber += 1;
END
```

Press the Refresh button to refresh the page.

There should now be more entries in the Page slots table and the new rows should be visible:

![01 page slots more rows](/docs/tutorial/images/01-page-slots-more-rows.png)

## Step 6 - Filling the page

We haven't yet filled the page with data.

Clicking on Page Header in the Slot Offset Table will select the Page Header and display the decoded values.

`Slot Count` tells us the number of records that are stored in the page - 102 (2 inserted initially and 100 inserted through the above step).

Note `Free Count` and `Next Page`. `Free Count` corresponds to the number of free bytes in the data page, so this page is about 50% full. `Next Page` is `(0:0)` meaning there is no next page.

Add more rows to fill the page and beyond:

```SQL
DECLARE @RowNumber INT = 101;

WHILE @RowNumber <= 1000
BEGIN
    INSERT INTO dbo.HeapTable
    VALUES
    (
        100 + @RowNumber
       ,CONCAT('This is row ', @RowNumber)
       ,CONCAT('Row ', @RowNumber)
    )

    SET @RowNumber += 1;
END
```

Press the Refresh button on the Page Viewer.

The page is now full. `Slot Count` shows the maximum number of records that fitted in the page, and `Free Count` is close to zero. The rest of the rows have overflowed onto new pages.

Switch back to the database tab and press Refresh. The allocation map now shows more pages allocated to the table as it has grown beyond a single page.

You might expect `Next Page` on our first page to now point to the next page of the table, but it is still `(0:0)`. This is because heaps do not link their pages together. The `Next Page` / `Previous Page` pointers in the page header are only maintained for index pages and the leaf level of clustered indexes.

So how does SQL Server find all the pages of a heap? The answer is the IAM (Index Allocation Map) chain - the same structure Internals Viewer uses to draw the allocation map. The IAM is a bitmap of the extents allocated to an object, and for heaps it is the only way to find the pages.

We'll look at IAM pages in [Part 2](/docs/tutorial/2-viewing-pages), and see linked pages in action with a clustered index.

## Summary

In this part we:

- Created a database and connected to it
- Created a heap table and watched the first extent get allocated as data was inserted
- Used the Allocation Info entry points to find where the data is stored
- Viewed rows in the Page Viewer
- Filled a page and saw that heap pages are tracked by the IAM chain, not linked together

Next: [Part 2 - Viewing pages](/docs/tutorial/2-viewing-pages)
