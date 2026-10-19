/*
-----------------------------------------------------------------------------------------------------------------------
    Internals Viewer
-----------------------------------------------------------------------------------------------------------------------
    Operator queries for Full Trace

    One plan shape per query, all serial and no spools. Part 1 runs against the demo tables as they are. Part 2
    creates three small order tables for the shapes the demo tables can't produce. Every plan was checked against a
    demo database at compatibility level 170, where a query over the 1,000,000 row CompressedTable can switch to
    batch mode on rowstore. The row mode queries that would switch say DISALLOW_BATCH_MODE.

    Select one statement and choose Trace Query Selection.
-----------------------------------------------------------------------------------------------------------------------
*/

USE InternalsViewerDemo
GO

/*
-----------------------------------------------------------------------------------------------------------------------
    Part 1 - Demo tables
-----------------------------------------------------------------------------------------------------------------------
*/

-- Table Scan with a predicate
SELECT Id, TextField FROM dbo.HeapTable WHERE TextField LIKE '%500' OPTION (MAXDOP 1)
GO

-- Clustered Index Scan with a predicate. RowSpacer keeps it off the narrower TextField index
SELECT Id, RowSpacer FROM dbo.ClusteredTable WHERE TextField LIKE '%99' OPTION (MAXDOP 1)
GO

-- Clustered Index Seek
SELECT Id, TextField, RowSpacer FROM dbo.ClusteredTable WHERE Id BETWEEN 100 AND 120 OPTION (MAXDOP 1)
GO

-- Index Seek and Key Lookup
SELECT Id, TextField, RowSpacer FROM dbo.ClusteredTable WHERE TextField LIKE 'Clustered table row 100%' OPTION (MAXDOP 1)
GO

-- Index Seek and RID Lookup
SELECT Id, TextField FROM dbo.HeapTable WHERE Id BETWEEN 100 AND 110 OPTION (MAXDOP 1)
GO

-- Nested Loops
SELECT h.Id, c.TextField
FROM   dbo.HeapTable AS h
       INNER LOOP JOIN dbo.ClusteredTable AS c ON c.Id = h.Id
WHERE  h.Id <= 10
OPTION (MAXDOP 1)
GO

-- Merge Join
SELECT h.Id, c.TextField
FROM   dbo.HeapTable AS h
       INNER MERGE JOIN dbo.ClusteredTable AS c ON c.Id = h.Id
WHERE  h.Id <= 200
OPTION (MAXDOP 1)
GO

-- Hash Match join. The build side is read inside the Hash Match's Open, before it returns a row
SELECT h.Id, h.TextField, c.TextField
FROM   dbo.HeapTable AS h
       INNER HASH JOIN dbo.ClusteredTable AS c ON c.Id = h.Id
WHERE  h.Id <= 200
OPTION (MAXDOP 1)
GO

-- Merge Join (Left Anti Semi Join)
SELECT c.Id
FROM   dbo.ClusteredTable AS c
WHERE  c.Id <= 50
       AND NOT EXISTS (SELECT 1 FROM dbo.HeapTable AS h WHERE h.Id = c.Id AND h.TextField LIKE '%1')
OPTION (MAXDOP 1)
GO

-- Stream Aggregate with no grouping
SELECT COUNT(*) AS Rows, MIN(TextField) AS FirstText FROM dbo.HeapTable WHERE Id <= 200 OPTION (MAXDOP 1)
GO

-- Sort feeding a Stream Aggregate
SELECT NumberField2, COUNT(*) AS Rows
FROM   dbo.CompressedTable
WHERE  Id <= 2000
GROUP BY NumberField2
OPTION (ORDER GROUP, MAXDOP 1)
GO

-- Hash Match aggregate
SELECT NumberField2, COUNT(*) AS Rows
FROM   dbo.CompressedTable
WHERE  Id <= 2000
GROUP BY NumberField2
OPTION (HASH GROUP, MAXDOP 1, USE HINT ('DISALLOW_BATCH_MODE'))
GO

-- Filter above an aggregate. HAVING can't be pushed below the grouping, so 6 of the 10 groups are rejected here
SELECT NumberField2, COUNT(*) AS Rows
FROM   dbo.CompressedTable
WHERE  Id <= 2000
GROUP BY NumberField2
HAVING MAX(NumberField1) > 95
OPTION (HASH GROUP, MAXDOP 1, USE HINT ('DISALLOW_BATCH_MODE'))
GO

