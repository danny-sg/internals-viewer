namespace InternalsViewer.Internals;

internal class SqlCommands
{
    public static readonly string AllocationUnitName =
@"SELECT CONCAT(s.name, '.', o.name) AS Alloc_Unit
  FROM   sys.allocation_units au
         INNER JOIN sys.partitions p ON au.container_id = p.partition_id
         INNER JOIN sys.objects o ON p.object_id = o.object_id
         INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
  WHERE  au.allocation_unit_id = @AllocationUnitId";

    public static readonly string AllocationUnits =
@"SELECT o.object_id
	  ,iau.first_iam_page
      ,s.name AS schema_name
	  ,o.name AS table_name
      ,i.name AS index_name
	  ,is_ms_shipped AS is_system
      ,p.index_id
      ,i.type AS index_type
      ,iau.type AS allocation_unit_type
	  ,CASE iau.type
			WHEN 1 THEN 'Row Data'
			WHEN 2 THEN 'LOB Data'
			WHEN 3 THEN 'Row Overflow Data'
	   END AS type_description
      ,iau.used_pages
      ,iau.total_pages
FROM   sys.all_objects o
	   INNER JOIN sys.schemas s ON o.schema_id = s.schema_id 
	   INNER JOIN sys.partitions p ON p.object_id = o.object_id
       INNER JOIN sys.indexes i ON i.object_id = o.object_id AND i.index_id = p.index_id
	   INNER JOIN sys.system_internals_allocation_units iau ON iau.container_id = p.partition_id
ORDER BY is_ms_shipped desc, s.name asc , o.name asc";

    public static readonly string BufferPool =
@"SELECT file_id
      ,page_id
      ,is_modified
      ,row_count
      ,free_space_in_bytes
FROM   sys.dm_os_buffer_descriptors WITH (NOLOCK)
WHERE  database_id = DB_ID(@database)";

    public static readonly string Checkpoint = @"CHECKPOINT";

    public static readonly string CompatibilityLevel = @"SELECT compatibility_level FROM sys.databases WHERE name = @Name";

    public static readonly string Compression = @"SELECT ISNULL(data_compression, 0) FROM sys.partitions WHERE partition_id = @PartitionId";

    public static readonly string Database = @"SELECT name FROM sys.databases WHERE database_id = @DatabaseId";

    public static readonly string DatabaseTables =
@"SELECT o.object_id
	  ,s.name AS schema_name
	  ,o.name AS table_name
	  ,is_ms_shipped AS system 
FROM   sys.objects o 
	   INNER JOIN sys.schemas s ON o.schema_id = s.schema_id 
WHERE  type IN ('U','S')
ORDER BY is_ms_shipped desc, s.name asc , o.name asc";

    public static readonly string DatabaseId = @"SELECT DB_ID(@DatabaseName)";

    public static readonly string Databases = 
@"SELECT   d.database_id
	    ,d.name
        ,SUM(size) AS Size
        ,d.state 
	    ,COUNT(*) AS DataFiles
       ,d.compatibility_level
FROM     sys.databases d  
	     INNER JOIN sys.master_files mf ON d.database_id = mf.database_id 
WHERE    type = 0
GROUP BY d.database_id
		,d.name
		,d.state 
        ,compatibility_level   
ORDER BY d.name";

    public static readonly string EntryPoints = 
@"SELECT DISTINCT first_page   
      ,root_page
      ,first_iam_page
      ,partition_number
FROM   sys.system_internals_allocation_units iau
       INNER JOIN sys.partitions p ON iau.container_id = p.partition_id
       INNER JOIN sys.indexes    i ON p.object_id = i.object_id
                                      AND
                                      i.index_id   = p.index_id
WHERE p.object_id = OBJECT_ID(@ObjectName) AND ISNULL(i.name, '') = @IndexName AND iau.type = 1
ORDER BY partition_number";

    public static readonly string FileSize = @"SELECT size FROM sys.database_files WHERE file_id = @FileId";

    public static readonly string Files = 
@"CREATE TABLE #FileStats
            (file_id int
            ,filegroup int  
            ,total_extents int
            ,used_extents int 
            ,name sysname
            ,file_name nchar(520))
INSERT #FileStats EXEC ('dbcc showfilestats')
		
SELECT df.file_id
      ,df.type
      ,fg.name as filegroup_name
      ,df.name
      ,physical_name
      ,size
      ,total_extents
      ,used_extents
FROM   sys.database_files df
       INNER JOIN sys.filegroups fg ON df.data_space_id = fg.data_space_id
       INNER JOIN #FileStats fs ON df.file_id = fs.file_id
WHERE  df.type = 0

DROP TABLE #FileStats";

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

    public static readonly string ObjectHasClusteredIndex = @"SELECT ISNULL(OBJECTPROPERTY(OBJECT_ID(@TableName), 'TableHasClustIndex'), 0)";

    public static readonly string Page = @"DBCC PAGE({0}, {1}, {2}, {3}) WITH TABLERESULTS";

    public static readonly string SysAdminCheck = @"SELECT CONVERT(BIT, IS_SRVROLEMEMBER('sysadmin'))";

    public static readonly string TableColumns = 
@"SELECT CASE WHEN is_uniqueifier = 1 THEN 'Uniqueifier'
            WHEN is_dropped     = 1 THEN '(Dropped)'
            ELSE c.name END AS name,
       ISNULL(c.column_id,-1) AS column_id,
       TYPE_NAME(pc.system_type_id) as type_name,
       pc.system_type_id,
       pc.max_length,
       pc.precision,
       pc.scale,
       pc.leaf_offset,
       pc.is_uniqueifier,
       pc.is_dropped,
       ISNULL(c.is_sparse, 0) AS is_sparse,
       pc.leaf_null_bit
FROM   sys.allocation_units au
       INNER JOIN sys.partitions p ON au.container_id = p.partition_id
       INNER JOIN sys.system_internals_partition_columns pc ON p.partition_id = pc.partition_id
       INNER JOIN sys.all_objects o ON p.object_id = o.object_id
       LEFT JOIN sys.all_columns c ON column_id = partition_column_id AND c.object_id = p.object_id
WHERE  au.allocation_unit_id  = @AllocationUnitId";

    public static readonly string TableInfo = @"SELECT o.object_id
      ,s.name AS schema_name
      ,CASE WHEN i.name IS NULL 
            THEN o.name
            ELSE o.name + '.' + i.name 
       END AS object_name
      ,o.type AS object_type
      ,i.type_desc
      ,i.type AS index_type
      ,i.index_id AS index_id
      ,iau.first_iam_page
      ,iau.root_page
      ,iau.first_page
      ,iau.total_pages
      ,iau.used_pages
      ,iau.data_pages
      ,iau.type AS alloc_unit_type
      ,iau.type_desc
      ,partition_number
      ,COUNT(*) OVER (PARTITION BY i.index_id) AS partition_count
FROM   sys.system_internals_allocation_units iau
       INNER JOIN sys.partitions p ON iau.container_id = p.partition_id
       INNER JOIN sys.objects o ON p.object_id = o.object_id
       INNER JOIN sys.indexes i ON p.index_id = i.index_id AND i.object_id = p.object_id
       INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE  p.object_id = @ObjectId
ORDER BY index_id, iau.type ASC";

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

    public static readonly string Version = @"SELECT SERVERPROPERTY('productversion'), size FROM sysfiles WHERE fileid = 1";



}
