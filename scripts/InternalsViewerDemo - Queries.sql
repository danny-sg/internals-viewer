
/*  
-------------------------------------------------------------------------------------------------------------------
Test Queries
-------------------------------------------------------------------------------------------------------------------
*/

USE InternalsViewerDemo
GO

-- SELECT * (Scan)
SELECT * 
FROM   dbo.ClusteredTable

-- Index seek
SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  TextField LIKE 'Clustered table row 400%' OR Id = 1000
GO

-- Index scan
SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  TextField LIKE '%Clustered table row 400%'
GO

-- RID Lookup
SELECT *
FROM   dbo.HeapTable
WHERE  Id = 500
GO


-- Joins
-- Nested Loops - one clustered index seek per outer row: a
-- repeating drumbeat of single-page reads

SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER LOOP JOIN dbo.ClusteredTable c
         ON c.Id = h.Id
GO

-- Merge Join - both inputs already sorted on the key, zipped
-- together in one interleaved in-order pass. If the plan shows
-- a Sort on the heap side, force the ordered index scan with:
-- dbo.HeapTable h WITH (INDEX(IX_HeapTable_NumberField))

SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER MERGE JOIN dbo.ClusteredTable c
         ON c.Id = h.Id
GO

-- Hash Match - build phase reads the heap into a hash table
-- (nothing streams downstream), then the probe phase streams the
-- larger input through Id

SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER HASH JOIN dbo.ClusteredTable c
         ON c.Id = h.Id
GO

-- No hint - see which strategy the optimizer picks by itself

SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER JOIN dbo.ClusteredTable c
         ON c.Id = h.Id
WHERE  h.Id = 100
GO

SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       LEFT JOIN dbo.ClusteredTable c
         ON c.Id = h.Id
GO

-- Lock Escalation
SELECT *
FROM dbo.ClusteredTable
WITH (UPDLOCK, HOLDLOCK)

-- Locked
SELECT *
FROM dbo.ClusteredTable
WITH (TABLOCKX)

-- Modification Queries
-- Heap Insert
INSERT INTO dbo.HeapTable VALUES (999222, 'Inserted Row', 'New Row 1')

-- Clustered Index Insert
INSERT INTO dbo.ClusteredTable VALUES ('New Row', GETDATE())

-- Heap Delete
DELETE FROM dbo.HeapTable WHERE Id = 1000

SELECT * FROM dbo.HeapTable
INSERT INTO dbo.HeapTable VALUES (100, 'New Row', 'New Row')

-- Clustered Index Delete
DELETE FROM dbo.ClusteredTable WHERE Id = 9999
-- 
-- Cleanup
-- 
/*
USE master
GO
DROP DATABASE InternalsViewerTutorial
GO
*/

SELECT * FROM ClusteredTable

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