-- Filter on a window function. The row number only exists above the Sequence Project, so nine rows in ten are rejected there
SELECT Id, TextField, rn
FROM   (SELECT Id, TextField, ROW_NUMBER() OVER (ORDER BY TextField) AS rn FROM dbo.HeapTable WHERE Id <= 100) AS x
WHERE  rn % 10 = 0
OPTION (MAXDOP 1)
GO

-- Filter with a startup expression. It is checked once, before the seek below it is opened
SELECT Id, TextField FROM dbo.HeapTable WHERE Id <= 10 AND @@TRANCOUNT = 0 OPTION (MAXDOP 1)
GO

-- Sort
SELECT Id, TextField FROM dbo.HeapTable WHERE Id <= 200 ORDER BY TextField DESC OPTION (MAXDOP 1)
GO

-- Top N Sort
SELECT TOP (10) Id, TextField FROM dbo.HeapTable ORDER BY TextField DESC OPTION (MAXDOP 1)
GO

-- Top. The row goal stops the scan after five rows
SELECT TOP (5) Id, TextField FROM dbo.ClusteredTable OPTION (MAXDOP 1)
GO

-- Distinct Sort
SELECT DISTINCT TextField3 FROM dbo.CompressedTable WHERE Id <= 2000 OPTION (ORDER GROUP, MAXDOP 1)
GO

-- Compute Scalar
SELECT Id, UPPER(TextField) AS UpperText, LEN(TextField) * 2 AS DoubleLength FROM dbo.HeapTable WHERE Id <= 20 OPTION (MAXDOP 1)
GO

-- Concatenation
SELECT Id FROM dbo.HeapTable WHERE Id <= 5
UNION ALL
SELECT Id FROM dbo.ClusteredTable WHERE Id > 9995
OPTION (MAXDOP 1)
GO

-- Segment and Sequence Project
SELECT Id, ROW_NUMBER() OVER (PARTITION BY Id % 5 ORDER BY Id) AS rn FROM dbo.HeapTable WHERE Id <= 100 OPTION (MAXDOP 1)
GO

-- Assert. ix_HeapTable_Id isn't unique, so the scalar subquery checks it found at most one row
SELECT c.Id, (SELECT h.TextField FROM dbo.HeapTable AS h WHERE h.Id = c.Id) AS HeapText
FROM   dbo.ClusteredTable AS c
WHERE  c.Id <= 20
OPTION (MAXDOP 1)
GO

-- LOB values - in row, row overflow and LOB tree
SELECT Id, LEN(LobField) AS LobLength, LEFT(LobField, 10) AS LobStart FROM dbo.LobTable OPTION (MAXDOP 1)
GO

-- Forwarded record. The scan follows the forwarding pointer for row 2
SELECT Id, LEN(TextField) AS TextLength FROM dbo.ForwardingTable OPTION (MAXDOP 1)
GO

-- Page compressed seek
SELECT * FROM dbo.CompressedTable WHERE Id BETWEEN 5000 AND 5050 OPTION (MAXDOP 1)
GO

-- Columnstore scan and batch mode Hash Match aggregate. TradeId eliminates all but the first rowgroup
SELECT Symbol, COUNT(*) AS Rows, SUM(Quantity) AS Quantity
FROM   dbo.ColumnstoreTable
WHERE  TradeId <= 50000
GROUP BY Symbol
OPTION (MAXDOP 1)
GO

-- Batch mode Filter under a Top, handed to row mode through Batch To Row
SELECT TOP (10) TradeId, ReferenceCode FROM dbo.ColumnstoreTable WHERE RIGHT(ReferenceCode, 3) = '000' OPTION (MAXDOP 1)
GO

-- Columnstore with a delete bitmap and an open delta rowgroup
SELECT COUNT(*) AS Rows, SUM(Amount) AS Amount FROM dbo.ColumnstoreDeltaTable WHERE Id > 2090000 OPTION (MAXDOP 1)
GO

/*
-----------------------------------------------------------------------------------------------------------------------
    Part 2 - Order tables
    200 customers, 2,000 orders and about 5,000 order lines. Customers 191 to 200 have no orders, for the outer and
    anti joins.
-----------------------------------------------------------------------------------------------------------------------
*/

CREATE TABLE dbo.Customer
(
    CustomerId INT         NOT NULL
   ,Region     CHAR(2)     NOT NULL
   ,Name       VARCHAR(50) NOT NULL
   ,CONSTRAINT pk_Customer PRIMARY KEY CLUSTERED (CustomerId)
);
GO

