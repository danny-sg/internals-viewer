# Reading SQL Server function names

> Commands in this guide are written in the export skill's form, for example `calltree --operator 3`. Over MCP each is a tool of the
> same name and the options are its parameters, so that call is `query_calltree` with `operator` set to 3.

Call tree nodes and call log entries name functions as `module!Class::Method`. SQL Server ships public symbols,
so the names are real, but no documentation of what they do. What follows is how the names are built and what
Internals Viewer has learned from reading them. Anything marked *inferred* is a reading of the name or of the
code's behaviour, not a published fact. Say so when you lean on it.

## Modules

| Module | Layer |
| --- | --- |
| `sqlmin` | The storage engine and most of query execution: scans, seeks, b-trees, pages, the buffer pool, locks, latches, batch mode, columnstore |
| `sqllang` | The language layer: batches, statements, the TDS connection, compilation and optimisation orchestration (`CXStmtQuery`, `CMsqlExecContext`, `process_request`) |
| `sqldk` | SQLOS: schedulers, tasks and workers (`SOS_Scheduler`, `SOS_Task`, `Worker`), memory objects (`CMemObj`, `operator new`), synchronisation. Not recorded by Full Trace, so only the calls into it appear |
| `SqlTsEs` | Expression services: scalar expression evaluation (`CEsExec`), type conversion |
| `qds` | Query Store |
| `KERNEL32`, `KERNELBASE`, `ntdll`, `ucrtbase` | Windows and the C runtime |

## Categories the app uses

Internals Viewer classifies every frame into one of these categories with the rules in `mappings/` (copies of the
pipe-delimited files the app embeds, taken from the Internals Viewer repository, `src/Query/InternalsViewer.Query/Mappings`, on 2026-10-27). `query_categories` totals calls per category and `query_functions`
labels each function with its category. The categories, and the kind of class in each:

| Category | What it covers | Typical classes |
| --- | --- | --- |
| Statement Execution | Batch and statement execution | `CXStmtSelect::XretExecute`, `CXStmtQuery::ErsqExecuteQuery` |
| Query Execution | The execution context and the execution tree | `CQueryScan`, `CXte*` (plan nodes), `CMsqlExecContext` |
| Query Operator | Row mode iterators | `CQScan*` (`CQScanRangeNew` seek, `CQScanTableScanNew` scan, `CQScanHash` hash match, `CQScanSort`, `CQScanNLJoinNew` nested loops, `CQScanStreamAggregateNew`, `CQScanTopNew`) |
| Batch Operator | Batch mode iterators and their structures | `CBpQScan*`, `CBpag*` (aggregates), `CBpBatch`, `CBpSpillProcessor` |
| Rowset | The rowset interface operators pull rows through, and record decoding | `RowsetNewSS`, `IndexDataSetSession`, `HeapDataSetSession`, `DatasetSession`, `FixedVarRecord`, `CValRow` |
| B-Tree Navigation | Walking an index from root to leaf | `BTreeMgr::Seek`, `BTreeMgr::HandleRoot`, `IndexPageManager::GetPageWithKey`, `IndexRowScanner`, `BTreeRow` |
| Page Access | Fixing and reading pages | `PageRef::Fix`, `IndexPageManager::GetNextPage`, `PageContext`, `HPageHandle` |
| Buffer Pool | The buffer cache | `BPool::Get`, `BPool::ReadPageCompletion`, `BUF`, `BufferPageIdOffset` reads |
| Latching | Page and structure latches | `LatchBase::AcquireInternal`, `LatchBase::Release`, `BUF::AcquireLatch` |
| Locking | Lock manager | `lck_lockInternal`, `LockOwner`, `GetHoBtLockInternal`, `XactLockInfo` |
| Allocations | Allocation maps and page allocation | `IAM_BmapScan`, `AllocScanPosition`, `PFS`, `GAM` |
| Storage | File control blocks and I/O issue | `FCB::*`, `FCB::PostRead`, `FCB::IoCompletion`, workfiles |
| I/O Infrastructure | Asynchronous I/O completion | `IOQueue::CheckForIOCompletion`, `JoinedIoCompletion` |
| Memory | Memory objects and allocators | `CMemObj::Alloc`, `operator new`, `CBpMalloc`, memory grants (`CQryMemManager`) |
| SQL OS, SQL OS Scheduling, SQL OS Synchronisation | Schedulers, tasks, yields, spinlocks, mutexes | `SOS_Scheduler::SwitchContext`, `SOS_Task`, `YieldAndCheckForAbort`, `SpinlockBase` |
| Expression Evaluation | Scalar expressions and predicates | `CEsExec::GeneralEval`, `CEsRuntime::Eval`, `CValEsConstXVar` |
| Columnstore | Columnstore segments, dictionaries, the object pool | `CSIScanBucketColumn`, `ColumnStoreObjectRef::Fix`, `Rbp*` (row bucket processing) |
| Transaction Management | Transactions, versioning | `XDES`, `CMsqlXact`, `RecoveryUnit` |
| Logging | Writing the transaction log | `WritePageRecorder`, `XdesLogRecord`, `LogMgr` |
| LOB Storage | Large object storage | `BlobNode`, `*LockBytes*`, `CLBSS*` |
| Metadata | Metadata access | `CMEDCat*`, `CMEDScan`, `CMEDRcs*` |
| Compilation, Optimization, Query Binding | Building the plan | `CCompPlan`, `COptContext`, `CAlgTableMetadata` |
| Networking | TDS and the client connection | `CNetConnection`, `CTdsConn`, `CPhysicalConnection` |
| Security | Permission checks, encryption | `CMEDRcsSecurity`, Always Encrypted checks |
| Extended Events, Tracing | Event publishing and profiling. Cut out of a Full Trace tree, except the operator profile wrappers (`CQScanProfileNew`, `CQScanLightProfileNew`) which keep their callees |
| System | Windows, the C runtime, `memcpy`, `__security_check_cookie` |

