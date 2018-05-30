CREATE TABLE [Data].SimpleHeapNotNullable
(
    RowDescription        VARCHAR(800) NOT NULL
   ,BitNotNullColumn      BIT          NOT NULL
   ,TinyIntNotNullColumn  TINYINT      NOT NULL
   ,SmallIntNotNullColumn SMALLINT     NOT NULL
   ,IntNotNullColumn      INT          NOT NULL
   ,BigIntNotNullColumn   BIGINT       NOT NULL
   ,VarcharNotNullColumn  VARCHAR(100) NOT NULL
   ,CharNotNullColumn     CHAR(100)    NOT NULL
   ,DateTimeNotNullColumn DATETIME     NOT NULL
   ,DateNotNullColumn     DATE         NOT NULL
)
GO