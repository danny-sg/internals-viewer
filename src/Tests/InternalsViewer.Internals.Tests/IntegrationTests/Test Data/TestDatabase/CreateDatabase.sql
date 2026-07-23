USE master;  
GO  
EXEC sp_detach_db @dbname = N'TestDatabase';  
GO  

EXEC sp_attach_db @dbname = N'TestDatabase', @filename1 = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\TestDatabase.mdf'
GO  

CHECKPOINT
DBCC SHRINKFILE (1, 1)
-- Create a test database 

DROP DATABASE IF EXISTS TestDatabase
GO

CREATE DATABASE TestDatabase
GO

USE TestDatabase
GO

CREATE SCHEMA Testing
GO

DROP TABLE IF EXISTS Testing.NumberTable_Clustered 
GO

CREATE TABLE Testing.NumberTable_Clustered
    (
     Id                         INT IDENTITY(1, 1) PRIMARY KEY CLUSTERED
    ,TinyIntNotNullValue        TINYINT            NOT NULL
    ,SmallIntNotNullValue       SMALLINT           NOT NULL
    ,IntNotNullValue            INT                NOT NULL
    ,BigIntNotNullValue         BIGINT             NOT NULL
    ,MoneyNotNullValue          MONEY              NOT NULL
    ,SmallMoneyNotNullValue     SMALLMONEY         NOT NULL
    ,Decimal_5_0_NotNullValue   DECIMAL(5, 0)      NOT NULL  -- 5 bytes
    ,Decimal_5_2_NotNullValue   DECIMAL(5, 2)      NOT NULL  -- 5 bytes
    ,Decimal_9_0_NotNullValue   DECIMAL(9, 0)      NOT NULL  -- 5 bytes
    ,Decimal_9_4_NotNullValue   DECIMAL(9, 4)      NOT NULL  -- 5 bytes
    ,Decimal_18_0_NotNullValue  DECIMAL(18, 0)     NOT NULL  -- 9 bytes
    ,Decimal_18_2_NotNullValue  DECIMAL(18, 2)     NOT NULL  -- 9 bytes
    ,Decimal_28_0_NotNullValue  DECIMAL(28, 0)     NOT NULL  -- 13 bytes
    ,Decimal_28_10_NotNullValue DECIMAL(28, 10)    NOT NULL  -- 13 bytes
    ,Decimal_32_4_NotNullValue  DECIMAL(32, 4)     NOT NULL  -- 17 bytes
    ,Decimal_32_8_NotNullValue  DECIMAL(32, 8)     NOT NULL  -- 17 bytes
    ,Float_10_NotNullValue      FLOAT(10)          NOT NULL  -- 4 bytes
    ,Float_40_NotNullValue      FLOAT(40)          NOT NULL  -- 8 bytes
    )
GO

DECLARE @i INT = 1  

