# Event types in events.jsonl

> Commands in this guide are written in the export skill's form, for example `calltree --operator 3`. Over MCP each is a tool of the
> same name and the options are its parameters, so that call is `query_calltree` with `operator` set to 3.

Each line is one event, `$type` naming its class. Fields shared by every event are described in
[format.md](format.md#eventsjsonl). This file lists the fields each type adds and what the event means. Enum values
are written as their names. Reference fields hold ids: event ids, log record ids or call tree node ids.

## Page reads

### ReadEventGroup (Name "Page Read") - one read, as the app lists it

| Field | Meaning |
| --- | --- |
| `ReadType` | `NonCached` = read from disk. `Cached` = the page was already in the buffer pool |
| `Pages` | Every page the read covered, in page order. A scatter/gather read pulls a range, usually one extent of 8 pages |
| `PageAddress` | The first page, as a representative |
| `Events` | The child events folded into the read: latches, file reads, `physical_page_read`, waits |
| `PoolLookup` | For a columnstore read, the object pool miss it served (an `ObjectPoolEvent` id) |
| `IsReadAhead` | The read was issued by read-ahead rather than by the operator needing the page |
| `IsAllocationPage` | An IAM, PFS, GAM or other allocation page: the engine finding its way, not the query's data |
| `DurationUs` | The measured read time, taken from the latch suspend for a disk read |

A disk read is a `BUF` latch suspend while the file is read, a `file_read` and `file_read_completed`, and one
`physical_page_read` per page. A buffer pool read is a `BUF SH` latch acquired on the page. A read is attributed to a
plan node through `PlanNodeIdentifier` when the operator could be worked out.

### IoEvent (Name `physical_page_read` / write) - one page physically read or written

`IsRead`, `IsRoot` (the page is the index root). Folded into a `ReadEventGroup`.

### FileEvent (Name `file_read`, `file_read_completed`, writes likewise) - one file I/O

`IsRead`, `Mode` (`Contiguous` single page, `ScatterGather` several pages in one I/O), `FileId`, `Offset` and
`Size` in bytes. `Offset / 8192` is the first page. Folded into a `ReadEventGroup`.

## Waits, locks, latches

### WaitEvent (Name "Wait")

| Field | Meaning |
| --- | --- |
| `WaitType` | The SQL Server wait type: `PAGEIOLATCH_SH`, `SOS_SCHEDULER_YIELD`, `CXPACKET`, `LCK_M_S`, etc. |
| `WaitResource` | What it waited on, for a page I/O wait the address of the BUF latch |
| `PageAddress` | For a page I/O wait, the page, taken from the latch suspend that measured it |
| `IsEnd` | The event is the end of the wait (the one carrying the duration) |
| `Category` | `Io`, `Cpu`, `Concurrency` or `Parallelism` |

In a Full Trace run only the parallelism waits (`CXPACKET`, `CXCONSUMER`, `CXSYNC_PORT`, `CXSYNC_CONSUMER`) were
captured, to find the worker threads, and `SOS_SCHEDULER_YIELD` is the recorded query yielding its scheduler.

### LockEvent (Name "Lock" for the acquire, `lock_released` for the release)

| Field | Meaning |
| --- | --- |
| `LockMode` | `S`, `X`, `U`, `IS`, `IX`, `SIX`, `Sch_S`, `Sch_M`, range modes such as `RangeS_S`, etc. |
| `Resource` | `ResourceType` (`Database`, `Object`, `Page`, `Key`, `Extent`, `Rid`, `Hobt`, `AllocationUnit`, `Rowgroup`, `Metadata`, `Xact`), `Key` (the internal lock hash), `PageAddress`, `RowIdentifier`, `KeyHash` (the `%%lockres%%` text for a key lock), `ObjectId`, `HobtId`, `LockPartition` |
| `LockOwnerContext` | `OwnerType` (`Transaction`, `Cursor`, `Session`, workspaces), `TransactionId`, `SessionId`, `WorkspaceId`, `SubId`, `NestId` |
| `PartitionCount` | More than 1 when an object lock taken on every lock partition was collapsed into one event |
| `DurationUs` | On the acquire, how long the lock was held when the release could be matched |

### LockGroup (Name "Object Locks" or "Object Schema Locks") - the locks one transaction took on one object

`Events` lists the member lock events, coarsest first. The description tallies them by resource type
("1 Object, 223 Page") and shows the shape of an escalation. `DurationUs` spans the whole group.

### LockEscalationEvent

`LockMode`, `ResourceType`, `EscalatedObjectId`, `TransactionId`, `EscalatedLockCount` (the finer locks the
escalation dropped), `HobtLockCount`. The point at which the dropped key and page locks ended.

### LatchEvent (Name `latch_acquired` / `latch_released`, suspends `latch_suspend_begin` / `latch_suspend_end`)

`LatchClass` (`BUF` is a page buffer latch; others name engine structures), `LatchMode` (`KP` keep, `SH` shared,
`UP` update, `EX` exclusive, `DT` destroy), `LatchAddress`, `PageAddress` for a `BUF` latch. An acquire and release
on the same page and address are one hold. A `BUF EX` latch on a read is the page being filled from disk, `BUF SH`
the page being read, `BUF KP` the buffer being pinned without reading it.

## Query and operators

### ExecutionOperatorEvent - one plan operator (Name is the operator, e.g. "Clustered Index Seek")

| Field | Meaning |
| --- | --- |
| `PlanNodeIdentifier` | `{PlanHandleId, NodeId}`. `NodeId` -1 is the statement root (`SELECT`, `INSERT`, etc.) |
| `ParentNodeId`, `NodeLevel` | Position in the plan tree |
| `OperatorDescription`, `LogicalOperator` | Physical and logical operator names from the plan |
| `OperatorSchemaName`, `OperatorTableName`, `OperatorIndexName` | The table and index the operator reads or writes |
| `Category` | `DataAccess`, `Join`, `Transformation`, `Buffer`, `Modification`, `Adapter` |
| `TimeUs`, `DurationUs` | When the operator started and how long it ran, from the per-thread profile |
| `EmitStartUs` | When rows first flowed out. Later than the start for a blocking operator (sort, hash build) |
| `BuildPhaseTimeUs`, `BuildPhaseDurationUs`, `ProbePhaseTimeUs`, `ProbePhaseDurationUs` | Hash join or hash aggregate phases |
| `RowsProcessed` | Rows across all threads |
| `Threads` | One `{ThreadId, StartUs, DurationUs, RowsProcessed}` per `query_thread_profile` thread. Thread 0 is the coordinator, 1..n the workers, so the worker count is the degree of parallelism |
| `Cost` | Estimated subtree cost |
| `EntryFrames` | Call tree node ids where this operator's execution begins. Several under parallelism or when an iterator is entered from more than one method. Empty when the operator was inlined or could not be matched |
| `ExitFrames` | Call tree node ids where a child operator begins, ending this operator's segment |
| `Instances` | Full Trace: the iterator object addresses (`this`) that ran this operator |
| `Memory` | Full Trace, when present: `Allocated`, `Allocations`, `Freed`, `PeakInUse` bytes and `Uses` (`Caller`, `Allocator`, `Bytes`, `Allocations`) attributed to this operator |

### QueryThreadEvent (Name `query_thread_profile`)

The raw per-thread, per-node profile event the operator events were built from. `NodeId`. Hidden in the app.

### BatchStartEvent, BatchEndEvent (`sql_batch_starting`, `sql_batch_completed`)

`SqlText`. The boundaries of the batch. Hidden in the app.

### TransactionEvent (`sql_transaction`)

`TransactionId`, `State` (`Begin`, `Commit`, `Rollback`). The commit or rollback is where locks held to the end of
the transaction were released. Hidden in the app.

### MemoryEvent

Memory grant feedback: `UsedMemoryKb`, `GrantedMemoryKb`, `AdditionalMemoryBeforeKb`, `AdditionalMemoryAfterKb`.

## Modifications

### TransactionLogEvent (Name "Transaction Log Record")

`Operation` (`LOP_INSERT_ROWS`, `LOP_MODIFY_ROW`, `LOP_FORMAT_PAGE`, etc.), `Context` (`LCX_CLUSTERED`, `LCX_HEAP`,
`LCX_PFS`, etc.), `AllocationUnitId`, `TransactionId`, `LogRecordSize`, `LogRecord` (a log record id in
`log-records.jsonl`), `PageAddress` and `RowIdentifier` from the record.

### PageSplitEvent

`SplitOperation` (a b-tree split or `SPLIT_FOR_NEW_PAGE`, a new page allocation), `NewPage` (the page the split
added), `RowsetId`, `LogRecords` (the records of the split and of the system transaction that formatted the new
page, in LSN order).

## Columnstore and batch mode

### RowGroupScanEvent (Name "Rowgroup Scan") - one rowgroup's scan, grouping its segment scans and pool lookups

`RowGroupId`, `Events` (the member events). `DurationUs` spans the rowgroup.

### SegmentScanEvent (Name "Segment Scan") - one column segment read in a rowgroup

| Field | Meaning |
| --- | --- |
| `IsScanStart` | True on the start event; the finished event (folded, `FoldedFrom` the start) carries the results |
| `NodeId`, `RowGroupId`, `ColumnId` | Which operator, rowgroup and column |
| `EncodingType` | `ValueFast`, `HashPrimary32Secondary64`, `StringHash`, `StringValue`, `None`, etc. How the segment is encoded |
| `CompressedDataType`, `SqlDataType`, `BitPacking`, `BaseId`, `Magnitude`, `NullValue`, `MinDataId`, `MaxDataId` | The segment's value encoding: bits per value, base and multiplier, data id range |
| `PrimaryDictionaryValueCount`, `SecondaryDictionaryValueCount`, `SecondaryBaseId` | Dictionary sizes for dictionary encodings |
| `FilterType`, `FilterOnCompressedDataType`, `IsFilterOnCompressedDataUsed` | The predicate applied while scanning, and whether it ran on the compressed (encoded) values before decoding |
| `CpuInstructionSet` | `AVX2`, `AVX512`, `SSE42`: the SIMD tier used |
| `IsDeepDataPossible`, `IsNullable` | Segment properties |
| `InputRows`, `OutputRows` | Rows into and out of the segment's filter |
| `PureRowBuckets`, `ImpureRowBuckets` | Buckets of rows where the filter result was known for the whole bucket (pure) against buckets needing row by row work (impure) |

### SegmentEliminateEvent (Name "Segment Eliminated")

`RowGroupId`, `HobtId`, `IsEliminatedByUniqueValueFilter` (false means eliminated from segment min/max metadata).
The rowgroup was skipped without being read.

### ObjectPoolEvent (Name "Object Pool Hit" / "Object Pool Miss")

`IsHit`, `ObjectType` (`ColumnSegment`, `PrimaryDictionary`, `SecondaryDictionary`, `DeleteBitmap`, etc.),
`RowGroupId`, `HobtId`, `ColumnId`, `PoolObjectId`, `Pages` (the pages the object lives on). A miss means the segment
or dictionary was read from disk; a hit means it was already decompressed in the columnstore object pool.

### ColumnStoreScanEvent - other columnstore scan events

`EventName` (the raw event: `column_store_rowgroup_read_issued`, `column_store_rowgroup_readahead_issued`,
`query_execution_column_store_rowgroup_scan_finished`, `column_store_expression_filter_bitmap_set`,
`column_store_rowgroup_skip_delete_buffer`, `query_execution_wait_syncpoint`), `RowGroupId`, `HobtId`, `Summary`.

### ColumnstoreFilterEvent (Name "Batch Filter" / "Bitmap Filter")

`EventName` (`query_execution_batch_filter` or `column_store_expression_filter_apply`), `NodeId`, `RowGroupId`,
`ColumnId`, `InputRows`, `OutputRows`, `IsPure`, `IsPrefiltered`.

### BatchModeEvent - batch mode aggregation and pushdown

`EventName` (`query_execution_batch_hash_aggregation_finished`, `query_execution_batch_global_string_dictionary`,
`query_execution_push_down_aggregate`, `query_execution_dynamic_push_down_statistics`), `NodeId`,
`IsFastComparisonUsed`, `IsLocalAggregationUsed`, `IsGlobalDictionaryUsed`, `GlobalDictionaryKeyColumns`,
`AggregationCount`, `GroupByCount`, `RowGroupId`, `RowsPushedDown`, `RowsNotPushedEncoding`,
`RowsNotPushedOverflow`, `RowsNotPushedDisabled`.
