/*===================================================================================================================
    Internals Viewer - Record Format Decode Stress Test
    Target : SQL Server 2025 (also runs on 2016+; a couple of 2022+ notes are flagged inline)
    Purpose: Exercise every FixedVar and CD (Column Descriptor / compressed) record decode path in the engine.

    Scope (per request):
        - FixedVar format ......... uncompressed data + index records
        - CD format ............... ROW and PAGE compressed data + index records
        - NOT covered ............. XML, (N)TEXT/IMAGE legacy LOB, columnstore, vector  (engine does not decode these)

    What this builds:
        A database 'InternalsViewerStressTest' containing one table per scenario. Each table is documented with the
        exact decode path(s) it targets. Section Z at the bottom prints a page locator so you can open the right
        page:slot directly in Internals Viewer.

    Re-runnable: drops and recreates the database every time. Comment out the DROP if you want to keep data.

    Record-format primer (what the engine keys off):
        FixedVar  -> Status Bits A/B, fixed data, null bitmap, var-length offset array, LOB/SLOB pointers,
                     forwarding stubs, and the index variants (clustered/non-clustered x root/node/leaf,
                     unique/non-unique uniquifier, included cols, down-page pointers, sentinel low-key).
        CD        -> 1-byte header, 1/2-byte column count, 4-bit column-descriptor array, short + long data
                     regions with cluster arrays, bit packing, page-compression Compression Info
                     (anchor record / column prefix + dictionary / PageSymbol), versioning.
===================================================================================================================*/

SET NOCOUNT ON;
GO

USE master;
GO

IF DB_ID(N'InternalsViewerStressTest') IS NOT NULL
BEGIN
    ALTER DATABASE InternalsViewerStressTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE InternalsViewerStressTest;
END
GO

CREATE DATABASE InternalsViewerStressTest;
GO

ALTER DATABASE InternalsViewerStressTest SET RECOVERY SIMPLE;
GO

USE InternalsViewerStressTest;
GO

/*===================================================================================================================
    SECTION A - FixedVar format (uncompressed)
===================================================================================================================*/