WHILE @i <= 10000
BEGIN
    INSERT INTO Testing.NumberTable_Clustered
    (
        TinyIntNotNullValue       
       ,SmallIntNotNullValue      
       ,IntNotNullValue           
       ,BigIntNotNullValue   
       ,MoneyNotNullValue   
       ,SmallMoneyNotNullValue   
       ,Decimal_5_0_NotNullValue  
       ,Decimal_5_2_NotNullValue  
       ,Decimal_9_0_NotNullValue  
       ,Decimal_9_4_NotNullValue  
       ,Decimal_18_0_NotNullValue 
       ,Decimal_18_2_NotNullValue 
       ,Decimal_32_8_NotNullValue 
       ,Decimal_28_0_NotNullValue 
       ,Decimal_28_10_NotNullValue
       ,Decimal_32_4_NotNullValue 
       ,Float_10_NotNullValue     
       ,Float_40_NotNullValue     
    )
    VALUES
    (
        CONVERT(TINYINT, CRYPT_GEN_RANDOM(1))
       ,CONVERT(SMALLINT, CRYPT_GEN_RANDOM(2))
       ,CONVERT(INT, CRYPT_GEN_RANDOM(4))
       ,CONVERT(BIGINT, CRYPT_GEN_RANDOM(8))
       ,CONVERT(MONEY, CRYPT_GEN_RANDOM(8))
       ,CONVERT(SMALLMONEY, CRYPT_GEN_RANDOM(8))
       ,CONVERT(DECIMAL(5, 0),  (CONVERT(BIGINT, CRYPT_GEN_RANDOM(5)) % POWER(10., 5.)))
       ,CONVERT(DECIMAL(5, 2),  (CONVERT(BIGINT, CRYPT_GEN_RANDOM(5)) % POWER(10., 5.)) / (POWER(10, 2) * 1.))
       ,CONVERT(DECIMAL(9, 0),  (CONVERT(BIGINT, CRYPT_GEN_RANDOM(5)) % POWER(10., 9.)))
       ,CONVERT(DECIMAL(9, 4),  (CONVERT(BIGINT, CRYPT_GEN_RANDOM(5)) % POWER(10., 9.)) / (POWER(10, 4) * 1.))
       ,CONVERT(DECIMAL(18, 0), (CONVERT(BIGINT, CRYPT_GEN_RANDOM(8)) % POWER(10., 18.)))
       ,CONVERT(DECIMAL(18, 2), (CONVERT(BIGINT, CRYPT_GEN_RANDOM(8)) % POWER(10., 18.)) / (POWER(10, 2) * 1.))
       ,CONVERT(DECIMAL(32, 8), (CONVERT(BIGINT, CRYPT_GEN_RANDOM(8)) % POWER(10., 32.)) / (POWER(10, 8) * 1.))
       ,CONVERT(DECIMAL(28, 0), (CONVERT(BIGINT, CRYPT_GEN_RANDOM(8)) % POWER(10., 28.)))
       ,CONVERT(DECIMAL(28, 10), (CONVERT(BIGINT, CRYPT_GEN_RANDOM(8)) % POWER(10., 28.)) / (POWER(10., 10.) * 1.))
       ,CONVERT(DECIMAL(32, 4), (CONVERT(BIGINT, CRYPT_GEN_RANDOM(8)) % POWER(10., 32.)) / (POWER(10, 4) * 1.))
       ,@i * (1./3.)
       ,@i * (1./3.)
    )

    SET @i = @i + 1
END

DROP TABLE IF EXISTS Testing.StringTable_Clustered 
GO

CREATE TABLE Testing.StringTable_Clustered
    (
     Id                             INT IDENTITY(1, 1) PRIMARY KEY CLUSTERED
    ,Char1Value       CHAR(1)       NOT NULL
    ,Char100Value     CHAR(100)     NOT NULL
    ,VarChar100Value  VARCHAR(100)  NOT NULL
    ,VarCharMaxValue  VARCHAR(MAX)  NOT NULL
    ,NChar1Value      NCHAR(1)      NOT NULL
    ,NChar100Value    NCHAR(100)    NOT NULL
    ,NVarChar100Value NVARCHAR(100) NOT NULL
    ,NVarCharMaxValue NVARCHAR(MAX) NOT NULL
    )
GO

INSERT INTO Testing.StringTable_Clustered
SELECT 'A'
      ,REPLICATE('A', 20) + REPLICATE('B', 20) + REPLICATE('C', 20)
      ,REPLICATE('X', 20) + REPLICATE('Y', 20) + REPLICATE('Z', 20)
      ,REPLICATE('D', 8001)
      ,'B'
      ,REPLICATE('A', 20) + REPLICATE('B', 20) + REPLICATE('C', 20)
      ,REPLICATE('X', 20) + REPLICATE('Y', 20) + REPLICATE('Z', 20)
      ,REPLICATE('O', 8001)

DROP TABLE IF EXISTS Testing.DateTable_Clustered 
GO

CREATE TABLE Testing.DateTable_Clustered
    (
     Id                  INT IDENTITY(1, 1) PRIMARY KEY CLUSTERED
    ,DateTimeValue       DATETIME NOT NULL
    ,SmallDateTimeValue  SMALLDATETIME NOT NULL
    ,DateValue           DATE NOT NULL
    ,Time0Value           TIME(0) NOT NULL
    ,Time1Value           TIME(1) NOT NULL
    ,Time2Value           TIME(2) NOT NULL
    ,Time3Value           TIME(3) NOT NULL
    ,Time4Value           TIME(4) NOT NULL
    ,Time5Value           TIME(5) NOT NULL
    ,Time6Value           TIME(6) NOT NULL
    ,Time7Value           TIME(7) NOT NULL
    ,DateTime20Value      DATETIME2(0) NOT NULL
    ,DateTime21Value      DATETIME2(1) NOT NULL
    ,DateTime22Value      DATETIME2(2) NOT NULL
    ,DateTime23Value      DATETIME2(3) NOT NULL
    ,DateTime24Value      DATETIME2(4) NOT NULL
    ,DateTime25Value      DATETIME2(5) NOT NULL
    ,DateTime26Value      DATETIME2(6) NOT NULL
    ,DateTime27Value      DATETIME2(7) NOT NULL
    ,DateTimeOffsetValue DATETIMEOFFSET NOT NULL
    )
