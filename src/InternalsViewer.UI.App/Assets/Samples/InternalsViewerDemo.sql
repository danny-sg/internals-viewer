-- SELECT * (Scan)
SELECT *
FROM   dbo.ClusteredTable
GO

-- Index seek
SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  Id = 1000
GO

-- Index seek (multiple)
SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  Id IN (1000, 2000)
GO

-- Clustered Index scan
SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  TextField LIKE '%Clustered table row 400%'
GO

-- Heap Scan
SELECT *
FROM   dbo.HeapTable
WHERE  TextField = ''
GO

-- RID Lookup
SELECT *
FROM   dbo.HeapTable
WHERE  Id = 500
GO

-- Nested Loops Join
SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER LOOP JOIN dbo.ClusteredTable c
         ON c.Id = h.Id
GO

-- Merge Join
SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER MERGE JOIN dbo.ClusteredTable c
         ON c.Id = h.Id
GO

-- Hash Match
SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER HASH JOIN dbo.ClusteredTable c
         ON c.Id = h.Id
GO

-- Join - No hint
SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER JOIN dbo.ClusteredTable c
         ON c.Id = h.Id
WHERE  h.Id = 100
GO

-- Columnstore Scan - every segment of the columns the query touches
SELECT COUNT_BIG(*)
      ,SUM(Quantity)
FROM   dbo.ColumnstoreTable
GO

-- Segment Elimination - TradeId is ordered, so one rowgroup is read and three are skipped
SELECT SUM(Quantity)
FROM   dbo.ColumnstoreTable
WHERE  TradeId BETWEEN 2000000 AND 2000100
GO

-- No Elimination - Quantity covers the same range in every rowgroup, so nothing can be skipped
SELECT COUNT_BIG(*)
FROM   dbo.ColumnstoreTable
WHERE  Quantity = 500
GO

-- String Dictionary - 500 values shared by every rowgroup
SELECT Symbol
      ,COUNT_BIG(*)
FROM   dbo.ColumnstoreTable
GROUP BY Symbol
GO

-- Numeric Dictionary - float values too sparse to bit pack
SELECT COUNT_BIG(DISTINCT MarketValue)
FROM   dbo.ColumnstoreTable
GO

-- Variable Length Data - distinct on every row, and null for 200 rows in every 1,000
SELECT TOP (100)
       ReferenceCode
      ,SettlementId
FROM   dbo.ColumnstoreTable
WHERE  TradeId BETWEEN 1000 AND 2000
GO

-- Delete Bitmap - the rows stay in the segments, the bitmap marks them gone
DELETE FROM dbo.ColumnstoreDeltaTable
WHERE  Id BETWEEN 1200000 AND 1200100
GO

-- Open Delta Store - under the bulk threshold, so this lands in the delta rowgroup
INSERT INTO dbo.ColumnstoreDeltaTable
        (Id, Category, Amount, CreatedDate)
VALUES  (9500001, 'Delta_Insert', 99.99, SYSDATETIME())
GO

-- Lock Escalation
SELECT *
FROM dbo.ClusteredTable
WITH (UPDLOCK, HOLDLOCK)
GO

-- Locked
SELECT *
FROM dbo.ClusteredTable
WITH (TABLOCKX)
GO

-- Heap Insert
INSERT INTO dbo.HeapTable VALUES (999222, 'Inserted Row', 'New Row 1')
GO

-- Clustered Index Insert
INSERT INTO dbo.ClusteredTable VALUES ('New Row', GETDATE())
GO

-- Heap Delete
DELETE FROM dbo.HeapTable WHERE Id = 1000
GO

-- Clustered Index Delete
DELETE FROM dbo.ClusteredTable WHERE Id = 9999
GO

INSERT INTO dbo.CompressedTable
(
    NumberField1,
    NumberField2,
    NumberField3,
    TextField1,
    TextField2,
    TextField3,
    DateField1,
    DateField2,
    DecimalField1
)
SELECT 654321
      ,54321
      ,4321
      ,'Value_Insert'
      ,'Description_Insert'
      ,'Category_Insert'
      ,GETDATE()
      ,GETDATE()
      ,67543.21
GO