/*-------------------------------------------------------------------------------------------------------------------
    A1. Heap - every FIXED-LENGTH data type the engine decodes.
        Decode paths: Status Bits A/B, fixed-length region, null bitmap (all-present and all-null rows).
        Value profiles include min / max / zero / negative / boundary values for each type.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A1_Heap_FixedTypes
(
    c_bit              bit,
    c_tinyint          tinyint,
    c_smallint         smallint,
    c_int              int,
    c_bigint           bigint,
    c_decimal_9_0      decimal(9,0),     -- 5-byte numeric
    c_decimal_19_4     decimal(19,4),    -- 9-byte numeric
    c_decimal_38_10    decimal(38,10),   -- 17-byte numeric (max precision)
    c_numeric_28_14    numeric(28,14),
    c_smallmoney       smallmoney,
    c_money            money,
    c_real             real,
    c_float            float,
    c_smalldatetime    smalldatetime,
    c_datetime         datetime,
    c_date             date,             -- 3-byte
    c_time7            time(7),           -- 5-byte
    c_time0            time(0),           -- 3-byte (scale changes storage width)
    c_datetime2_7      datetime2(7),      -- 8-byte
    c_datetime2_0      datetime2(0),      -- 6-byte
    c_dto              datetimeoffset(7), -- 10-byte
    c_guid             uniqueidentifier,
    c_rowversion       rowversion,        -- 8-byte fixed binary (engine sees binary(8))
    c_char10           char(10),          -- fixed text (blank padded)
    c_nchar5           nchar(5),
    c_binary8          binary(8)
);
GO
-- Row 1: representative/boundary values
INSERT dbo.A1_Heap_FixedTypes
    (c_bit,c_tinyint,c_smallint,c_int,c_bigint,c_decimal_9_0,c_decimal_19_4,c_decimal_38_10,c_numeric_28_14,
     c_smallmoney,c_money,c_real,c_float,c_smalldatetime,c_datetime,c_date,c_time7,c_time0,c_datetime2_7,
     c_datetime2_0,c_dto,c_guid,c_char10,c_nchar5,c_binary8)
VALUES
    (1,255,-32768,-2147483648,9223372036854775807,123456789,1234567890123.4567,
     -9999999999999999999999999999.9999999999,12345678901234.12345678901234,
     -214748.3648,922337203685477.5807,3.14159E0,2.718281828459045E0,'2079-06-06 23:59','9999-12-31 23:59:59.997',
     '2026-06-26','23:59:59.9999999','23:59:59','9999-12-31 23:59:59.9999999','2026-06-26 00:00:00',
     '2026-06-26 12:34:56.7654321 +05:30','6F9619FF-8B86-D011-B42D-00C04FC964FF','ABC','XY',0xDEADBEEFCAFEF00D);
-- Row 2: zeros / minimums (exercises zero-byte values & sign bits)
INSERT dbo.A1_Heap_FixedTypes
    (c_bit,c_tinyint,c_smallint,c_int,c_bigint,c_decimal_9_0,c_decimal_19_4,c_decimal_38_10,c_numeric_28_14,
     c_smallmoney,c_money,c_real,c_float,c_smalldatetime,c_datetime,c_date,c_time7,c_time0,c_datetime2_7,
     c_datetime2_0,c_dto,c_guid,c_char10,c_nchar5,c_binary8)
VALUES
    (0,0,0,0,0,0,0.0000,0.0000000000,0.00000000000000,0,0,0,0,'1900-01-01','1753-01-01','0001-01-01',
     '00:00:00','00:00:00','0001-01-01','0001-01-01','0001-01-01 00:00:00 +00:00',
     '00000000-0000-0000-0000-000000000000','',N'',0x0000000000000000);
-- Row 3: all NULLs -> null bitmap fully set
INSERT dbo.A1_Heap_FixedTypes (c_int) VALUES (NULL);
GO

/*-------------------------------------------------------------------------------------------------------------------
    A2. Heap - VARIABLE-LENGTH data types + null bitmap interaction.
        Decode paths: variable-length column count, offset array, var data region, NULL vs empty-string ('')
        distinction (empty string = 0-length var col present; NULL = bit set, no offset entry).
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A2_Heap_VarTypes
(
    id           int            NOT NULL,
    c_varchar    varchar(50),
    c_nvarchar   nvarchar(50),
    c_varbinary  varbinary(50),
    c_sysname    sysname        NULL    -- nvarchar(128)
);
GO
INSERT dbo.A2_Heap_VarTypes VALUES
    (1, 'Hello',           N'Wide ☃ text', 0x010203,           N'dbo'),
    (2, '',                N'',                  0x,                 N''),          -- empty (0-length) vs null
    (3, NULL,              NULL,                NULL,               NULL),          -- all var cols null
    (4, REPLICATE('A',50), REPLICATE(N'B',50),  CAST(REPLICATE('C',50) AS varbinary(50)), N'maxlen'),
    (5, 'mix',             NULL,                0xFF,               NULL);          -- sparse-ish null pattern
GO

/*-------------------------------------------------------------------------------------------------------------------
    A3. Heap - mixed fixed + variable with a wide null bitmap (>8 columns forces a multi-byte null bitmap).
        Decode paths: null bitmap size = ceil(cols/8), interleaved fixed/var ordering.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A3_Heap_MixedWideNullBitmap
(
    f1 int, v1 varchar(20), f2 bigint, v2 varchar(20), f3 smallint, v3 nvarchar(20),
    f4 tinyint, v4 varbinary(20), f5 datetime, v5 varchar(20), f6 bit, f7 bit, f8 bit, f9 bit, f10 bit
);
GO
INSERT dbo.A3_Heap_MixedWideNullBitmap VALUES
    (1,'a',10,'b',2,N'c',3,0x04,'2026-01-01','d',1,0,1,0,1),
    (NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL),  -- every-bit-set null bitmap
    (5,NULL,50,'y',NULL,N'z',NULL,NULL,'2026-02-02',NULL,1,NULL,0,NULL,1);          -- checkerboard nulls
GO

/*-------------------------------------------------------------------------------------------------------------------
    A4. Heap - FORWARDED RECORDS (forwarding stub + forwarded record).
        Decode paths: RecordType = ForwardingStub, the 8-byte RID stub, and the forwarded record on its new page.
        Method: insert rows that fit, then widen a variable column so the row no longer fits -> SQL leaves a stub.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A4_Heap_Forwarded
(
    id   int          NOT NULL,
    pad  varchar(8000) NULL
);
GO
INSERT dbo.A4_Heap_Forwarded (id, pad) VALUES (1, REPLICATE('x', 100));
INSERT dbo.A4_Heap_Forwarded (id, pad) VALUES (2, REPLICATE('y', 100));
INSERT dbo.A4_Heap_Forwarded (id, pad) VALUES (3, REPLICATE('z', 100));
GO
-- Grow rows 1 & 3 so they must move off their original page -> forwarding stubs left behind.
UPDATE dbo.A4_Heap_Forwarded SET pad = REPLICATE('X', 5000) WHERE id = 1;
UPDATE dbo.A4_Heap_Forwarded SET pad = REPLICATE('Z', 5000) WHERE id = 3;
GO

/*-------------------------------------------------------------------------------------------------------------------
    A5. CLUSTERED index, UNIQUE primary key.
        Decode paths: clustered leaf = data record (FixedVar data), plus clustered Root/Node index records with
        down-page pointers and the left-edge sentinel (null low-key) record. Needs enough rows for >1 B-tree level.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A5_Clustered_PK
(
    id   int          NOT NULL CONSTRAINT PK_A5 PRIMARY KEY CLUSTERED,
    name varchar(100) NULL,
    val  bigint       NULL
);
GO
INSERT dbo.A5_Clustered_PK (id, name, val)
SELECT TOP (5000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       'name_' + CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS varchar(10)),
       ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) * 7
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO

/*-------------------------------------------------------------------------------------------------------------------
    A6. CLUSTERED index, NON-UNIQUE  -> UNIQUIFIER.
        Decode paths: the 4-byte uniquifier (stored in the variable region only for 2nd+ duplicate; absent = 0).
        Method: deliberately insert many duplicate key values.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A6_Clustered_NonUnique_Uniquifier
(
    grp  int          NOT NULL,
    note varchar(30)  NULL
);
GO
CREATE CLUSTERED INDEX CIX_A6 ON dbo.A6_Clustered_NonUnique_Uniquifier (grp);  -- NOT unique
GO
-- 1..200 each repeated 6 times => uniquifiers 0..5 per key
INSERT dbo.A6_Clustered_NonUnique_Uniquifier (grp, note)
SELECT v.grp, 'dup#' + CAST(d.n AS varchar(5))
FROM (SELECT TOP (200) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS grp FROM sys.all_objects) v
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6)) d(n);
GO

/*-------------------------------------------------------------------------------------------------------------------
    A7. NON-CLUSTERED index on a HEAP (key columns + RID lookup).
        Decode paths: NC leaf record carrying an 8-byte RID (Binary(8) row locator); NC root/node down-page
        pointers; non-unique NC where the RID participates in the key.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A7_Heap_With_NC
(
    id     int          NOT NULL,
    keycol int          NOT NULL,
    payload varchar(40) NULL
);
GO
INSERT dbo.A7_Heap_With_NC (id, keycol, payload)
SELECT TOP (3000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       (ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) % 500,   -- duplicate key values -> non-unique NC
       'p' + CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS varchar(10))
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO
CREATE NONCLUSTERED INDEX IX_A7_keycol ON dbo.A7_Heap_With_NC (keycol);            -- non-unique, RID-anchored
GO

/*-------------------------------------------------------------------------------------------------------------------
    A8. NON-CLUSTERED UNIQUE index on a HEAP.
        Decode paths: unique NC root does NOT carry the RID in the key (RID demoted to non-key); contrast with A7.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A8_Heap_With_NC_Unique
(
    id     int NOT NULL,
    ukey   int NOT NULL
);
GO
INSERT dbo.A8_Heap_With_NC_Unique (id, ukey)
SELECT TOP (3000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       ROW_NUMBER() OVER (ORDER BY (SELECT NULL))                 -- unique values
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO
CREATE UNIQUE NONCLUSTERED INDEX UX_A8_ukey ON dbo.A8_Heap_With_NC_Unique (ukey);
GO

/*-------------------------------------------------------------------------------------------------------------------
    A9. NON-CLUSTERED on a CLUSTERED table, with INCLUDED columns, both unique and non-unique.
        Decode paths: NC leaf carries the clustered key as the row locator (not a RID); INCLUDE columns appear in
        leaf only (not in node/root); unique vs non-unique key composition differences.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A9_Clustered_With_NC_Include
(
    id       int          NOT NULL CONSTRAINT PK_A9 PRIMARY KEY CLUSTERED,
    seekkey  int          NOT NULL,
    inc1     varchar(40)  NULL,
    inc2     datetime     NULL
);
GO
INSERT dbo.A9_Clustered_With_NC_Include (id, seekkey, inc1, inc2)
SELECT TOP (4000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       (ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) % 800,
       'inc_' + CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS varchar(10)),
       DATEADD(MINUTE, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), '2026-01-01')
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO
CREATE NONCLUSTERED INDEX IX_A9_nonunique ON dbo.A9_Clustered_With_NC_Include (seekkey) INCLUDE (inc1, inc2);
CREATE UNIQUE NONCLUSTERED INDEX UX_A9_unique ON dbo.A9_Clustered_With_NC_Include (id)  INCLUDE (inc1);
GO

/*-------------------------------------------------------------------------------------------------------------------
    A10. ROW-OVERFLOW data (SLOB).
        Decode paths: an in-row variable column replaced by a row-overflow pointer when total row > 8060 bytes;
        the overflow value lives on a ROW_OVERFLOW_DATA page. Two 5000-byte varchars guarantee overflow.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A10_RowOverflow
(
    id int          NOT NULL CONSTRAINT PK_A10 PRIMARY KEY,
    a  varchar(5000) NULL,
    b  varchar(5000) NULL
);
GO
INSERT dbo.A10_RowOverflow VALUES
    (1, REPLICATE('a',5000), REPLICATE('b',5000)),   -- forces one column off-row
    (2, REPLICATE('c',3000), REPLICATE('d',3000)),   -- fits in row (control case)
    (3, REPLICATE('e',5000), NULL);
GO

/*-------------------------------------------------------------------------------------------------------------------
    A11. LOB data (varchar(max) / nvarchar(max) / varbinary(max)).
        Decode paths: in-row small MAX value vs off-row LOB pointer to LOB_DATA pages (text root / blob record).
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A11_Lob
(
    id   int             NOT NULL CONSTRAINT PK_A11 PRIMARY KEY,
    vmax varchar(max)    NULL,
    nmax nvarchar(max)   NULL,
    bmax varbinary(max)  NULL
);
GO
INSERT dbo.A11_Lob VALUES
    (1, 'small in-row', N'small in-row', 0x00112233),                              -- stays in row
    (2, REPLICATE(CAST('a' AS varchar(max)), 40000),                               -- off-row LOB
        REPLICATE(CAST(N'b' AS nvarchar(max)), 40000),
        CAST(REPLICATE(CAST('c' AS varchar(max)), 40000) AS varbinary(max))),
    (3, NULL, NULL, NULL);
GO

/*-------------------------------------------------------------------------------------------------------------------
    A12. SPARSE columns + COLUMN SET.
        Decode paths: sparse vector record sub-structure (the engine loads it when HasSparseColumns); non-sparse
        anchor columns + the trailing sparse vector in the variable region.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.A12_Sparse
(
    id    int          NOT NULL CONSTRAINT PK_A12 PRIMARY KEY,
    dense varchar(20)  NULL,
    s1    int          SPARSE NULL,
    s2    varchar(30)  SPARSE NULL,
    s3    datetime     SPARSE NULL,
    s4    money        SPARSE NULL,
    cs    xml COLUMN_SET FOR ALL_SPARSE_COLUMNS    -- column_set is metadata only; values still stored as sparse vector
);
GO
INSERT dbo.A12_Sparse (id, dense, s1, s2, s3, s4) VALUES
    (1, 'd1', 100, 'sparse-a', '2026-03-03', 12.34),
    (2, 'd2', NULL, NULL, NULL, NULL),                 -- all sparse null -> minimal/no sparse vector
    (3, NULL, 7, NULL, '2026-04-04', NULL);            -- partial sparse population
GO

/*===================================================================================================================
    SECTION B - CD format (compressed records)

    ROW compression  => every record becomes a CD record (header bit 0 set), integers/decimals/dates stored as
                        minimal significant bytes, char/binary trailing blanks/zeros trimmed.
    PAGE compression => ROW compression + per-page Compression Info: anchor record (column-prefix) and dictionary;
                        columns then reference those via PageSymbol / prefix descriptors.
===================================================================================================================*/

