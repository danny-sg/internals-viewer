/*
-----------------------------------------------------------------------------------------------------------------------
    Internals Viewer
-----------------------------------------------------------------------------------------------------------------------
    Trace Queries

    One query per traceable operator, then combinations worth stepping through.

    Every query pins its plan shape with hints so the operator under test actually appears. MAXDOP 1 is on all of them:
    a parallel plan puts an Exchange in the tree and nothing below it can be traced.

    Run against InternalsViewerDemo, built by "InternalsViewerDemo - Create script.sql".
-----------------------------------------------------------------------------------------------------------------------
*/

USE InternalsViewerDemo
GO

/*
-----------------------------------------------------------------------------------------------------------------------
    Prerequisite - dbo.DuplicateKeyTable is created empty by the create script
-----------------------------------------------------------------------------------------------------------------------

    Needed by 1.13 and 2.6. Twenty categories of one hundred rows, clustered on Category so the rows arrive in group
    order without a Sort.
*/

IF NOT EXISTS (SELECT 1 FROM dbo.DuplicateKeyTable)
BEGIN
    INSERT INTO dbo.DuplicateKeyTable
            (Category, TextField)
    SELECT  Id % 20
           ,CONCAT('Duplicate key row ', Id)
    FROM    dbo.ClusteredTable
    WHERE   Id <= 2000;
END
GO

/*
-----------------------------------------------------------------------------------------------------------------------
    Part 1 - One operator at a time
-----------------------------------------------------------------------------------------------------------------------
*/

-- 1.1 Table Scan
--     Allocation ordered scan of a heap. Watch the IAM read, the PFS checks, and the extent by extent walk.

SELECT Id
      ,TextField
FROM   dbo.HeapTable
OPTION (MAXDOP 1)
GO

-- 1.2 Clustered Index Scan
--     Descends to the first leaf page, then follows the leaf level links. CreatedDate forces the clustered index
--     rather than one of the narrower non-clustered ones.

SELECT Id
      ,TextField
      ,CreatedDate
FROM   dbo.ClusteredTable
OPTION (MAXDOP 1)
GO

-- 1.3 Backward Index Scan
--     Same walk in reverse, following the previous page links rather than the next.

SELECT TOP (20)
       Id
FROM   dbo.ClusteredTable
ORDER  BY Id DESC
OPTION (MAXDOP 1)
GO

-- 1.4 Index Seek
--     One descent from the root, then a walk along the range until the end key is passed.

SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  Id BETWEEN 500 AND 520
OPTION (MAXDOP 1)
GO

-- 1.5 Index Seek - multiple ranges
--     Three ranges means three descents. Watch for the Reseek step between them: the walk restarts at the root each
--     time rather than carrying on along the leaf.

SELECT Id
      ,TextField
FROM   dbo.HeapTable
WHERE  Id IN (100, 500, 1000)
OPTION (MAXDOP 1)
GO

-- 1.6 RID Lookup
--     The non-clustered index gives a row identifier, the heap page is then read directly - no descent, no search.

SELECT Id
      ,TextField
      ,FixedTextField
FROM   dbo.HeapTable
WHERE  Id = 500
OPTION (MAXDOP 1)
GO

-- 1.7 Key Lookup
--     ix_ClusteredTable_TextField covers Id and TextField, so CreatedDate forces a lookup back into the clustered
--     index. The lookup is a seek that is re-opened per outer row.

SELECT Id
      ,TextField
      ,CreatedDate
FROM   dbo.ClusteredTable
WHERE  TextField = 'Clustered table row 4242'
OPTION (MAXDOP 1)
GO

-- 1.8 Nested Loops
--     Six outer rows, one clustered index seek each. Watch the Rebind step at the start of every inner pass.

SELECT h.Id
      ,c.TextField
FROM   dbo.HeapTable AS h
       INNER LOOP JOIN dbo.ClusteredTable AS c
         ON c.Id = h.Id
WHERE  h.Id BETWEEN 100 AND 200
OPTION (MAXDOP 1)
GO

-- 1.9 Merge Join
--     Both sides arrive in Id order, so the join zips them together in one pass. Neither side is ever re-read.

SELECT h.Id
      ,c.TextField
FROM   dbo.HeapTable AS h
       INNER MERGE JOIN dbo.ClusteredTable AS c
         ON c.Id = h.Id
WHERE  h.Id BETWEEN 100 AND 1000
OPTION (MAXDOP 1)
GO

-- 1.10 Hash Match - join
--      The build side fills the hash table first and returns nothing, then the probe side streams through it.
--      Try the bucket count dropdown on the hash table pane to watch the chains lengthen.

SELECT h.Id
      ,c.TextField
FROM   dbo.HeapTable AS h
       INNER HASH JOIN dbo.ClusteredTable AS c
         ON c.Id = h.Id
WHERE  h.Id BETWEEN 100 AND 1000
OPTION (MAXDOP 1)
GO

