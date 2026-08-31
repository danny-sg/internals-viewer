/*
-----------------------------------------------------------------------------------------------------------------------
    Internals Viewer
 -----------------------------------------------------------------------------------------------------------------------
    Demo Database

    - dbo.HeapTable              - Heap
      + ix_HeapTable_NumberField - Non-Clustered index on heap

    - dbo.ClusteredTable         - Clustered Index table
      + ix_ClusteredTable_TextField - Non-clustered index on clustered index

    - dbo.LobTable               - Table with LOB (Large Object) structures

    - dbo.DuplicateKeyTable      - Non-Unique Clustered Index table

    - dbo.CompressedTable        - Clustered index PAGE compression table

    - dbo.ColumnstoreTable       - Clustered columnstore, built in TradeId order over four full rowgroups
                                   Segment elimination, RLE, bit packing, string and numeric
                                   dictionaries, and variable length data (VLD) storage

    - dbo.ColumnstoreDeltaTable  - Clustered columnstore carrying a delete bitmap and an open delta rowgroup
     
-----------------------------------------------------------------------------------------------------------------------
*/


/*  
-----------------------------------------------------------------------------------------------------------------------
Database Setup
-----------------------------------------------------------------------------------------------------------------------
*/

USE master
GO

CREATE DATABASE InternalsViewerDemo
GO

USE InternalsViewerDemo
GO

-- Tables

CREATE TABLE dbo.HeapTable
(
    Id             INT          NOT NULL
   ,TextField      VARCHAR(100) NOT NULL
   ,FixedTextField CHAR(10)     NOT NULL
);
GO

CREATE INDEX ix_HeapTable_Id
    ON dbo.HeapTable (Id);
GO