/*-------------------------------------------------------------------------------------------------------------------
    B1. ROW compression - HEAP - all data-type profiles.
        Decode paths: CD header, 1-byte column count, CD array flags (Null / N-ByteShort / Long / BitTrue),
        short-data region, minimal-byte integer/decimal/temporal encoding, trailing-blank trimming.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.B1_Row_Heap_AllTypes
(
    c_bit1     bit, c_bit2 bit, c_bit3 bit,          -- bit packing into CD (BitTrue) / descriptor
    c_tiny     tinyint,
    c_small    smallint,
    c_int      int,
    c_big      bigint,
    c_dec      decimal(19,4),
    c_money    money,
    c_dt       datetime,
    c_date     date,
    c_dt2      datetime2(7),
    c_guid     uniqueidentifier,
    c_char     char(20),        -- under ROW compression fixed char loses trailing blanks -> short var-like
    c_nchar    nchar(10),
    c_binary   binary(16),
    c_varchar  varchar(40),
    c_short    varchar(8)       -- <=8 bytes stays in SHORT region
)
WITH (DATA_COMPRESSION = ROW);
GO
INSERT dbo.B1_Row_Heap_AllTypes VALUES
    (1,0,1, 1, 2, 3, 4, 5.6700, 8.90, '2026-05-05', '2026-05-05', '2026-05-05 01:02:03', NEWID(),
     'abc', N'xy', 0xAABB, 'short', 'tiny'),
    (0,0,0, 0, 0, 0, 0, 0.0000, 0.00, '1900-01-01', '0001-01-01', '0001-01-01', '00000000-0000-0000-0000-000000000000',
     '', N'', 0x, '', ''),                                                            -- all zero -> 0-byte CD encodings
    (1,1,1, 255, -32768, -2147483648, 9223372036854775807, -999999999999999.9999, -922337203685477.5808,
     '9999-12-31 23:59:59.997', '9999-12-31', '9999-12-31 23:59:59.9999999', NEWID(),
     REPLICATE('Z',20), REPLICATE(N'W',10), 0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF, REPLICATE('V',40), 'maxshort');
GO
INSERT dbo.B1_Row_Heap_AllTypes (c_int) VALUES (NULL);   -- mostly-null CD row
GO

/*-------------------------------------------------------------------------------------------------------------------
    B2. ROW compression - CLUSTERED (unique PK).
        Decode paths: CD data records as clustered leaves + clustered index node/root records (still FixedVar at the
        index levels above a compressed leaf; leaf pages carry CD records).
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.B2_Row_Clustered_PK
(
    id   int          NOT NULL CONSTRAINT PK_B2 PRIMARY KEY CLUSTERED,
    a    int          NULL,
    b    varchar(50)  NULL
)
WITH (DATA_COMPRESSION = ROW);
GO
INSERT dbo.B2_Row_Clustered_PK (id, a, b)
SELECT TOP (5000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       (ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) % 100,
       'v' + CAST((ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) % 100 AS varchar(10))
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO

/*-------------------------------------------------------------------------------------------------------------------
    B3. ROW compression - CLUSTERED NON-UNIQUE  -> uniquifier under compression.
        Decode paths: how the 4-byte uniquifier is represented inside a CD record.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.B3_Row_Clustered_Uniquifier
(
    grp  int         NOT NULL,
    note varchar(20) NULL
)
WITH (DATA_COMPRESSION = ROW);
GO
CREATE CLUSTERED INDEX CIX_B3 ON dbo.B3_Row_Clustered_Uniquifier (grp) WITH (DATA_COMPRESSION = ROW);
GO
INSERT dbo.B3_Row_Clustered_Uniquifier (grp, note)
SELECT v.grp, 'd' + CAST(d.n AS varchar(5))
FROM (SELECT TOP (150) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS grp FROM sys.all_objects) v
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) d(n);
GO

/*-------------------------------------------------------------------------------------------------------------------
    B4. ROW compression - NON-CLUSTERED index (compressed index pages -> CD index records).
        Decode paths: CdIndexRecord - column descriptors for index keys, down-page pointer encoded as a 6-byte
        short post-column (SixByteShort), short/long regions in an index context.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.B4_Row_NC_Index
(
    id   int          NOT NULL CONSTRAINT PK_B4 PRIMARY KEY CLUSTERED,
    k1   int          NOT NULL,
    k2   varchar(30)  NOT NULL,
    pad  varchar(50)  NULL
);
GO
INSERT dbo.B4_Row_NC_Index (id, k1, k2, pad)
SELECT TOP (6000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       (ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) % 1000,
       'key_' + CAST((ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) % 1000 AS varchar(10)),
       REPLICATE('p', 30)
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO
CREATE NONCLUSTERED INDEX IX_B4 ON dbo.B4_Row_NC_Index (k1, k2)
    WITH (DATA_COMPRESSION = ROW);          -- compress the index B-tree itself
GO

/*-------------------------------------------------------------------------------------------------------------------
    B5. PAGE compression - HEAP with highly repetitive data.
        Decode paths: Compression Info structure (header flags), ANCHOR RECORD / column prefix, DICTIONARY, and
        per-column PageSymbol descriptors that reference the dictionary. Repetition is what triggers both.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.B5_Page_Heap_Dictionary
(
    id       int          NOT NULL,
    category varchar(40)  NULL,         -- few distinct values -> dictionary entries
    region   char(20)     NULL,         -- shared prefix -> anchor/column-prefix
    amount   decimal(9,2) NULL
)
WITH (DATA_COMPRESSION = PAGE);
GO
INSERT dbo.B5_Page_Heap_Dictionary (id, category, region, amount)
SELECT TOP (20000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       CHOOSE(1 + ABS(CHECKSUM(NEWID())) % 4, 'ELECTRONICS','ELECTRICAL','ELECTRONIC-PARTS','ELECTRO-MECH'),
       'NORTH-AMERICA-REGION',                                   -- identical prefix across the page -> anchor
       CAST((ABS(CHECKSUM(NEWID())) % 50) AS decimal(9,2))
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO

/*-------------------------------------------------------------------------------------------------------------------
    B6. PAGE compression - CLUSTERED.
        Decode paths: page-compressed clustered leaves (CI + CD records) beneath an index B-tree.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.B6_Page_Clustered
(
    id     int          NOT NULL CONSTRAINT PK_B6 PRIMARY KEY CLUSTERED,
    status char(12)     NULL,
    label  varchar(40)  NULL
)
WITH (DATA_COMPRESSION = PAGE);
GO
INSERT dbo.B6_Page_Clustered (id, status, label)
SELECT TOP (20000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       CHOOSE(1 + ABS(CHECKSUM(NEWID())) % 3, 'ACTIVE', 'ACTIVE-HOLD', 'ACTIVE-PEND'),
       'COMMON-PREFIX-' + CAST((ABS(CHECKSUM(NEWID())) % 5) AS varchar(2))
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO

/*-------------------------------------------------------------------------------------------------------------------
    B7. PAGE compression - NON-CLUSTERED index.
        Decode paths: page-compressed index pages (CI + CdIndexRecord with dictionary/anchor in an index AU).
-------------------------------------------------------------------------------------------------------------------*/
CREATE NONCLUSTERED INDEX IX_B7 ON dbo.B6_Page_Clustered (status, label)
    WITH (DATA_COMPRESSION = PAGE);
