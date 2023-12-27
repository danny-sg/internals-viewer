namespace InternalsViewer.Internals;

internal class SqlCommands
{


    public static readonly string Checkpoint = @"CHECKPOINT";

    public static readonly string Compression = 
@"SELECT ISNULL(data_compression, 0) 
FROM   sys.partitions  p
       INNER JOIN sys.allocation_units au ON au.container_id = p.partition_id
WHERE au.allocation_unit_id = @AllocationUnitId";


    public static readonly string IndexColumns = @"-- The reason this is necessary is that the key columns are in sys.system_internals_partition_columns
-- but there doesn''''t seem to be a link to which column they are. 
-- 
-- Let me know at danny@sqlinternalsviewer.com if you can think of a better way!
WITH IndexColumns AS
(
SELECT ipc.partition_column_id
	  ,i.type
      ,i.index_id
      ,p.object_id
      ,CASE WHEN is_uniqueifier = 1 THEN 'Uniqueifier' ELSE ac.name END AS name
      ,ipc.system_type_id
      ,ipc.max_length
      ,ipc.precision
      ,ipc.scale
      ,ipc.leaf_offset
      ,ipc.internal_offset
	  ,ic.index_column_id
	  ,ac.column_id
	  ,ic.is_included_column
	  ,i.is_unique
      ,ipc.internal_null_bit
      ,ipc.is_uniqueifier
from   sys.partitions p
       INNER JOIN sys.allocation_units au ON p.partition_id = au.container_id
       INNER JOIN sys.indexes i ON p.object_id = i.object_id
                                   AND
                                   p.index_id = i.index_id
       INNER JOIN sys.system_internals_partition_columns ipc ON p.partition_id = ipc.partition_id
       LEFT JOIN sys.index_columns ic ON ipc.partition_column_id = CASE WHEN i.index_id = 1 
                                                                        THEN ic.column_id
                                                                        ELSE ic.index_column_id 
                                                                   END
                                          AND
                                          ic.object_id = p.object_id    
                                          AND
                                          ic.index_id = p.index_id
       LEFT JOIN sys.all_columns ac ON ac.object_id = p.object_id 
                                          AND
                                          ac.column_id = ic.column_id
WHERE  au.allocation_unit_id = @AllocationUnitId
)
,KeyColumns AS
(
SELECT ROW_NUMBER() OVER(ORDER BY ipc.key_ordinal) AS RowId
      ,CASE WHEN ipc.is_uniqueifier = 1 THEN 'Uniqueifier'
            WHEN ipc.is_dropped = 1     THEN 'Dropped'
            ELSE ac.name END AS [name] 
      ,p.partition_id
      ,ac.column_id
      ,ipc.is_uniqueifier
      ,ipc.is_dropped
from   sys.partitions p
       INNER JOIN sys.allocation_units au ON p.partition_id = au.container_id
       INNER JOIN sys.indexes i ON p.object_id = i.object_id
                                   AND
                                   p.index_id = i.index_id
       INNER JOIN sys.system_internals_partition_columns ipc ON p.partition_id = ipc.partition_id
       LEFT JOIN  sys.all_columns ac ON ipc.partition_column_id = ac.column_id 
                                        AND
                                        p.object_id = ac.object_id
WHERE  au.allocation_unit_id = @AllocationUnitId
       AND ipc.key_ordinal >= 1
       AND NOT EXISTS (SELECT * FROM IndexColumns ic WHERE ic.name = ac.name)
       AND ipc.is_uniqueifier != 1
)
,MaxIndexColumn AS
(
SELECT MAX(partition_column_id) AS ColumnId FROM IndexColumns WHERE [name] IS NOT NULL
)
SELECT type
      ,ISNULL(ic.name, kc.name) AS name
      ,TYPE_NAME(system_type_id) as type_name
      ,system_type_id
      ,max_length
      ,precision
      ,scale
      ,leaf_offset
      ,internal_offset
      ,index_id
      ,CASE WHEN index_id = 1 THEN internal_offset ELSE leaf_offset END AS offset
      ,ISNULL(ISNULL(index_column_id, kc.RowId + ColumnId),0) AS index_column_id
      ,ISNULL(ISNULL(ic.column_id, kc.column_id),-1) AS column_id
      ,ISNULL(is_included_column,0) AS is_included_column
      ,is_unique
      ,ISNULL(OBJECTPROPERTY(object_id,'TableHasClustIndex'),0) AS hasClusteredIndex
      ,CONVERT(BIT,CASE WHEN kc.is_uniqueifier = 1 OR ic.is_uniqueifier =1 THEN 1 ELSE 0 END) AS is_uniqueifier
      ,ISNULL(is_dropped,0) AS is_dropped
      ,CONVERT(BIT, CASE WHEN ic.name IS NULL THEN 1 ELSE 0 END) AS IsKey
      ,internal_null_bit
FROM   IndexColumns ic
       CROSS JOIN MaxIndexColumn m
       LEFT JOIN KeyColumns kc ON kc.RowId + ColumnId = ic.partition_column_id
WHERE  ISNULL(ic.name, kc.name) IS NOT NULL";

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