GO
INSERT INTO Testing.DateTable_Clustered
VALUES
    (
     GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    ,GETDATE()
    )
GO

CREATE TABLE Testing.SqlVariantTable_Clustered
    (
    VariantValue SQL_VARIANT NOT NULL
   ,DataType     VARCHAR(50) NULL
   ,[Precision]  TINYINT     NULL
   ,[Scale]      TINYINT     NULL
    )
GO

INSERT INTO Testing.SqlVariantTable_Clustered VALUES
    (CONVERT(SQL_VARIANT, CONVERT(DATETIME2(7), GETDATE())),      'DATETIME2', NULL, 7)
   ,(CONVERT(SQL_VARIANT, CONVERT(datetimeoffset(7), GETDATE())), 'datetimeoffset', NULL, 7)
   ,(CONVERT(SQL_VARIANT, CONVERT(datetime, GETDATE())),          'datetime', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(smalldatetime, GETDATE())),     'smalldatetime', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(date, GETDATE())),              'date', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(time, GETDATE())),              'time', NULL, 7)
   ,(CONVERT(SQL_VARIANT, CONVERT(FLOAT, 1000 / 3)),              'FLOAT', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(REAL, 1000 / 3)),               'REAL', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(DECIMAL, 1000 / 3)),            'DECIMAL', 5, 2)
   ,(CONVERT(SQL_VARIANT, CONVERT(MONEY, 1000 / 3)),              'MONEY', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(SMALLMONEY, 1000 / 3)),         'SMALLMONEY', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(BIGINT, 9999)),                 'BIGINT', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(INT, 9999)),                    'INT', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(SMALLINT, 9999)),               'SMALLINT', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(SMALLINT, 99)),                 'TINYINT', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(BIT, 1)),                       'BIT', NULL, NULL)
   ,(CONVERT(SQL_VARIANT, CONVERT(DATETIME2(0), GETDATE())),      'DATETIME2(0)', NULL, 0)
   ,(CONVERT(SQL_VARIANT, CONVERT(DATETIME2(1), GETDATE())),      'DATETIME2(1)', NULL, 1)
GO

CREATE TABLE Testing.BitTable_Clustered
    (
    Description     VARCHAR(50)
   ,BitNotNullValue1 BIT NOT NULL
   ,BitNotNullValue2 BIT NOT NULL
   ,BitNotNullValue3 BIT NOT NULL
   ,BitNotNullValue4 BIT NOT NULL
   ,BitNotNullValue5 BIT NOT NULL
   ,BitNotNullValue6 BIT NOT NULL
   ,BitNotNullValue7 BIT NOT NULL
   ,BitNotNullValue8 BIT NOT NULL
   ,BitNotNullValue9 BIT NOT NULL
   ,BitNullValue1    BIT NOT NULL
   ,BitNullValue2    BIT NOT NULL
   ,BitNullValue3    BIT NOT NULL
   ,BitNullValue4    BIT NOT NULL
   ,BitNullValue5    BIT NOT NULL
   ,BitNullValue6    BIT NOT NULL
   ,BitNullValue7    BIT NOT NULL
   ,BitNullValue8    BIT NOT NULL
   ,BitNullValue9    BIT NOT NULL
    )

    INSERT INTO Testing.BitTable_Clustered VALUES
    ('All True', 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1)
   ,('All False', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Not Null 1', 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Not Null 2', 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Not Null 3', 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Not Null 4', 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Not Null 5', 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Not Null 6', 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Not Null 7', 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Not Null 8', 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Not Null 9', 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Null 1',     0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0)
   ,('Null 2',     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0)
   ,('Null 3',     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0)
   ,('Null 4',     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0)
   ,('Null 5',     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0)
   ,('Null 6',     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0)
   ,('Null 7',     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0)
   ,('Null 8',     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0)
   ,('Null 9',     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)
GO

CREATE TABLE Testing.NonClusteredUniquifier
    (
    IdValue      INT
   ,VarcharValue VARCHAR(100)
    )
