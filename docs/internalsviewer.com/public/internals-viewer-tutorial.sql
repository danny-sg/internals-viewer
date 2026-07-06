/* ================================================================
   SQL Internals Viewer - Tutorial script

   All of the SQL used in the tutorial at
   https://internalsviewer.com

   Run each step in turn, refreshing in Internals Viewer between
   steps to see the effect on the database storage.

   Requires SQL Server 2019 or later.
   ================================================================ */

-- ================================================================
-- Part 1 - Connecting and allocations
-- ================================================================

-- Step 1 - Create the database, then connect to it in
--          Internals Viewer

CREATE DATABASE InternalsViewerTutorial
GO

USE InternalsViewerTutorial
GO

-- Step 2 - Add a table
--
-- This table is a heap - it does not have a clustered index.
-- No allocations happen until data is inserted.

CREATE TABLE dbo.HeapTable
(
    NumberField    INT          NOT NULL
   ,TextField      VARCHAR(100) NOT NULL
   ,FixedTextField CHAR(10)     NOT NULL
)
GO

-- Step 3 - Insert data
--
-- Refresh the database in Internals Viewer to see the first
-- extent allocated to the table.

INSERT INTO dbo.HeapTable VALUES
    (100, 'This is the first row',      'Row 1')
   ,(200, 'And this is the second row', 'Row 2')
GO

-- Step 5 - Add more rows
--
-- Refresh the Page Viewer to see the new rows appear in the
-- page slots.

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
GO

-- Step 6 - Fill the page
--
-- The rows overflow onto new pages. Note the heap's pages are
-- not linked - Next Page in the page header stays (0:0).

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
GO

-- ================================================================
-- Part 2 - Viewing pages
-- ================================================================

-- Tip - find the page a row is stored on. %%physloc%% returns the
-- row's physical location and fn_PhysLocFormatter formats it as
-- (File Id:Page Id:Slot Id) - paste the address into the Page
-- Viewer to open the page

SELECT sys.fn_PhysLocFormatter(%%physloc%%) AS RowLocation
      ,*
FROM   dbo.HeapTable
GO

-- Step 4 - Linked pages
--
-- A table with a clustered index - the data pages are the leaf
-- level of the index, kept in key order and doubly linked.

CREATE TABLE dbo.ClusteredTable
(
    Id          INT IDENTITY(1,1) NOT NULL
   ,TextField   VARCHAR(100)      NOT NULL
   ,CreatedDate DATETIME2         NOT NULL

   ,CONSTRAINT PK_ClusteredTable PRIMARY KEY CLUSTERED (Id)
)
GO

INSERT INTO dbo.ClusteredTable
        (TextField, CreatedDate)
SELECT  TOP (100000)
        CONCAT('This is row ', ROW_NUMBER() OVER (ORDER BY (SELECT NULL)))
       ,SYSDATETIME()
FROM    sys.all_columns AS c1
        CROSS JOIN sys.all_columns AS c2
GO

-- ================================================================
-- Part 3 - Indexes
-- ================================================================

-- Step 4 - Non-clustered index on a clustered table
--
-- A separate B-Tree. The leaf records contain the index key plus
-- the clustering key (Id) pointing back to the table.

CREATE INDEX IX_ClusteredTable_TextField ON dbo.ClusteredTable (TextField)
GO

-- Step 5 - Non-clustered index on a heap
--
-- No clustering key to point to, so the leaf records contain a
-- RID (File Id:Page Id:Slot Id) - a direct physical pointer to
-- the row.

CREATE INDEX IX_HeapTable_NumberField ON dbo.HeapTable (NumberField)
GO

-- ================================================================
-- Part 4 - Query
--
-- Run these in the Internals Viewer Query view (Query button on
-- the database toolbar) to trace and replay them on the timeline.
-- ================================================================

-- [Using the Query view]

-- An index seek - a handful of page reads
-- (root -> leaf on IX_ClusteredTable_TextField)

SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  TextField LIKE 'This is row 123%'
GO

-- The leading wildcard forces a scan - watch the Read lane fill
-- with page reads

SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  TextField LIKE '%row 123%'
GO

-- A blocking operator. Click the Sort's bar in the Plan lane -
-- it consumes its input for almost its whole lifetime and only
-- streams to the SELECT at the end

SELECT NumberField
      ,TextField
FROM   dbo.HeapTable
ORDER  BY TextField
GO

-- [Scans vs seeks]
--
-- Run each query, right-click the operator's bar in the Plan lane
-- of the timeline to open the Index view, then drag the playhead
-- back and forward to watch the pages light up.

-- A full clustered index scan - sweeps the whole leaf level in
-- Next Page order

SELECT *
FROM   dbo.ClusteredTable
GO

-- A clustered index seek - lights up only the root-to-leaf path
-- to the single matching row

SELECT *
FROM   dbo.ClusteredTable
WHERE  Id = 54321
GO

-- [Joins]
--
-- The same join hinted three ways. Trace each with Clear Buffer
-- Pool and Disable Read-Ahead on and compare the Read lane and
-- Index view access patterns.

-- First spread the heap's keys across the whole clustered table
-- (they currently only cover 100-1100) so the join access
-- patterns play out across the entire index - adds 5,000 rows,
-- every 20th Id

INSERT INTO dbo.HeapTable
        (NumberField, TextField, FixedTextField)
SELECT  Id
       ,CONCAT('Join row ', Id)
       ,LEFT(CONCAT('Row ', Id), 10)
FROM    dbo.ClusteredTable
WHERE   Id % 20 = 0
GO

-- Nested Loops - one clustered index seek per outer row: a
-- repeating drumbeat of single-page reads

SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER LOOP JOIN dbo.ClusteredTable c
         ON c.Id = h.NumberField
GO

-- Merge Join - both inputs already sorted on the key, zipped
-- together in one interleaved in-order pass. If the plan shows
-- a Sort on the heap side, force the ordered index scan with:
-- dbo.HeapTable h WITH (INDEX(IX_HeapTable_NumberField))

SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER MERGE JOIN dbo.ClusteredTable c
         ON c.Id = h.NumberField
GO

-- Hash Match - build phase reads the heap into a hash table
-- (nothing streams downstream), then the probe phase streams the
-- larger input through it

SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER HASH JOIN dbo.ClusteredTable c
         ON c.Id = h.NumberField
GO

-- No hint - see which strategy the optimizer picks by itself

SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER JOIN dbo.ClusteredTable c
         ON c.Id = h.NumberField
GO

-- ================================================================
-- Part 5 - LOB data
-- ================================================================

-- Step 1 - Three rows, three storage locations. Note the CASTs -
-- REPLICATE returns VARCHAR(8000) unless its input is already a
-- MAX type.

CREATE TABLE dbo.LobTable
(
    Id       INT          NOT NULL
   ,LobField VARCHAR(MAX) NOT NULL
)
GO

-- Row 1 - 500 bytes - stays in the row like a normal VARCHAR

INSERT INTO dbo.LobTable
VALUES (1, REPLICATE('A', 500))

-- Row 2 - 8,020 bytes - just over the 8,000 byte in row limit,
-- moves to a single LOB (Text/Image) page with a pointer left
-- in the data row

INSERT INTO dbo.LobTable
VALUES (2, REPLICATE(CAST('B' AS VARCHAR(MAX)), 8020))

-- Row 3 - 160,000 bytes - split into ~8 KB chunks organised as
-- a tree: an Internal root record linking to Data records across
-- multiple LOB pages

INSERT INTO dbo.LobTable
VALUES (3, REPLICATE(CAST('C' AS VARCHAR(MAX)), 160000))
GO

-- ================================================================
-- Clean up
-- ================================================================

-- USE master
-- GO
-- DROP DATABASE InternalsViewerTutorial
-- GO