CREATE TABLE dbo.SalesOrder
(
    OrderId    INT           NOT NULL
   ,CustomerId INT           NOT NULL
   ,OrderDate  DATE          NOT NULL
   ,Amount     DECIMAL(10,2) NOT NULL
   ,CONSTRAINT pk_SalesOrder PRIMARY KEY CLUSTERED (OrderId)
);
GO

CREATE INDEX ix_SalesOrder_CustomerId
    ON dbo.SalesOrder (CustomerId);
GO

CREATE TABLE dbo.OrderLine
(
    OrderId    INT         NOT NULL
   ,LineNumber INT         NOT NULL
   ,Product    VARCHAR(20) NOT NULL
   ,Quantity   INT         NOT NULL
   ,CONSTRAINT pk_OrderLine PRIMARY KEY CLUSTERED (OrderId, LineNumber)
);
GO

;WITH n AS
(
    SELECT TOP (200) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
    FROM   sys.all_objects
)
INSERT INTO dbo.Customer (CustomerId, Region, Name)
SELECT rn
      ,CHOOSE(rn % 4 + 1, 'NA', 'EU', 'AP', 'SA')
      ,CONCAT('Customer ', rn)
FROM   n;
GO

;WITH n AS
(
    SELECT TOP (2000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
    FROM   sys.all_objects AS a
           CROSS JOIN sys.all_objects AS b
)
INSERT INTO dbo.SalesOrder (OrderId, CustomerId, OrderDate, Amount)
SELECT rn
      ,(rn - 1) % 190 + 1
      ,DATEADD(DAY, rn % 365, '2026-01-01')
      ,CAST((rn * 37) % 1000 + 0.99 AS DECIMAL(10,2))
FROM   n;
GO

INSERT INTO dbo.OrderLine (OrderId, LineNumber, Product, Quantity)
SELECT o.OrderId
      ,l.LineNumber
      ,CONCAT('Product ', (o.OrderId + l.LineNumber) % 50)
      ,(o.OrderId * l.LineNumber) % 10 + 1
FROM   dbo.SalesOrder AS o
       CROSS JOIN (VALUES (1), (2), (3), (4)) AS l (LineNumber)
WHERE  l.LineNumber <= o.OrderId % 4 + 1;
GO

-- Merge Join, one to many. Both inputs arrive in OrderId order from their clustered indexes
SELECT o.OrderId, l.LineNumber, l.Product
FROM   dbo.SalesOrder AS o
       INNER MERGE JOIN dbo.OrderLine AS l ON l.OrderId = o.OrderId
WHERE  o.OrderId <= 100
OPTION (MAXDOP 1)
GO

-- Merge Join, many to many. Duplicate keys on both sides make the join keep the inner rows of each key to replay them
SELECT a.OrderId, b.OrderId AS OtherOrderId
FROM   dbo.SalesOrder AS a
       INNER MERGE JOIN dbo.SalesOrder AS b ON b.CustomerId = a.CustomerId
WHERE  a.CustomerId <= 3
OPTION (MAXDOP 1)
GO

-- Stream Aggregate on input already in group order, so no Sort
SELECT OrderId, SUM(Quantity) AS Quantity
FROM   dbo.OrderLine
WHERE  OrderId <= 200
GROUP BY OrderId
OPTION (MAXDOP 1)
GO

-- Left outer Hash Match. Customers 191 to 200 come out with NULL orders
SELECT c.CustomerId, c.Name, o.OrderId
FROM   dbo.Customer AS c
       LEFT OUTER HASH JOIN dbo.SalesOrder AS o ON o.CustomerId = c.CustomerId
WHERE  c.CustomerId > 185
OPTION (MAXDOP 1)
GO

-- Filter above an outer join. The predicate needs the NULLs the join adds, so it can't be pushed below it
SELECT c.CustomerId, o.OrderId, o.Amount
FROM   dbo.Customer AS c
       LEFT OUTER HASH JOIN dbo.SalesOrder AS o ON o.CustomerId = c.CustomerId
WHERE  c.CustomerId > 185
       AND ISNULL(o.Amount, 0) < 100
OPTION (MAXDOP 1)
GO

-- Constant Scan joined to a table
SELECT v.Label, c.Name
FROM   (VALUES (1, 'First'), (2, 'Second'), (3, 'Third')) AS v (CustomerId, Label)
       INNER JOIN dbo.Customer AS c ON c.CustomerId = v.CustomerId
OPTION (MAXDOP 1)
GO