GO
CREATE CLUSTERED INDEX clx_Testing_NonClusteredUniqifier ON Testing.NonClusteredUniquifier(IdValue)

CREATE NONCLUSTERED INDEX ix_Testing_NonClusteredUniquifier ON Testing.NonClusteredUniquifier (VarcharValue)

DECLARE @i INT = 0

WHILE @i < 1000
BEGIN
    INSERT INTO Testing.NonClusteredUniquifier VALUES (1, 'Test Value')

    SET @i+=1 
END
GO

CREATE TABLE Testing.DataRecordLoaderTests_FixedLengthOnly
(
    Column1 INT      NOT NULL
   ,Column1 BIGINT   NOT NULL
   ,Column3 CHAR(10) NOT NULL
)

INSERT INTO Testing.DataRecordLoaderTests VALUES (123, 66665555444, 'Test Col 3')
GO

CREATE TABLE Testing.DataRecordLoaderTests_VariableLengthOnly
(
    Column1 VARCHAR(100) NOT NULL
   ,Column2 NVARCHAR(50) NOT NULL
)

INSERT INTO Testing.DataRecordLoaderTests_VariableLengthOnly VALUES ('Value 1', 'Different Value 2')
GO

CREATE TABLE Testing.DataRecordLoaderTests_VariableFixed_NotNull
(
    Column1 INT          NOT NULL
   ,Column2 CHAR(10)     NOT NULL
   ,Column3 VARCHAR(100) NOT NULL
   ,Column4 NVARCHAR(50) NOT NULL
)

INSERT INTO Testing.DataRecordLoaderTests_VariableFixed_NotNull VALUES (1, 'ABC', 'Variable A', 'Variable B')
GO

CREATE TABLE Testing.DataRecordLoaderTests_Lob_RowOverflow
( 
    Column1 INT           NOT NULL
   ,Column2 VARCHAR(8000) NULL 
   ,Column3 VARCHAR(8000) NULL 
   ,Column4 VARCHAR(MAX)  NULL 
);

INSERT INTO Testing.DataRecordLoaderTests_Lob_RowOverflow
VALUES (1, replicate('a',8000), replicate('b',8000), NULL);
INSERT INTO Testing.DataRecordLoaderTests_Lob_RowOverflow
    VALUES (2,replicate('a',8000),NULL, NULL);
INSERT INTO Testing.DataRecordLoaderTests_Lob_RowOverflow
    VALUES (3,replicate('a',8000),NULL, CONVERT(VARCHAR(MAX), replicate('A',8000)) + CONVERT(VARCHAR(MAX), replicate('A',8000)) + CONVERT(VARCHAR(MAX), replicate('A',8000)))
GO

-- Clustered index with fixed length columns
CREATE TABLE Testing.IndexRecordLoaderTests_Clustered_Fixed
(
    KeyColumn1 INT          NOT NULL
   ,KeyColumn2 INT          NOT NULL
   ,Column3    VARCHAR(100) NOT NULL
)
GO

CREATE CLUSTERED INDEX clx_Testing_IndexRecordLoaderTests_Clustered_Fixed 
    ON Testing.IndexRecordLoaderTests_Clustered_Fixed (KeyColumn1, KeyColumn2)
GO

DECLARE @i INT = 1

WHILE @i < 10000
BEGIN
    INSERT INTO Testing.IndexRecordLoaderTests_Clustered_Fixed VALUES (@i, @i, CONCAT('Test Value: ', 100000 - @i, 100000 - @i))

    SET @i+=1
END

-- Clustered index with fixed and variable length columns
CREATE TABLE Testing.IndexRecordLoaderTests_Clustered_Mixed
(
    KeyColumn1 INT          NOT NULL
   ,KeyColumn2 VARCHAR(100) NOT NULL
   ,Column3    VARCHAR(100) NOT NULL
)
GO


CREATE CLUSTERED INDEX clx_Testing_IndexRecordLoaderTests_Clustered_Mixed
    ON Testing.IndexRecordLoaderTests_Clustered_Mixed (KeyColumn1, KeyColumn2)
GO

DECLARE @i INT = 1

WHILE @i < 10000
BEGIN
    INSERT INTO Testing.IndexRecordLoaderTests_Clustered_Mixed VALUES (@i, CONCAT('Test Value: ', 100000 - @i, 100000 - @i), @i)

    SET @i+=1
END