-- 1.11 Hash Match - aggregate
--      One entry per group rather than per row. Blocking: nothing is returned until the whole input has been read.

SELECT TextField3
      ,COUNT(*) AS RowTotal
FROM   dbo.CompressedTable
WHERE  Id <= 5000
GROUP  BY TextField3
OPTION (HASH GROUP, MAXDOP 1)
GO

-- 1.12 Stream Aggregate - scalar
--      No grouping, so the whole input folds into one row. It returns a row even when the input has none, which is
--      worth trying by changing the predicate to something that matches nothing.

SELECT MIN(Id)   AS MinId
      ,MAX(Id)   AS MaxId
      ,COUNT(*)  AS RowTotal
FROM   dbo.HeapTable
OPTION (MAXDOP 1)
GO

-- 1.13 Stream Aggregate - grouped
--      DuplicateKeyTable is clustered on Category, so the rows already arrive in group order and no Sort is needed.
--      A group is returned the moment the key changes.

SELECT Category
      ,COUNT(*) AS RowTotal
FROM   dbo.DuplicateKeyTable
GROUP  BY Category
OPTION (ORDER GROUP, MAXDOP 1)
GO

-- 1.14 Sort
--      Blocking - every input row is collected before the first row is returned, because the smallest row might be
--      the last one read.

SELECT Id
      ,TextField
FROM   dbo.HeapTable
WHERE  Id <= 2000
ORDER  BY TextField
OPTION (MAXDOP 1)
GO

-- 1.15 Sort - distinct
--      Duplicates are removed at the output point: the rows are already in key order, so equal rows sit next to each
--      other and only the first of each run is returned.

SELECT DISTINCT
       Category
FROM   dbo.DuplicateKeyTable
ORDER  BY Category
OPTION (MAXDOP 1)
GO

-- 1.16 Top
--      The row goal is pushed into the scan below, so the scan stops early rather than reading to the end. Watch the
--      Stopped step say RowGoalMet.

SELECT TOP (5)
       Id
      ,TextField
FROM   dbo.ClusteredTable
OPTION (MAXDOP 1)
GO

-- 1.17 Concatenation
--      Inputs are read one after another, in order. The second input is not opened until the first runs out.

SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  Id BETWEEN 1 AND 10
UNION ALL
SELECT Id
      ,TextField
FROM   dbo.HeapTable
WHERE  Id BETWEEN 100 AND 200
OPTION (MAXDOP 1)
GO

-- 1.18 Compute Scalar
--      Expressions evaluated per row and added as columns. Only functions the engine model implements will translate
--      - ABS, LEN, UPPER, LOWER, LEFT, RIGHT, SUBSTRING, CHARINDEX, REPLACE, CONCAT, ISNULL, ROUND, POWER and the
--      date functions. Anything else leaves the operator untranslatable and the trace unavailable.

SELECT Id
      ,Id * 2                  AS Doubled
      ,LEN(TextField)          AS TextLength
      ,UPPER(LEFT(TextField, 9)) AS Prefix
FROM   dbo.ClusteredTable
WHERE  Id BETWEEN 1 AND 20
OPTION (MAXDOP 1)
GO

/*
-----------------------------------------------------------------------------------------------------------------------
    Part 2 - Combinations
-----------------------------------------------------------------------------------------------------------------------
*/

-- 2.1 Top over a Key Lookup
--     The row goal travels down through the loop join into the outer seek. The seek stops after three rows and the
--     lookup runs three times, not once per matching index row.

SELECT TOP (3)
       Id
      ,TextField
      ,CreatedDate
FROM   dbo.ClusteredTable
WHERE  TextField LIKE 'Clustered table row 1%'
OPTION (MAXDOP 1)
GO

-- 2.2 Compute Scalar over a Hash Aggregate over a Compute Scalar
--     Three operators worth watching together. The lower Compute Scalar builds the grouping expression, the aggregate
--     hashes on it, and the upper Compute Scalar converts the bigint COUNT the engine keeps back to the int COUNT
--     returns.

SELECT Id % 10  AS Bucket
      ,COUNT(*) AS RowTotal
FROM   dbo.ClusteredTable
WHERE  Id BETWEEN 1 AND 500
GROUP  BY Id % 10
OPTION (HASH GROUP, MAXDOP 1)
GO

-- 2.3 Sort feeding a Stream Aggregate
--     The same grouping, done the other way. The Sort is what makes the stream aggregate possible, and it is blocking,
--     so nothing reaches the aggregate until the sort has read everything.

SELECT Id % 10  AS Bucket
      ,COUNT(*) AS RowTotal
FROM   dbo.ClusteredTable
WHERE  Id BETWEEN 1 AND 500
GROUP  BY Id % 10
OPTION (ORDER GROUP, MAXDOP 1)
GO

-- 2.4 Hash join of two seeks
--     Both sides are bounded, so the build is small and the whole table fits on screen. Good for watching a bucket
--     chain form and then be walked.

