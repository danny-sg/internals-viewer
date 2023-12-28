namespace InternalsViewer.Internals;

internal class SqlCommands
{
    public static readonly string TransactionLog = 
@"SELECT [Current LSN] AS LSN
      ,REPLACE(SUBSTRING(Operation, 5, LEN(Operation)),'_',' ') AS Operation
      ,CASE WHEN Context = 'LCX_NULL' THEN NULL
      ELSE REPLACE(SUBSTRING(Context, 5, LEN(Context)),'_',' ') 
      END AS Context
      ,AllocUnitId
      ,AllocUnitName
      ,[Page ID] AS PageId
      ,[Slot ID] AS SlotId
      ,RowFlags
      ,[Num Elements] AS NumElements
      ,[Offset in Row] AS OffsetInRow
      ,[Rowbits First Bit] AS RowbitsFirstBit
      ,[Rowbits Bit Count] AS RowbitsBitCount
      ,[Rowbits Bit Value] AS RowbitsBitValue
      ,[Byte Offset] AS ByteOffset
      ,[New Value] AS NewValue
      ,[Old Value] AS OldValue
      ,[Description]
      ,[RowLog Contents 0] AS Contents0
      ,[RowLog Contents 1] AS Contents1
      ,[RowLog Contents 2] AS Contents2
      ,[RowLog Contents 3] AS Contents3
      ,[RowLog Contents 4] AS Contents4
      ,CONVERT(BIT,COALESCE(o.is_ms_shipped,1)) AS isSystem
      ,CONVERT(BIT,
        CASE WHEN Context IN ('LCX_GAM','LCX_SGAM', 'LCX_PFS','LCX_DIFF_MAP') 
            THEN 1
            ELSE 0
        END) AS isAllocation
FROM  ::fn_dblog(@begin,null) l
      LEFT JOIN sys.allocation_units au ON l.AllocUnitId = au.allocation_unit_id
      LEFT JOIN sys.partitions p ON p.partition_id = au.container_id
      LEFT JOIN sys.all_objects o ON p.object_id = o.object_id
WHERE Operation NOT LIKE '%XACT%' 
      AND 
      Operation NOT LIKE '%CKPT%'";
}