Categories marked infrastructure in the app (Extended Events, Tracing, SQLOS Scheduling and Synchronisation, I/O
Infrastructure, System) are hidden by default there. When counting "the query's work", leave them out too.

## Reading a name

- `C` starts almost every class (`CQueryScan`, `CBpBatch`); `I` an interface (`IBpScanner`); `T` a template.
- **`CQScan…` are row mode operators**, pulled a row at a time through `GetRow` or `GetRowHelper`.
  **`CBpQScan…` are the batch mode counterparts** (`Bp` = batch processing, *inferred*), pulled a batch at a time
  through `BpGetNextBatch`. `CQScanBatchHelper` is the boundary where batch mode becomes row mode.
- `CXte…` classes are the plan's own nodes (the execution tree); the iterator for a node is built from them.
- `CQScanRangeNew` is the Index Seek, the Clustered Index Seek and the Key Lookup. `CQScanTableScanNew` is a table or
  index scan. `CQScanNew` is the base. `…New` means a newer implementation that lives beside an older one.
- `RowsetNewSS::FetchNextRow` and `GetNextRows` are the rowset interface an operator reads through.
  `IndexDataSetSession` and `HeapDataSetSession` are the index and heap sessions behind it, and
  `GetNextRowValuesInternal` is a row being fetched. `IndexRowScanner` and `IndexPageManager` move along the leaf
  level, `BTreeMgr::Seek` descends from the root.
- `BPool::Get` fetches a page from the buffer pool, reading it from disk if it is not there. The page id is passed
  by pointer, which is why the page appears as a locator on the node. `PageRef::Fix` pins a page for use.
- `HoBtAccess::Init` opens access to a heap or b-tree (a HoBt); its id argument is the partition id. `hobt_id >> 16`
  is the id as the engine passes it internally.
- `Rbp…` classes (`RbpDecode`, `RbpFilter`, `RbpEarlyFilter`, `RbpBitUnpack`, `RbpAggregate`) are the columnstore
  scan's per-column pipeline (`Rbp` = row bucket processing, *inferred*). `RbpEarlyFilter` filters in the compressed
  domain; `RbpFilter` after decoding. The leading template argument is usually the enum that chose the variant.
- `Compile…` in these names means selecting a function pointer for a case, not generating code.
- `F…` methods return a bool (`FGetRow`, `FIsNull`); `Pv…`/`Pc…` return pointers; `Ersq…`, `Xret…` return a status.
- `SIMD`/`NoSIMD`, `SSE42`/`AVX2`/`AVX512`, `_2B/_4B/_8B` name vectorised variants, instruction set tiers and element
  widths.
- `XeSqlPkg::<event>::Publish` is Extended Events publishing; `CQScanProfileNew::GetRow` is the actual-plan profiling
  wrapper around each operator's `GetRow` (two `QueryPerformanceCounter` calls per row, which is why
  `KERNEL32!QueryPerformanceCounterStub` is often the most called function in a Full Trace).
- A constructor is a method named after its class (`CQScanSort::CQScanSort`). On a stack it means the plan was being
  built, not run.
- `memcpy`, `memset`, `__security_check_cookie` are the C runtime moving data and checking stack cookies, and their
  counts say nothing about the query beyond volume.

## Reading a path

Innermost first, a stack during a seek fetching a row reads: `BPool::Get` < `IndexPageManager::GetNextPage` <
`IndexRowScanner::MoveToRowOnNextPage` < `IndexDataSetSession::GetNextRowValuesInternal` < `RowsetNewSS::FetchNextRow`
< `CQScanRowsetNew::GetRowWithPrefetch` < `CQScanNew::GetRowOrReQualifyHelper` < `CQScanProfileNew::GetRow` <
`CQueryScan::GetRow` < `CXStmtQuery::ErsqExecuteQuery` < `process_request`. Read from the right: the request runs the
statement, which pulls rows from the plan's root, which pulls them from the seek, which asks the rowset for the next
row, which moves the row scanner to the next page, which fetches it from the buffer pool.

Two more legs complete that picture:

- **The first row** descends the b-tree instead of moving to the next page: `IndexDataSetSession::SeekInternal` and
  `GetNextRowValuesInternal` < `IndexRowScanner::EstablishInitialKeyOrderPosition` < `GetRowForKeyValue` <
  `IndexPageManager::GetPageWithKey` < `BTreeMgr::Seek`, with `BTreeMgr::HandleRoot` fetching the root page, one
  `BPool::Get` per level below it, and `PageRef::SearchPage` finding the key on each page. `CQScanRangeNew::SetRange`
  with `CEsRuntime::Eval` just before it is the seek key being evaluated.
- **Under `BPool::Get`** a page already in the pool costs only `BUF::AcquireLatch` < `LatchBase::AcquireInternal` (the
  buffer pool read). A page that is not there goes through `BPool::GetFromDisk`: `BPool::Steal` and
  `MemoryClerkInternal::AllocatePages` take buffers for the extent, `BUF::AcquireIOLatch` holds them while
  `RecoveryUnit::ScatterRead` < `FCB::ScatterRead` < `KERNEL32!ReadFileScatter` issues the I/O, the task yields
  (`Worker::OSYieldNoAbort` < `SOS_Scheduler::SwitchContext`), and the completion runs `IOQueue::CheckForIOCompletion`
  < `FCB::IoCompletion` < `BPool::ReadPageCompletion` < `FCB::PostRead` (`Page::VerifyChecksum`,
  `Page::RemoveTearProof`) once per page before the SH latch is taken. That is the disk read.