SELECT h.Id
      ,c.TextField
FROM   dbo.HeapTable AS h
       INNER HASH JOIN dbo.ClusteredTable AS c
         ON c.Id = h.Id
WHERE  h.Id BETWEEN 100 AND 400
       AND c.Id BETWEEN 100 AND 400
OPTION (MAXDOP 1)
GO

-- 2.5 Left outer hash join
--     The build side is drained once the probe is done, emitting the rows the probe never matched. Watch the entries
--     the hash table gives up at the end.

SELECT c.Id
      ,h.TextField
FROM   dbo.ClusteredTable AS c
       LEFT HASH JOIN dbo.HeapTable AS h
         ON h.Id = c.Id
WHERE  c.Id BETWEEN 1 AND 200
OPTION (MAXDOP 1)
GO

-- 2.6 Merge join with duplicate keys
--     DuplicateKeyTable has a hundred rows per Category. The inner rows sharing a key are buffered as a group and the
--     group is replayed for each matching outer row - the many to many case.

SELECT d.Category
      ,d.TextField
FROM   dbo.DuplicateKeyTable AS d
       INNER MERGE JOIN dbo.DuplicateKeyTable AS d2
         ON d2.Category = d.Category
WHERE  d.Category <= 2
OPTION (MAXDOP 1)
GO

-- 2.7 Concatenation feeding a Sort
--     Two inputs read in sequence, then everything collected and ordered. The Sort cannot start returning rows until
--     both inputs have been read to the end.

SELECT Id
      ,TextField
FROM   (SELECT Id, TextField FROM dbo.ClusteredTable WHERE Id BETWEEN 1 AND 50
        UNION ALL
        SELECT Id, TextField FROM dbo.HeapTable WHERE Id BETWEEN 100 AND 600) AS combined
ORDER  BY TextField
OPTION (MAXDOP 1)
GO

-- 2.8 Nested loops driving a RID lookup, under a Top
--     Three operators, three different access patterns: an ordered index seek, a direct page read per row, and a row
--     goal that stops the whole thing early.

SELECT TOP (4)
       Id
      ,TextField
      ,FixedTextField
FROM   dbo.HeapTable
WHERE  Id BETWEEN 100 AND 2000
OPTION (MAXDOP 1)
GO

/*
-----------------------------------------------------------------------------------------------------------------------
    Part 3 - Shapes that cannot be traced
-----------------------------------------------------------------------------------------------------------------------

    View > Trace does nothing for these. Every operator in the subtree has to be one the engine model implements, so a
    single unsupported operator anywhere below makes the whole tree untraceable.

    - Parallelism / Exchange      - any parallel plan. This is why every query above sets MAXDOP 1.
    - Spool                       - eager and lazy, common in some subquery and outer join shapes.
    - Partial Aggregate           - the per thread aggregate under an exchange in a parallel plan.
    - Segment / Sequence Project  - window functions, ROW_NUMBER and friends.
    - Insert / Update / Delete    - data modification is not simulated.
    - Nested Loops over a scan    - the inner side has to be a seek or a lookup. A loop join whose inner is a scan has
                                    no correlated seek columns to bind, so it cannot be re-opened per outer row.
    - TOP PERCENT and WITH TIES   - the count is not known before the input is read.
    - Aggregates the model does not implement - STDEV, VAR, CHECKSUM_AGG, STRING_AGG. COUNT, COUNT_BIG, MIN, MAX, SUM,
                                    AVG and ANY all translate.

    One worth trying, to see an untraceable operator stop the whole tree:
*/

-- Untraceable - the window function adds a Segment and a Sequence Project

SELECT Id
      ,TextField
      ,ROW_NUMBER() OVER (ORDER BY Id) AS RowNumber
FROM   dbo.ClusteredTable
WHERE  Id BETWEEN 1 AND 100
OPTION (MAXDOP 1)
GO

/*
-----------------------------------------------------------------------------------------------------------------------
    Part 4 - Filter
-----------------------------------------------------------------------------------------------------------------------

    A predicate the access path can use becomes a seek bound or a residual on the scan. One it cannot use lands on a Filter
    above, and every row has to be read from a page before the Filter can reject it. These two return the same rows by very
    different amounts of work.
*/

-- 4.1 Filter above an aggregate
--     HAVING cannot be applied until the group is complete, so it lands on a Filter above the aggregate.

SELECT Category
      ,COUNT(*) AS RowTotal
FROM   dbo.DuplicateKeyTable
GROUP  BY Category
HAVING COUNT(*) > 50
OPTION (ORDER GROUP, MAXDOP 1)
GO

-- 4.2 The same rows without a Filter
--     Compare the row counts on the scan below each. The seek reads what it returns, the Filter reads everything and throws
--     most of it away.

SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  Id BETWEEN 500 AND 520
OPTION (MAXDOP 1)
GO
