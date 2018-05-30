CREATE TABLE [Data].SimpleHeapMixedNullable
(
    RowDescription        VARCHAR(800) NOT NULL
   ,BitNotNullColumn      BIT          NOT NULL
   ,BitNullColumn         BIT          NULL
   ,TinyIntNotNullColumn  TINYINT      NOT NULL
   ,TinyIntNullColumn     TINYINT      NULL
   ,SmallIntNotNullColumn SMALLINT     NOT NULL
   ,SmallIntNullColumn    SMALLINT     NULL
   ,IntNotNullColumn      INT          NOT NULL
   ,IntNullColumn         INT          NULL
   ,BigIntNotNullColumn   BIGINT       NOT NULL
   ,BigIntNullColumn      BIGINT       NULL
   ,VarcharNotNullColumn  VARCHAR(100) NOT NULL
   ,VarcharNullColumn     VARCHAR(100) NULL
   ,CharNotNullColumn     CHAR(100)    NOT NULL
   ,CharNullColumn        CHAR(100)    NULL
   ,DateTimeNotNullColumn DATETIME     NOT NULL
   ,DateTimeNullColumn    DATETIME     NULL
   ,DateNotNullColumn     DATE         NOT NULL
   ,DateNullColumn        DATE         NULL
)
GO