CREATE TABLE dbo.ClusteredTable
(
    Id          INT IDENTITY(1,1) NOT NULL
   ,TextField   VARCHAR(100)      NOT NULL
   ,CreatedDate DATETIME2         NOT NULL
   ,CONSTRAINT pk_ClusteredTable PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE INDEX ix_ClusteredTable_CreatedDate_Desc ON dbo.ClusteredTable (CreatedDate DESC);

CREATE INDEX ix_ClusteredTable_TextField 
    ON dbo.ClusteredTable (TextField);
GO

CREATE TABLE dbo.DuplicateKeyTable
(
    Category  INT          NOT NULL
   ,TextField VARCHAR(100) NOT NULL
);
GO

CREATE CLUSTERED INDEX ix_DuplicateKeyTable_Category 
    ON dbo.DuplicateKeyTable (Category);
GO

CREATE TABLE dbo.LobTable
(
    Id       INT          NOT NULL
   ,LobField VARCHAR(MAX) NOT NULL
);
GO

CREATE TABLE dbo.ForwardingTable
(
    Id        INT           NOT NULL
   ,TextField VARCHAR(8000) NOT NULL
)
GO

CREATE TABLE dbo.CompressedTable
(
    Id            BIGINT IDENTITY(1,1) NOT NULL
   ,NumberField1  INT           NOT NULL
   ,NumberField2  INT           NOT NULL
   ,NumberField3  BIGINT        NOT NULL
   ,TextField1    VARCHAR(50)   NOT NULL
   ,TextField2    VARCHAR(100)  NOT NULL
   ,TextField3    CHAR(20)      NOT NULL
   ,DateField1    DATE          NOT NULL
   ,DateField2    DATETIME2     NOT NULL
   ,DecimalField1 DECIMAL(18,2) NOT NULL
    CONSTRAINT pk_CompressedTable
        PRIMARY KEY CLUSTERED (Id)
        WITH (DATA_COMPRESSION = PAGE)
);
GO

-- Data

-- dbo.ClusteredTable - 100,000 rows

INSERT INTO dbo.ClusteredTable
        (TextField, CreatedDate)
SELECT  TOP (100000)
        CONCAT('Clustered table row ', ROW_NUMBER() OVER (ORDER BY (SELECT NULL)))
       ,SYSDATETIME()
FROM    sys.all_columns AS c1
        CROSS JOIN sys.all_columns AS c2
GO

-- dbo.HeapTable - 5,000 rows
INSERT INTO dbo.HeapTable
        (Id, TextField, FixedTextField)
SELECT  Id
       ,CONCAT('Row Id: ', Id)
       ,LEFT(CONCAT('Row ', Id), 10)
FROM    dbo.ClusteredTable
WHERE   Id % 20 = 0
GO

-- dbo.CompressedTable - 1,000,000 rows
;WITH n AS
(
    SELECT TOP (1000000)
           ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
    FROM   sys.all_objects a
           CROSS JOIN sys.all_objects b
)
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
SELECT rn % 100
      ,rn % 10
      ,rn % 1000
      ,CONCAT('Value_', rn % 50)
      ,CONCAT('Description_', rn % 20)
      ,CONCAT('Category_', RIGHT('00' + CAST(rn % 10 AS VARCHAR(2)), 2))
      ,DATEADD(DAY, rn % 365, '2026-01-01')
      ,DATEADD(MINUTE, rn % 1440, '2026-01-01')
      ,CAST((rn % 1000) * 1.25 AS DECIMAL(18,2))
FROM   n;
GO

-- dbo.LobTable - Three rows for different LOB storage

-- Row 1 - 500 bytes - stays in the row like a normal VARCHAR
INSERT INTO dbo.LobTable
VALUES (1, REPLICATE('A', 500))

-- Row 2 - 8,020 bytes - just over the 8,000 byte in row limit in the data row
INSERT INTO dbo.LobTable
VALUES (2, REPLICATE(CAST('B' AS VARCHAR(MAX)), 8020))

-- Row 3 - 160,000 bytes - split into ~8 KB chunks
INSERT INTO dbo.LobTable
VALUES (3, REPLICATE(CAST('C' AS VARCHAR(MAX)), 160000))
GO

-- dbo.ForwardingTable - Three rows, one forwarded on update
INSERT INTO dbo.ForwardingTable
        (Id, TextField)
VALUES  (1, REPLICATE('A', 2500))
       ,(2, REPLICATE('B', 2500))
       ,(3, REPLICATE('C', 2500))
GO

UPDATE dbo.ForwardingTable
SET    TextField = REPLICATE('B', 7000)
WHERE  Id = 2
GO


/*  
-----------------------------------------------------------------------------------------------------------------------
Columnstore
-----------------------------------------------------------------------------------------------------------------------

 Each column of dbo.ColumnstoreTable is shaped to land on a different segment encoding:

    TradeId       - sequential, so a rowgroup's min/max never overlap its neighbours' and a range predicate
                    eliminates whole segments. Bit packed at the width its 1,048,576 distinct ids need
    TradeDate     - one date every 4,096 rows, so the segment is almost entirely literal RLE runs, and the
                    dates are ordered so this column eliminates segments as well
    StatusCode    - 900 rows of one value then 100 rows of varying values, repeating, which mixes literal RLE
                    runs and bit packed runs in the same segment
    Quantity      - the full 1 to 1,000 range in every rowgroup, so it bit packs and eliminates nothing.
                    The counterpart to TradeId
    Symbol        - 500 distinct values shared by every rowgroup, giving a global string dictionary
    MarketValue   - 2,000 float values spread over twenty orders of magnitude, which is too sparse to bit pack
                    and bounded enough to build a numeric dictionary from
    ReferenceCode - distinct on every row, which is past what a dictionary is worth and falls back to VLD
                    pages holding the values Huffman compressed
    SettlementId  - sixteen bytes wide, so VLD again, but random enough that the pages are stored raw. Null
                    for 200 rows in every 1,000, which puts null repeat runs in the RLE array

-----------------------------------------------------------------------------------------------------------------------
*/

CREATE TABLE dbo.ColumnstoreTable
(
    TradeId       BIGINT           NOT NULL
   ,TradeDate     DATE             NOT NULL
   ,StatusCode    SMALLINT         NOT NULL
   ,Quantity      INT              NOT NULL
   ,Symbol        VARCHAR(10)      NOT NULL
   ,MarketValue   FLOAT            NOT NULL
   ,ReferenceCode VARCHAR(30)      NOT NULL
   ,SettlementId  UNIQUEIDENTIFIER NULL
);
GO

CREATE TABLE dbo.ColumnstoreDeltaTable
(
    Id          INT           NOT NULL
   ,Category    VARCHAR(20)   NOT NULL
   ,Amount      DECIMAL(18,2) NOT NULL
   ,CreatedDate DATETIME2     NOT NULL
);
GO

-- dbo.ColumnstoreTable - 4,194,304 rows, which is four whole rowgroups
;WITH n AS
(
    SELECT TOP (4194304)
           ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
    FROM   sys.all_columns AS c1
           CROSS JOIN sys.all_columns AS c2
)
INSERT INTO dbo.ColumnstoreTable
(
    TradeId,
    TradeDate,
    StatusCode,
    Quantity,
    Symbol,
    MarketValue,
    ReferenceCode,
    SettlementId
)
SELECT rn
      ,DATEADD(DAY, rn / 4096, '2020-01-01')
      ,CASE WHEN rn % 1000 < 900 THEN 0 ELSE CAST(rn % 97 AS SMALLINT) END
      ,rn % 1000 + 1
      ,CONCAT('SYM', RIGHT(CONCAT('000', rn % 500), 3))
      ,POWER(CAST(10.0 AS FLOAT), (rn % 2000) / 100.0 - 10.0)
      ,CONCAT('REF-', RIGHT(CONCAT('0000000000', rn), 10))
      ,CASE WHEN rn % 1000 < 200 THEN NULL ELSE NEWID() END
FROM   n;
GO

-- Building the rowstore index first and converting it is what puts the rowgroups in TradeId order, which is
-- what segment elimination needs. MAXDOP 1 keeps every rowgroup full rather than one per thread
CREATE CLUSTERED INDEX cci_ColumnstoreTable
    ON dbo.ColumnstoreTable (TradeId);
GO

CREATE CLUSTERED COLUMNSTORE INDEX cci_ColumnstoreTable
    ON dbo.ColumnstoreTable
    WITH (DROP_EXISTING = ON, MAXDOP = 1);
GO

-- dbo.ColumnstoreDeltaTable - 2,097,152 rows, which is two whole rowgroups
;WITH n AS
(
    SELECT TOP (2097152)
           ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
    FROM   sys.all_columns AS c1
           CROSS JOIN sys.all_columns AS c2
)
INSERT INTO dbo.ColumnstoreDeltaTable
(
    Id,
    Category,
    Amount,
    CreatedDate
)
SELECT rn
      ,CONCAT('Category_', rn % 25)
      ,CAST((rn % 5000) * 1.75 AS DECIMAL(18,2))
      ,DATEADD(MINUTE, rn % 1440, '2026-01-01')
FROM   n;
GO

CREATE CLUSTERED COLUMNSTORE INDEX cci_ColumnstoreDeltaTable
    ON dbo.ColumnstoreDeltaTable
    WITH (MAXDOP = 1);
GO

-- Deletes against a compressed rowgroup only set bits in its delete bitmap, the rows stay in the segments.
-- Confined to the first rowgroup so the second one is left with no bitmap to compare against
DELETE FROM dbo.ColumnstoreDeltaTable
WHERE  Id <= 1048576
       AND Id % 500 = 0;
GO

-- Under the 102,400 row bulk threshold an insert goes to the delta store, and a delta rowgroup stays OPEN
-- until it fills or the tuple mover is asked to close it
;WITH n AS
(
    SELECT TOP (5000)
           ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
    FROM   sys.all_columns AS c1
)
INSERT INTO dbo.ColumnstoreDeltaTable
(
    Id,
    Category,
    Amount,
    CreatedDate
)
SELECT rn + 9000000
      ,CONCAT('Delta_', rn % 5)
      ,CAST((rn % 5000) * 1.75 AS DECIMAL(18,2))
      ,SYSDATETIME()
FROM   n;
GO

/*  
-----------------------------------------------------------------------------------------------------------------------
Cleanup
-----------------------------------------------------------------------------------------------------------------------

----
USE master;
GO

DROP DATABASE InternalsViewerDemo;
GO
----

*/
