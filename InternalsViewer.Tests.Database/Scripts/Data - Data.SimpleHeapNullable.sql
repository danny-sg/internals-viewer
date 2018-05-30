INSERT INTO Data.SimpleHeapNullable VALUES
   ('Bit column populated',      1,    NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
  ,('Tinyint column populated',  NULL, 100, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
  ,('Smallint column populated', NULL, NULL, 9999, NULL, NULL, NULL, NULL, NULL, NULL)
  ,('Int column populated',      NULL, NULL, NULL, 888888, NULL, NULL, NULL, NULL, NULL)
  ,('BigInt column populated',   NULL, NULL, NULL, NULL, 55555555, NULL, NULL, NULL, NULL)
  ,('Varchar column populated',  NULL, NULL, NULL, NULL, NULL, 'Variable Length Value!', NULL, NULL, NULL)
  ,('Char column populated',     NULL, NULL, NULL, NULL, NULL, NULL, 'Fixed length value!', NULL, NULL)
  ,('DateTime column populated', NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2018-01-01 10:10', NULL)
  ,('Date column populated',     NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2010-12-12')
  ,('All populated',             1,    22,   333,  4444, 55555, '666666', '7777777', '2010-12-12', '2010-12-12')
  ,(NULL,                        NULL,    NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL) -- None populated
GO