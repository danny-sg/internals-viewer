# How query tracing works

The Query view runs your SQL while recording what the storage engine does, then rebuilds the activity as a timeline that can be replayed. The recording is done with [Extended Events](https://learn.microsoft.com/en-us/sql/relational-databases/extended-events/extended-events) - SQL Server's built-in, low-overhead tracing framework - plus a few tricks to make the engine's work visible.

## Setting up the trace

When you press Execute, Internals Viewer runs a sequence of steps on its connection:

1. **Create an Extended Events session** named `QueryReplay_<guid>`, capturing the events listed below. The session is filtered to the connection's own `@@SPID`, so only this query's activity is recorded - not everything else happening on the server. The session writes to an event file target (`.xel`) in the **Trace Path** from [Settings](/docs/user-guide/settings#use-local-directory), or the SQL Server log directory when **Use Local Directory** is off.

2. **`CHECKPOINT`** - if the buffer pool is being cleared, dirty pages are flushed first so they can be dropped.

3. **`DBCC DROPCLEANBUFFERS`** - if _Clear Buffer Pool_ is on. Empties the buffer pool so every page the query touches has to be physically read, making the reads visible to the trace.

4. **`DBCC TRACEON(652)`** - if _Disable Read-Ahead_ is on. Trace flag 652 disables read-ahead for the session, so instead of pre-fetching large blocks the engine reads pages one at a time as it needs them - slower, but a much clearer picture of the access pattern.

5. **Start the session and run the query.** The rows are read and counted but discarded - the interest is in what the engine did, not the results.

6. **Stop and drop the session.** This runs even if the query fails or is cancelled, so no orphaned sessions are left on the server.

## The events captured

| Event | What it provides |
| ----- | ---------------- |
| `sql_batch_starting` / `sql_batch_completed` | The boundaries of the batch - the timeline's time range |
| `physical_page_read` / `physical_page_write` | Each page I/O with its page address - the Read lane, and the highlights on the Allocations and Index views |
| `lock_acquired` / `lock_released` | Each lock with its resource and mode - the Lock lane |
| `wait_info` | Each wait with its type and duration - the Wait lane |
| `query_thread_profile` | Per-operator runtime statistics - when each plan operator was active, for the Plan lane |
| `query_post_execution_showplan` | The actual execution plan XML - the Execution Plan view |
| `page_split` | Page splits caused by the query |
| `query_memory_grant_usage` / `hash_spill_details` / `sort_warning` | Memory granted and used by the query, and spills to tempdb |
| `log_flush_complete` / `file_write_completed` | Transaction log and data file write activity |
| `latch_acquired` / `latch_released` / `latch_suspend_begin` / `latch_suspend_end` | Page latches, and the latch waits that mark a disk read - the Latch lane |
| `lock_escalation` | Lock escalations, the markers on the Lock lane |
| `file_read` / `file_read_completed` | The file I/O behind each physical read, and the pages a scatter/gather read covered |
| `query_execution_column_store_segment_scan_started` / `_finished`, `column_store_rowgroup_read_issued`, `column_store_segment_eliminate`, `column_store_object_pool_hit` / `_miss` | Columnstore segment scans, rowgroup reads, rowgroup elimination and object pool lookups - the Columnstore lane |
| `query_execution_batch_filter`, `column_store_expression_filter_apply`, `query_execution_push_down_aggregate`, `query_execution_dynamic_push_down_statistics` | Batch mode filters and aggregate pushdown - the Columnstore lane and the plan annotations |
| `transaction_log` | The log records of a traced data modification - the Log lane |
| `memory_grant_updated_by_feedback` | Memory grant feedback, alongside the memory events |

Each event also carries actions - `sql_text`, `session_id`, `request_id`, `database_id`, `plan_handle`, and `transaction_id` - used to tie events to the right statement and plan, and `sqlos.system_thread_id`, `task_address` and `worker_address`, which identify the thread and task behind each event and tie a parallel plan's events to its workers. The Events menu tailors the session: the page I/O events are always captured, the lock, wait, and memory events can be toggled, and enabling _Call Stack_ adds the `package0.callstack` action, attaching the raw SQL Server call stack to every event.

## Building the timeline

After the session stops, the event file is read back with `sys.fn_xe_file_target_read_file` and parsed into typed engine events.

Timestamps put every event on a common timeline, and the playhead simply moves through it - as it passes each `physical_page_read` the corresponding page lights up on the Allocations or Index view.

### Two clocks - milliseconds vs microseconds

There is a resolution mismatch at the heart of the timeline. Extended Events timestamps are only accurate to the **millisecond**, but the plan operator runtime statistics (from `query_thread_profile` and the plan's run-time counters) work in **microseconds** - and a fast query can do a lot inside one millisecond, with dozens of page reads sharing an identical timestamp.

To make every event individually addressable on the timeline, events that share the same coarse timestamp are spread evenly across their millisecond window, in capture order - the k-th of n coincident events is offset by `1000µs × k / n`. Order is preserved, and each read gets its own moment for the playhead to land on.

### Matching events to operators

The plan XML from `query_post_execution_showplan` is parsed into an operator tree, but most events don't say which operator caused them - a `physical_page_read` only knows which page it read. Events are matched to operators using three signals, in decreasing order of confidence:

1. **Node id** - `query_thread_profile` events carry the operator's node id from the plan, so they match directly. They also establish a per-operator execution time window.
2. **Object identity** - a page read, I/O, or lock event resolves through its page to an allocation unit, and the (table, index) pair usually identifies exactly one operator - a seek on a non-clustered index can only belong to the operator using that index.
3. **Timing** - when identity alone is ambiguous (the same index read by two operators in a self-join, say), the operator whose execution window best contains the event's timestamp wins.

That is what connects the views: an operator in the Execution Plan pane, its activity bar in the Plan lane, and the reads it caused in the Read lane are all the same plan node seen through different events.

### Execution phases - blocking vs streaming

The operators are also classified by _how_ they process rows, which is what the Plan lane and Execution Plan animate during replay:

- **Streaming operators** (scans, seeks, nested loops, compute scalar, etc.) emit rows as they receive them - their bar is active from their start.
- **Blocking operators** (hash match, sort, etc.) must consume their input before they can produce anything. For these the timeline works out when rows first flowed *out* of the operator, and the span before that is the consume phase, drawn dimmed. A hash join is broken into its **build** phase (reading the build input into the hash table) and **probe** phase (streaming the probe input through it).

The phases propagate up the tree: a streaming operator can't emit rows before its child does, so it inherits its child's emit time - a blocking operator anywhere below delays the whole chain above it. This is why, replaying a hash join, nothing streams to the `SELECT` until the build phase completes, and why a sort at the bottom of a plan pushes every bar above it into its dimmed waiting state.

Lock events get one more resolution step. A row lock doesn't report which row was locked - it reports a hash of the key (the same value the `%%lockres%%` virtual column exposes). Internals Viewer queries the table for matching key hashes to resolve them back to real rows, so a lock event can point at the actual record.

## Resolving call stacks

The `package0.callstack` action doesn't produce function names - each raw frame identifies the **module** that generated it, the name of its **PDB** (the symbol file mapping the compiled binary back to names), a **GUID + age** pinning the exact PDB revision for that build, and the **RVA** (relative virtual address) of the frame within the binary. Turning that into `sqlmin!IndexPageManager::GetNextPage` takes three steps:

1. **Download** - each distinct PDB referenced by the trace is fetched from the Microsoft public symbol server, using the standard symbol store path `https://msdl.microsoft.com/download/symbols/<pdb>/<GUID><age>/<pdb>`. The same folder layout is replicated under the local **Symbols Path** (default `C:\Symbols`), so each symbol file is only ever downloaded once - subsequent traces resolve from the cache.

2. **Resolve** - the RVA is mapped to a function name against the cached PDB using the Debug Interface Access (DIA) API, giving the `module!Class::Method` symbol and the offset into the function. DIA normally comes with Visual Studio, but its redistributable `msdia140.dll` ships with Internals Viewer and is accessed registration-free through a small C++ bridge (`InternalsViewer.Query.DiaBridge`) - so there are no dependencies to install and no COM registration step.

3. **Classify** - the resolved symbols are classified by a mapping dictionary into the **Module** badge (Storage Engine, Query Processor, SQL OS, SQL Server Host, etc.) and the **Category** badge (Index Access, Row Access, Page Access, Buffer Manager, Buffer Pool, Latching, Lock Manager, etc.) shown in the Call Stack pane. Frames belonging to infrastructure - Extended Events publishing, scheduling, thread management - are flagged so the frames doing the actual work stand out.

## Full Trace

Everything above is sampled. A call stack is captured when an event fires, so the call tree only ever holds the code that was on the stack at those moments, and the relational operators fire no events of their own - see [How operator matching works](/docs/deep-dives/operator-matching) for what that costs. **Record Full Trace** swaps sampling for a recording. [Time Travel Debugging](https://learn.microsoft.com/en-us/windows-hardware/drivers/debuggercmds/time-travel-debugging-overview) (TTD) records every instruction a process executes, and the recording can be replayed afterwards, so every call the query made can be read back in order, with its arguments.

Recording a SQL Server service is not quite what TTD was designed for, and most of what follows is about making it work.

### The elevated harness

Attaching a recorder to a service needs administrator rights, which Internals Viewer does not run with. So each run starts a small helper elevated, `InternalsViewer.TraceHarness.exe`, with a single UAC prompt. The app and the harness then signal each other through named events - prepared, attach, ready and stop - so the harness can be started before the query connection opens, attach at the last moment and stop when the query is done, all under the one prompt.

The harness is native C++ with the C runtime linked in, so it loads nothing but Windows system DLLs. It runs as administrator, so it should not load anything a user could swap. For the same reason it opens every executable and DLL in the TTD folder so they cannot be changed while it runs, and checks each one is signed by Microsoft before it starts TTD.

TTD is copied from the WinDbg Store package into `%ProgramData%\InternalsViewer\TimeTravel\Recorder`, with read access for `NT SERVICE\ALL SERVICES`. The recorder works by injecting a DLL into the target process, and the SQL Server service cannot load it from inside the Store package, or from a copy under the user profile - the injection fails with "Client DLL not found". From ProgramData it works.

### Why the service restarts

TTD cannot be unloaded from a process. Once a `sqlservr.exe` has been recorded the recorder stays inside it, and attaching again only signals that recorder to restart tracing - which then records just the thread that signalled it. That is one useful recording per SQL Server process.

So before attaching, the harness lists the modules loaded in SQL Server. If TTD is among them it restarts the service - stopping the services that depend on it, such as SQL Server Agent, then SQL Server, then starting them all again - and records the new process. The app waits for the database to come back online before carrying on. A recording of a freshly started instance needs no restart, and nothing is restarted at the end.

The run then goes:

1. **Prepare** - the harness starts, restarting SQL Server if it has to, before the query's connection opens
2. **Warm up** - unless _Clear Buffer Pool_ is on, the query runs once untraced inside a transaction that is rolled back, so compiling the plan and reading its pages are not in the recording
3. **Snapshot the memory clerks** - for naming the memory later
4. **Create the event session** - then the harness attaches TTD with `-module sqlmin.dll -module sqllang.dll` just before the session starts
5. **Run the query** - the results are read and the recording stops
6. **Snapshot the memory clerks again**

Recording only `sqlmin.dll` and `sqllang.dll` keeps the trace to the storage engine and the query processor. SQLOS, in `sqldk.dll`, does scheduling, spinlocks and Extended Events on every thread, and recording it would add a great deal for little gain. It also means the recording comes in islands. Whenever execution leaves the recorded modules - into SQLOS, or into Windows - the trace has a gap until it comes back.

### A minimal event session

Extended Events is code that runs on the query's own thread, so under a recording every event published is recorded calls. With the normal event set the event machinery made up most of the recording. In Full Trace mode the session is cut down to what is needed:

| Event | Why |
| ----- | --- |
| `sql_batch_starting` / `sql_batch_completed` | The boundaries of the batch |
| `query_thread_profile` | Per-operator counters, including the actual rows used to match operators |
| `query_post_execution_showplan` | The actual plan |
| `wait_info`, only the end of `CXPACKET`, `CXCONSUMER`, `CXSYNC_PORT` and `CXSYNC_CONSUMER` waits | Finding the parallel worker threads |

On one query that took the recording from 24.5 million calls to half a million, and the recorded query from 25 seconds to 5.

The waits are about threads. A recording holds every thread in the process, and the replay should only follow the query's, so Full Trace adds the `sqlos.system_thread_id` action - the operating system thread each event fired on. The query's own thread is on every event. Parallel workers are harder: `query_thread_profile` only fires on the coordinator, and `sqlos.task_started` never fires with the session's context. What every worker does do is wait on its exchange at least once, with a `CXSYNC_PORT` wait as it starts, so the filtered waits name every worker's thread. The thread ids are taken straight from the raw rows of the event file, before any other processing, since the processed events lose the workers' waits.

Workers are pooled, so a worker's thread can run another task inside the recording window - a Query Store flush, a system task - and its calls come along too.

**Record Extended Events** turns the normal event set back on. The events' `Publish` functions stay in the replay as markers (see below), so Messages can compare the events published on each recorded thread with those that reached the file.

### One pass through the replay

The recording is read by `InternalsViewer.Query.TimeTravelBridge`, a native C++ bridge over the TTD replay API. The API's headers come from the `Microsoft.TimeTravelDebugging.Apis` NuGet package, and the replay engine itself, `TTDReplay.dll`, is loaded from the same TTD copy that made the recording, so the two always match.

Opening the recording is the slow part. The replay engine indexes the whole file before it will replay anything, which takes a minute or more and about as much memory as the file is large, and it has no lazy or partial open.

After that the whole trace is replayed exactly once, from the first position to the last, with a callback on every call and return and on every gap. Two measurements decided that:

- **Threads** - replaying "only the current thread" still emulates every thread, and even hands the other threads' calls to the callback. A pass per thread costs a full replay each, and 13 threads took around 15 minutes. One pass, filtering threads inside the callback, replayed 102.7 million calls on 132 threads in 65 seconds.
- **Seeking** - moving the replay to a position to read something costs 100 to 200 ms. That is fine for one value and hopeless for millions.

So everything a Full Trace needs - the call tree, the Flame Chart's spans and the argument log - is gathered inside that one forward pass, and nothing goes back.

The bridge keeps a shadow stack for each recorded thread. A call pushes a frame and adds one to the count on a node of the call tree, keyed by its parent, the function's address and, for iterator methods, the object it ran on. Frames are popped by stack pointer rather than by pairing each return with its call, because the islands break the pairing. A return pops every frame whose stack pointer it passes, and a large gap clears the stack, so unrelated calls never end up nested under stale frames. A call into an unrecorded module, such as SQLOS, is still seen at the call instruction, so its arguments are logged. When execution reappears at its return address the frame is closed, and RAX is read as the return value.

Each closed frame becomes a span - its start and end positions and instruction counts, its thread and its node - and the spans are streamed to the app 65,536 at a time to build the Flame Chart.

### Leaving out Extended Events

Even a minimal session leaves the event machinery in the trace, and with it tracing code, and on the query's thread those were most of the calls. So before the replay, every function in `sqlmin.dll`, `sqllang.dll` and `sqldk.dll` is classified from its PDB with the same category mappings the Call Stack uses, and the addresses of those in the Extended Events and tracing categories - around 9,000 of them - go to the bridge. A call to one of them pushes an excluded frame, and nothing beneath it is logged. Messages reports how many functions were excluded.

The exceptions are markers: each event's `Publish` function, such as `XeSqlPkg::lock_acquired::Publish`, and `XE_BufferMgr::Reserve`, which writes an event into a session's buffer. These are logged even inside excluded code, with nothing beneath them, so the events published on the recorded threads can be counted. After the replay, anything left beneath Extended Events or tracing frames is taken out of the call tree and the Flame Chart.

### Matching operators by iterator

Sampled stacks have to work out which frames belong to which operator from the shape of the stacks, because a frame does not record which object it was running on. A recording does. Each operator is an iterator object - a `CQScan*` class in row mode, `CBpQScan*` in batch mode - and its `Open`, `GetRow` and `Close` (`BpOpen`, `BpGetNextBatch` and `BpClose` in batch mode) take the object as `this`, which the x64 calling convention passes in RCX. The bridge is given the addresses of those methods, and on a call to one it reads RCX and makes it part of the node's key. Two Index Seeks under a Nested Loops run the same code at the same addresses, but on different objects, so they stay apart all the way through. A method whose code is shared with some other function by identical code folding is left out, since RCX there might be anything.

The compiler gets in the way in one place. With profile-guided optimisation, `CQScanProfileNew::GetRow` - the wrapper that counts rows for the actual plan - does not call its child's `GetRow`. It calls `CQScanNew::GetRowOrReQualifyHelper` with the child iterator as its first parameter and the child's `GetRow` inlined into it, so for an Index Seek `CQScanRangeNew::GetRow` is never called at all. The helper is treated as an instance method too, which works because its first parameter arrives in RCX just as `this` would. The statement's own frame is folded away in the same way, which is why a statement's entry is `CXStmtQuery::ErsqExecuteQuery`.

The instances then form a tree - each one's parent is the instance that was running when it was first entered - and the tree is aligned with the plan. An instance scores against an operator for a matching iterator class, from the mappings, and for a `GetRow` count that matches the operator's actual rows, allowing an extra call per `Open` for the call that finds no more rows. Skipping an instance or a plan node costs points, and wrappers such as the profiling iterator are looked through. A root instance can only match a top operator, or a Parallelism operator, where each worker's producer is a root of its own.

The matched instances give each operator its entry frames, which drive the Call Stack's operator view, the memory per operator and the Flame Chart's operator lifetimes - each instance lives from its first method call to its last.

### Memory

SQL Server allocates through SQLOS in `sqldk.dll`, which is not recorded. But every allocation the storage engine and query processor make is a call into it, and the argument log already holds each call's registers, so memory is counted from the allocator calls:

- **Allocators by name** - memory objects' `Alloc`, `Realloc` and `Free` (`CMemObj`, `CMemThread` and the rest), `operator new` and `delete`, `MemoryClerkInternal` page allocations, `CQryMemManager`, hash table bucket pages, and by export name the Windows heap, the C runtime and `VirtualAlloc`, counted only when it commits. They are classified in the same pass over the PDBs as the exclusions above.
- **Size and pointer** - the size comes from the entry registers, and the pointer from RAX at the return.
- **Outermost only** - an allocator called inside another is not counted again. Page allocations made by the memory object machinery are its plumbing, since the memory object's own `Alloc` has already counted them.
- **Frees by pointer** - frees are matched to allocations by pointer, across threads, in trace position order, with frees first where they tie so an in-place reallocation works. That gives the memory in use and its peak.
- **Clerks** - the first argument of a `MemoryClerkInternal` call is its memory clerk, and `sys.dm_os_memory_clerks` and `sys.dm_os_memory_objects`, read before and after the recording, name it. `MEMORYCLERK_SQLQERESERVATIONS` is the query's workspace memory, which is what the Flame Chart's **Grant In Use** line draws against the grant.
- **Operators** - each allocation belongs to the innermost operator whose entry frame is on its stack, so a child's memory stays with the child, and to the statement above it. A frame shared by more than one operator is not guessed at.

The known gap is a memory object released as a whole. Its pages go back through the plumbing, so its memory is never seen to be freed, and in use only climbs for it.

### The argument log

Every call is logged, not just counted. For each function and object, each call is eight 64-bit values - its sequence position, its thread and whether it returned, RCX, RDX, R8 and R9 at the call, RAX at the return, and its node in the call tree - and a recording can hold tens of millions of calls.

The bridge keeps each function's calls in chunks of 16,384 and hands them over column by column - every call's RCX together, then every RDX - and the app compresses each chunk with zlib on the thread pool while the replay carries on. Columns, because the values repeat: 41% of them are zero, and only 12% of a column's non-zero values are distinct. Compressed by column the log is around 6.5% of its raw size, against 8.7% by row. It lives in memory and is read lazily - the [Arguments](/docs/user-guide/query/CallStack#arguments) tab decompresses the chunks it shows, and **Find Calls** scans every chunk in parallel.

Stack arguments, the floating point registers and the memory behind pointers are not captured, which is why those show **Not Captured**.

### No clock

TTD records instructions, not time. A position in a recording is a sequence number, shared by every thread and advanced where threads synchronise, plus the number of steps - instructions - the thread has executed since. There is no timestamp, and the recording slows everything down around 25 times, and not evenly, so a millisecond figure would not mean much anyway. This is why the Timeline is hidden for a Full Trace.

The Flame Chart's two axes are built from positions:

- **Position** is `sequence + steps / (largest step count + 1)`, which lines up across threads and is linear within a sequence. An earlier encoding squashed late steps together, and merged a hundred `GetRow` calls into one bar.
- **Instructions** is a clock kept for each thread, adding up the steps between callbacks. It is the closest the recording has to how long a call took, but it is counted separately for each thread, so the lanes do not line up.

## Data modifications

`INSERT`, `UPDATE`, and `DELETE` get special handling so they can be traced without permanently changing the database:

1. The current end of the transaction log is noted (via `fn_dblog`)
2. A named transaction is started, and the `sqlserver.transaction_log` event is added to the session
3. The query runs inside the transaction
4. The log records the query generated are read from `fn_dblog`
5. The transaction is **rolled back**

The trace captures everything the modification did - the pages written, the locks taken, the log records generated - but the database ends up exactly where it started. This is also why the timeline can show what a modification _would_ do page by page: the log records describe each individual change.

## In the source

- `src/Query/InternalsViewer.Query/QueryRunner.cs` - session setup, the run sequence, and cleanup
- `src/Query/InternalsViewer.Query/Events/EventReader.cs` and `Events/Parsing/EventParser.cs` - reading the `.xel` file back and parsing events
- `src/Query/InternalsViewer.Query/Events/Consolidation/` - the passes that pair and group raw events, such as the reads behind a latch or the segment scans of a rowgroup
- `src/Query/InternalsViewer.Query/Plans/Parsers/ExecutionPlanParser.cs` and `Events/Operators/EventPlanNodeMatcher.cs` - plan XML parsing and matching operators to profile events
- `src/Query/InternalsViewer.Query/KeyHashLookup.cs` - resolving lock key hashes to rows
- `src/Query/InternalsViewer.Query/CallStack/` - symbol download (`Symbols/SymbolDownloader.cs`), DIA resolution (`CallstackResolver.cs`), and the Module / Category classification (`Categories/`)
- `src/Query/InternalsViewer.Query.Debugging/` - the WinDbg session and the Time Travel Debugging recorder
- `src/Query/InternalsViewer.Query.Debugging/TimeTravel/` - preparing a recording: the TTD copy, the licence check, the trace folder and starting the harness
- `src/Query/InternalsViewer.Query.TraceHarness/TraceHarness.cpp` - the elevated harness: the signature checks, the service restart, and attaching and stopping TTD
- `src/Query/InternalsViewer.Query.TimeTravelBridge/` - the native replay over the TTD replay API: the shadow stacks, the call tree, the spans and the call log
- `src/Query/InternalsViewer.Query/CallStack/TimeTravel/` - the managed side of the replay: the functions to exclude and track (`ReplayFunctions.cs`), iterator matching (`Iterators/`), memory (`Memory/`), the argument log (`CallLog/`) and the Flame Chart's spans (`Timeline/`)
- `src/InternalsViewer.TransactionLog/LogRecordReader.cs` - reading log records for modifications
