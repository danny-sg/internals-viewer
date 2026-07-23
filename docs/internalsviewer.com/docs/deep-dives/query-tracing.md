# How query tracing works

The Query view runs your SQL while recording what the storage engine does, then rebuilds the activity as a timeline that can be replayed. The recording is done with [Extended Events](https://learn.microsoft.com/en-us/sql/relational-databases/extended-events/extended-events) - SQL Server's built-in, low-overhead tracing framework - plus a few tricks to make the engine's work visible.

## Setting up the trace

When you press Execute, Internals Viewer runs a sequence of steps on its connection:

1. **Create an Extended Events session** named `QueryReplay_<guid>`, capturing the events listed below. The session is filtered to the connection's own `@@SPID`, so only this query's activity is recorded - not everything else happening on the server. The session writes to an event file target (`.xel`) in the SQL Server log directory.

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

Each event also carries actions - `sql_text`, `session_id`, `database_id`, `plan_handle`, and `transaction_id` - used to tie events to the right statement and plan. The Events menu tailors the session: the page I/O events are always captured, the lock, wait, and memory events can be toggled, and enabling _Call Stack_ adds the `package0.callstack` action, attaching the raw SQL Server call stack to every event.

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

## Data modifications

`INSERT`, `UPDATE`, and `DELETE` get special handling so they can be traced without permanently changing the database:

1. The current end of the transaction log is noted (via `fn_dblog`)
2. A named transaction is started, and the `sqlserver.transaction_log` event is added to the session
3. The query runs inside the transaction
4. The log records the query generated are read from `fn_dblog`
5. The transaction is **rolled back**

The trace captures everything the modification did - the pages written, the locks taken, the log records generated - but the database ends up exactly where it started. This is also why the timeline can show what a modification _would_ do page by page: the log records describe each individual change.

## In the source

- `InternalsViewer.Query/QueryRunner.cs` - session setup, the run sequence, and cleanup
- `InternalsViewer.Query/Events/EventReader.cs` and `EventParser.cs` - reading the `.xel` file back and parsing events
- `InternalsViewer.Query/Plans/ExecutionPlanParser.cs` and `EventPlanNodeMatcher.cs` - plan XML parsing and matching operators to profile events
- `InternalsViewer.Query/KeyHashLookup.cs` - resolving lock key hashes to rows
- `InternalsViewer.Query/Callstack/` - symbol download (`SymbolDownloader.cs`), DIA resolution (`CallstackResolver.cs`), and the Module / Category classification (`Categories/`)
- `InternalsViewer.Query/TransactionLog/LogRecordReader.cs` - reading log records for modifications
