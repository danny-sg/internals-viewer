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
Cleanup
-----------------------------------------------------------------------------------------------------------------------

----
USE master;
GO

DROP DATABASE InternalsViewerDemo;
GO
----

*/
