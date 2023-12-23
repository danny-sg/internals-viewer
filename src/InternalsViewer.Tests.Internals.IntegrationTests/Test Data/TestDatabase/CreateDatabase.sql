-- Create a test database 

DROP DATABASE IF EXISTS TestDatabase
GO

USE TestDatabase
GO

CREATE SCHEMA Testing
GO

CREATE TABLE Testing.Table_Clustered
    (
     Id                  INT IDENTITY(1, 1) PRIMARY KEY CLUSTERED
    ,VarcharNotNullValue VARCHAR(100) NOT NULL
    ,VarcharNullValue    VARCHAR(100) NULL
    ,IntNotNullValue     INT          NULL
    ,IntNullValue        INT          NULL
    )
GO