GO

/*-------------------------------------------------------------------------------------------------------------------
    B8. CD with > 30 columns  -> SHORT/LONG DATA CLUSTER ARRAYS.
        Columns are grouped into clusters of 30; >30 columns forces a non-empty cluster array in both regions.
        Built dynamically. Mix of short (<=8 byte) and long (>8 byte) columns + page compression for dictionary.
-------------------------------------------------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.B8_CD_ManyColumns_ClusterArray') IS NOT NULL DROP TABLE dbo.B8_CD_ManyColumns_ClusterArray;
GO
DECLARE @sql nvarchar(max) = N'CREATE TABLE dbo.B8_CD_ManyColumns_ClusterArray (id int NOT NULL';
DECLARE @i int = 1;
WHILE @i <= 40
BEGIN
    -- alternate short ints (short region) and 40-char strings (long region)
    SET @sql += N', c' + CAST(@i AS nvarchar(5)) +
                CASE WHEN @i % 2 = 0 THEN N' int NULL' ELSE N' varchar(40) NULL' END;
    SET @i += 1;
END
SET @sql += N') WITH (DATA_COMPRESSION = PAGE);';
EXEC sys.sp_executesql @sql;
GO
-- Populate: ints small (minimal-byte short), strings long & repetitive (long region + dictionary)
DECLARE @cols nvarchar(max) = N'id';
DECLARE @vals nvarchar(max) = N'';
DECLARE @i int = 1;
WHILE @i <= 40
BEGIN
    SET @cols += N', c' + CAST(@i AS nvarchar(5));
    SET @vals += CASE WHEN @i % 2 = 0
                      THEN N', ' + CAST(@i AS nvarchar(5))
                      ELSE N', ''LONGVALUE-PREFIX-' + CAST(@i AS nvarchar(5)) + N'''' END;
    SET @i += 1;
END
DECLARE @ins nvarchar(max) =
    N'INSERT dbo.B8_CD_ManyColumns_ClusterArray (' + @cols + N')
      SELECT TOP (8000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL))' + @vals + N'
      FROM sys.all_objects a CROSS JOIN sys.all_objects b;';
EXEC sys.sp_executesql @ins;
GO

/*-------------------------------------------------------------------------------------------------------------------
    B9. CD with > 256 columns  -> 2-BYTE COLUMN COUNT.
        The CD column count uses 1 byte for <=127 and 2 bytes (high bit flag) above that. ~320 columns exercises
        the 2-byte big-endian column-count decode and a 3-element cluster array (320/30).
-------------------------------------------------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.B9_CD_WideColumnCount') IS NOT NULL DROP TABLE dbo.B9_CD_WideColumnCount;
GO
DECLARE @sql nvarchar(max) = N'CREATE TABLE dbo.B9_CD_WideColumnCount (id int NOT NULL';
DECLARE @i int = 1;
WHILE @i <= 320
BEGIN
    SET @sql += N', c' + CAST(@i AS nvarchar(5)) + N' int NULL';
    SET @i += 1;
END
SET @sql += N') WITH (DATA_COMPRESSION = ROW);';
EXEC sys.sp_executesql @sql;
GO
-- One fully-populated row + one row with trailing NULLs (varied CD null flags across 2-byte count)
DECLARE @cols nvarchar(max) = N'id', @vals nvarchar(max) = N'1';
DECLARE @i int = 1;
WHILE @i <= 320
BEGIN
    SET @cols += N', c' + CAST(@i AS nvarchar(5));
    SET @vals += N', ' + CAST(@i % 200 AS nvarchar(5));   -- small values -> minimal-byte short
    SET @i += 1;
END
DECLARE @ins nvarchar(max) =
    N'INSERT dbo.B9_CD_WideColumnCount (' + @cols + N') VALUES (' + @vals + N');';
EXEC sys.sp_executesql @ins;
INSERT dbo.B9_CD_WideColumnCount (id, c1, c160, c320) VALUES (2, 5, 6, 7);   -- mostly-null wide CD row
GO

/*-------------------------------------------------------------------------------------------------------------------
    B10. CD LONG DATA REGION (values > 8 bytes under compression).
        Decode paths: long-data header, long-data offset count/array, long-data cluster array, long fields;
        plus the LOB high-bit flag distinction inside the long offset array.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.B10_CD_LongRegion
(
    id    int          NOT NULL CONSTRAINT PK_B10 PRIMARY KEY,
    short varchar(8)   NULL,        -- short region
    long1 varchar(200) NULL,        -- long region
    long2 nvarchar(200) NULL,       -- long region (wide chars)
    bin   varbinary(200) NULL       -- long region (binary)
)
WITH (DATA_COMPRESSION = ROW);
GO
INSERT dbo.B10_CD_LongRegion VALUES
    (1, 'sh', REPLICATE('L',200), REPLICATE(N'W',200), CAST(REPLICATE('B',200) AS varbinary(200))),
    (2, '', '', N'', 0x),
    (3, 'edge8byt', REPLICATE('x',9), REPLICATE(N'y',5), 0x0102030405060708090A);  -- 9 bytes = just into long
GO

/*-------------------------------------------------------------------------------------------------------------------
    B11. CD + LOB / ROW-OVERFLOW under compression.
        Decode paths: long offset-array LOB flag, row-overflow pointer inside a compressed record (compression only
        applies to in-row data; off-row blobs are referenced from the CD record).
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.B11_CD_Lob
(
    id   int            NOT NULL CONSTRAINT PK_B11 PRIMARY KEY,
    a    varchar(5000)  NULL,       -- can push off-row (row overflow)
    b    varchar(5000)  NULL,
    vmax varbinary(max) NULL        -- LOB
)
WITH (DATA_COMPRESSION = PAGE);
GO
INSERT dbo.B11_CD_Lob VALUES
    (1, REPLICATE('a',5000), REPLICATE('b',5000), CAST(REPLICATE(CAST('z' AS varchar(max)),40000) AS varbinary(max))),
    (2, 'inrow', 'inrow', 0x0011),
    (3, REPLICATE('c',4000), NULL, NULL);
GO

/*-------------------------------------------------------------------------------------------------------------------
    B12. PAGE compression - data engineered for COLUMN PREFIX (anchor record) maximisation.
        Decode paths: anchor record column-prefix values + per-column "prefix length / suffix" descriptors. Every
        column shares a long common prefix so the anchor stores the prefix and rows store only the divergent tail.
-------------------------------------------------------------------------------------------------------------------*/
CREATE TABLE dbo.B12_Page_ColumnPrefix
(
    id   int          NOT NULL,
    col1 varchar(60)  NULL,
    col2 varchar(60)  NULL,
    col3 char(40)     NULL
)
WITH (DATA_COMPRESSION = PAGE);
GO
INSERT dbo.B12_Page_ColumnPrefix (id, col1, col2, col3)
SELECT TOP (20000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       'WWW.INTERNALSVIEWER.EXAMPLE.COM/PATH/' + CAST((ABS(CHECKSUM(NEWID())) % 9) AS varchar(2)),
       'PREFIX-COMMON-STRING-VALUE-SUFFIX-'   + CAST((ABS(CHECKSUM(NEWID())) % 9) AS varchar(2)),
       'STATUS-CODE-'                          + CAST((ABS(CHECKSUM(NEWID())) % 9) AS char(1))
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO

/*-------------------------------------------------------------------------------------------------------------------
    B13. (OPTIONAL / ADVANCED) Row VERSIONING info on the record (CD header versioning bit & FixedVar versioning).
        Versioning bytes are written when a versioning feature is active (RCSI / SNAPSHOT isolation, online rebuild,
        or an AFTER trigger reading inserted/deleted). They persist on the page only while a version is needed, so
        this is inherently non-deterministic for a static capture. To observe it:

            1) ALTER DATABASE InternalsViewerStressTest SET ALLOW_SNAPSHOT_ISOLATION ON;   (already safe to run)
            2) In session #1:  SET TRANSACTION ISOLATION LEVEL SNAPSHOT; BEGIN TRAN; SELECT * FROM dbo.B2_Row_Clustered_PK;
            3) In session #2:  UPDATE dbo.B2_Row_Clustered_PK SET a = a + 1 WHERE id BETWEEN 1 AND 50;
            4) Capture the updated pages NOW (versioning pointer present) before committing session #1.

        Left as instructions so the deterministic part of the script stays reproducible.
-------------------------------------------------------------------------------------------------------------------*/
ALTER DATABASE InternalsViewerStressTest SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

/*===================================================================================================================
    SECTION Z - PAGE LOCATOR
    Lists, for every table/index above, the allocation unit type and the page addresses to open in Internals Viewer.
    Allocation unit types:  IN_ROW_DATA = FixedVar/CD records | ROW_OVERFLOW_DATA = SLOB | LOB_DATA = blobs.
===================================================================================================================*/

PRINT '================ Object / index / allocation-unit map ================';

SELECT  t.name                                AS table_name,
        i.index_id,
        i.name                                AS index_name,
        i.type_desc                           AS index_type,
        p.data_compression_desc               AS compression,
        au.type_desc                          AS alloc_unit_type,
        au.total_pages,
        -- First IAM / data page of the allocation unit (file:page) for quick navigation
        CONVERT(varchar(20),
            CONVERT(int, SUBSTRING(au.first_page, 6, 1) + SUBSTRING(au.first_page, 5, 1))) + N':' +
        CONVERT(varchar(20),
            CONVERT(int, SUBSTRING(au.first_page, 4, 1) + SUBSTRING(au.first_page, 3, 1) +
                         SUBSTRING(au.first_page, 2, 1) + SUBSTRING(au.first_page, 1, 1))) AS first_page_file_page
FROM    sys.tables t
JOIN    sys.indexes i        ON i.object_id = t.object_id
JOIN    sys.partitions p     ON p.object_id = t.object_id AND p.index_id = i.index_id
JOIN    sys.system_internals_allocation_units au ON
            (au.type IN (1,3) AND au.container_id = p.hobt_id) OR     -- IN_ROW_DATA / ROW_OVERFLOW_DATA
            (au.type = 2     AND au.container_id = p.partition_id)    -- LOB_DATA
WHERE   t.name LIKE 'A[0-9]%' OR t.name LIKE 'B[0-9]%'
ORDER BY t.name, i.index_id, au.type;
GO

-- Richer per-page view (page_type, level, compression) for any table - change @table to drill in.
PRINT '================ Per-page detail for a single object (edit @table) ================';
DECLARE @table sysname = N'A5_Clustered_PK';
SELECT  pa.allocated_page_file_id          AS file_id,
        pa.allocated_page_page_id          AS page_id,
        CAST(pa.allocated_page_file_id AS varchar(10)) + ':' +
        CAST(pa.allocated_page_page_id AS varchar(10)) AS file_page,
        pa.page_type_desc,
        pa.index_id,
        pa.page_level,
        pa.is_iam_page,
        pa.is_allocated
FROM    sys.dm_db_database_page_allocations(DB_ID(), OBJECT_ID(@table), NULL, NULL, 'DETAILED') pa
WHERE   pa.is_allocated = 1
ORDER BY pa.index_id, pa.page_level DESC, pa.allocated_page_page_id;
GO

/*  To inspect an individual row's exact page:slot from T-SQL (cross-check against the engine):

        SELECT sys.fn_PhysLocFormatter(%%physloc%%) AS file_page_slot, *
        FROM   dbo.A6_Clustered_NonUnique_Uniquifier;

    And to dump a page's raw record bytes for a manual decode comparison:

        DBCC TRACEON (3604);
        DBCC PAGE ('InternalsViewerStressTest', <file_id>, <page_id>, 3);   -- style 3 = full record detail
*/

PRINT 'Done. Database InternalsViewerStressTest built with FixedVar + CD decode-path coverage.';
GO
