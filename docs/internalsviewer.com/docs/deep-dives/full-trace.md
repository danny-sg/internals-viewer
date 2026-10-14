# How Full Trace works

Full Trace records SQL Server running a query, then reads back every function call the query made. This page explains how. It starts with what a call and a stack frame are, because everything after that is about capturing them.

## Calls, frames and stacks

SQL Server is a C++ program, and like any program it does its work by functions calling other functions. Running a query is a handful of functions at the top - receive the batch, run the statement, run the plan - and beneath them thousands or millions of calls into the query processor, the storage engine and SQLOS, SQL Server's own layer over the operating system.

**A function call** has the same shape every time:

1. The caller puts the arguments where the function expects them, and calls it.
2. The call saves the return address - the instruction after the call, where the caller will carry on - and jumps to the function.
3. The function runs, perhaps calling others along the way, and leaves its result where the caller expects it.
4. It returns to the saved address, and the caller picks up from there.

On 64-bit Windows, where the function expects its arguments is fixed by the x64 calling convention. The first four integer or pointer arguments travel in the RCX, RDX, R8 and R9 registers, floating point values in the XMM registers, any more go on the stack, and the result comes back in RAX. A C++ method gets one more argument than it declares - `this`, the object it was called on - and it goes first, in RCX. That detail turns out to matter a great deal - see [Operators by iterator](#operators-by-iterator).

**A stack frame** is the space a call takes on its thread's stack. It holds the return address, the registers the function has to put back, its local variables and any arguments beyond the fourth. Each call pushes a frame and each return pops one, so the stack always holds exactly the calls still in progress.

**A call stack** is that chain of frames at one moment, read from the function running now back to where the thread started. Each frame is the reason for the one above it. A stack caught as a page is fetched might read, innermost first:

- `BPool::Get` - the buffer pool, finding the page in memory or reading it from disk
- the storage engine code reading a row from that page
- the Index Seek's iterator, asked for its next row
- the operator above it, asking for its own next row
- the statement, running its plan
- the worker thread that picked up the batch

Read that way, a stack answers two questions at once - what the code was doing, and why. The same `BPool::Get` called from a different operator, or from a different place in the same one, is a different stack.

**A call tree** merges many stacks. Each distinct path from a thread's starting point down to a function appears once, with a count of how often it was seen. The part every stack shares - the statement and the plan's operators - forms the trunk, and the query's work spreads out into the branches beneath. That is what the [Call Tree](/docs/user-guide/query/CallStack) shows.

## Why capture it

The execution plan says what SQL Server decided to do, and the counters say how much - rows, reads, CPU time. Neither says how. The calls do:

- **Which code ran** - the functions behind each operator, in the storage engine as well as the query processor. The plan shows an Index Seek, and the calls show the B-tree navigation, the pages fixed in the buffer pool, the latches and the row reads beneath it.
- **How often** - a count on every path, so a loop that runs once per row shows up as a call per row, and an operator doing more work than expected stands out.
- **In what order** - each thread's calls one after another, as execution actually unfolded. That is what the [Flame Chart](/docs/user-guide/query/FlameChart) draws.
- **What each call worked on** - its arguments and its return value. `BPool::Get` is passed a pointer to the id of the page it fetches, an allocation is passed a size and returns the address of the memory, and an iterator method is called on its operator's object. That is how a call is tied to a page, a row, a block of memory or an operator in the plan.

Showing the storage engine at work is what Internals Viewer is for, and the calls are the most direct record of that work there is.

## Sampling and recording

The rest of the Query view is built from samples. A call stack is captured when an Extended Event fires, so the call tree only holds the code that was on the stack at those moments - and the relational operators fire no events of their own (see [How operator matching works](/docs/deep-dives/operator-matching)). Full Trace swaps sampling for a recording. Every instruction SQL Server's storage engine and query processor execute is recorded, the recording is replayed, and every call the query made is read back in order - with its arguments, its return value and the memory it allocated.

A sampling profiler such as ETW cannot do that. It sees where the CPU was at intervals, not every call, and it captures no memory, so there is nothing to read an argument's value from. A recording has both.

This page covers the four parts that make it work: what Time Travel Debugging provides, the elevated harness that makes the recording, the native bridge that replays it, and how the replay is mapped back onto SQL Server's plan and memory.

## What Time Travel Debugging provides

[Time Travel Debugging](https://learn.microsoft.com/en-us/windows-hardware/drivers/debuggercmds/time-travel-debugging-overview) (TTD) ships with WinDbg and has two halves.

**The recorder**, `TTD.exe`, attaches to a running process by injecting itself into it, then records every instruction the process executes into a `.run` file - enough to replay the execution exactly, registers and memory included. It can be limited to chosen modules with `-module`, and Full Trace records only `sqlmin.dll` and `sqllang.dll`, the storage engine and the query processor.

**The replay API** reads a `.run` file back. The headers come from the `Microsoft.TimeTravelDebugging.Apis` NuGet package, and the engine itself is `TTDReplay.dll`, shipped beside the recorder. It provides:

- **An index of the recording** - `IReplayEngine::Initialize` reads and indexes the whole file before anything else can be done
- **Modules** - each module loaded in the process, with its path, base address and size
- **Threads** - each thread recorded, with the span of the recording it was active in
- **Cursors** - a cursor replays the recording forward or backward from any position
- **Positions** - a point in the recording is a sequence number, shared by every thread and advanced where threads synchronise, plus the number of steps, instructions, a thread has executed since
- **Callbacks** - a cursor can call back on every call and return, with the call's target and return address and a view of the thread at that instant - its registers, stack pointer, program counter, position, and its memory
- **Gap events** - where the recording has a hole, because execution left the recorded modules or recording stopped

What it does not provide matters as much. There are no symbols, no call tree, no clock, and no idea of what a query or an operator is - only execution. Everything else on this page is built on top.

## Making the recording

### The harness

Attaching a recorder to a service needs administrator rights, which Internals Viewer does not run with. So each run starts a small helper elevated, `InternalsViewer.TraceHarness.exe` (Internals Viewer Trace Harness), with a single UAC prompt.

The app and the harness talk through four named events - **Prepared**, **Attach**, **Ready** and **Stop** - named after the query's session so two Query tabs never cross. That lets the harness start before the query's connection opens, attach at the last possible moment and stop as soon as the query is done, all under the one prompt. Starting `TTD.exe` elevated directly would mean a prompt for each step and no way to check what was being run.

Because it runs as administrator, the harness is careful about what it trusts:

- **One file** - it is native C++ with the C runtime linked in, so it loads nothing but Windows system DLLs. A .NET helper, or the app relaunched elevated, would load far more.
- **Signed recorder** - before running anything it opens every executable and DLL in the TTD folder so they cannot be changed while it runs, and checks each one is signed by Microsoft.
- **A copy the service can load** - TTD is copied from the WinDbg Store package into `%ProgramData%\InternalsViewer\TimeTravel\Recorder`, with read access for `NT SERVICE\ALL SERVICES`. The recorder injects a DLL into the target, and the SQL Server service cannot load it from inside the Store package or from a copy under the user profile - the injection fails with "Client DLL not found".

The harness writes its own log, TTD's output and the recording into a folder per run under `%ProgramData%\InternalsViewer\TimeTravel\Traces`, named after the query's session.

### Why the service restarts

TTD cannot be unloaded from a process. Once a `sqlservr.exe` has been recorded the recorder stays inside it, and attaching again only signals that recorder to restart tracing - which then records just the thread that signalled it. That is one useful recording per SQL Server process.

So before attaching, the harness lists the modules loaded in SQL Server. If TTD is among them it restarts the service through the service control manager - stopping the services that depend on it, such as SQL Server Agent, then SQL Server, then starting them all again - and records the new process. It hands the new process id back to the app, which waits for the database to come back online before carrying on. A freshly started instance needs no restart, and nothing is restarted at the end.

### The run

A Full Trace run goes:

1. **Prepare** - the harness starts and restarts SQL Server if it has to, before the query's connection opens
2. **Warm up** - unless **Clear Buffer Pool** is on, the query runs once untraced inside a transaction that is rolled back, so compiling the plan and reading its pages from disk are not in the recording
3. **Snapshot the memory clerks** - `sys.dm_os_memory_clerks` and `sys.dm_os_memory_objects`, for naming memory later
4. **Attach** - the event session is created, and the harness attaches TTD to the process with `-module sqlmin.dll -module sqllang.dll` just before the session starts
5. **Run the query** - its results are read, then the harness runs `TTD.exe -stop` and the recording is closed
6. **Snapshot the memory clerks again** - while it is still the same SQL Server process

### A minimal event session

Extended Events is code that runs on the query's own thread, so under a recording every event published is recorded calls. With the normal event set the event machinery made up most of the recording. In Full Trace mode the session is cut down to what is needed:

| Event | Why |
| ----- | --- |
| `sql_batch_starting` / `sql_batch_completed` | The boundaries of the batch |
| `query_thread_profile` | Per-operator counters, including the actual rows used to match operators |
| `query_post_execution_showplan` | The actual plan |
| `wait_info`, only the end of `CXPACKET`, `CXCONSUMER`, `CXSYNC_PORT` and `CXSYNC_CONSUMER` waits | Finding the parallel worker threads |

The waits are about threads. A recording holds every thread in the process and the replay should only follow the query's, so Full Trace adds the `sqlos.system_thread_id` action - the operating system thread each event fired on. The query's own thread is on every event. Parallel workers are harder - `query_thread_profile` only fires on the coordinator, and `sqlos.task_started` never fires with the session's context. What every worker does do is wait on its exchange at least once, with a `CXSYNC_PORT` wait as it starts, so the filtered waits name every worker's thread. The thread ids are taken straight from the raw rows of the event file, before any other processing, since the processed events lose the workers' waits.

**Record Events** turns the normal event set back on, and each event is then found in the recording - see [Placing events](#placing-events).

## Opening the recording

When the query finishes the app has a `.run` file and the list of the query's threads. Everything from here happens in the background, in five steps that Messages and the taskbar show progress through - **Opening Full Trace**, **Resolving Symbols**, **Replaying Threads**, **Building Call Tree** and **Matching Operators**. The counts along the way - functions excluded, calls replayed, operators matched, memory allocated, events linked - go to the [Diagnostic log](/docs/user-guide/settings#diagnostic-log).

### Opening

`ReplaySession` loads the bridge, `InternalsViewer.Query.TimeTravelBridge.dll`, from the app's `runtimes` folder and asks it to open the recording. The bridge loads `TTDReplay.dll` from the same TTD copy that made the recording, so the two always match, creates the replay engine and calls `Initialize`.

That is the slow part. The engine indexes the whole file before it will replay anything, which uses about as much memory as the file is large, and it has no lazy or partial open. The bridge then reads the module list - name, base address and size - which is what turns any address in the recording into a module and an offset into it.

### Resolving symbols

A recording knows addresses, not names, so the replay needs to know some functions by address before it starts. Each module's file is opened from the path the recording gives - the recording was made on this machine, so the files are there - and its PE debug directory names its PDB, with the GUID and age that pin the exact build. The PDBs are downloaded and cached exactly as for [sampled call stacks](/docs/deep-dives/query-tracing#resolving-call-stacks).

Then one walk over each PDB of `sqlmin.dll`, `sqllang.dll` and `sqldk.dll` sorts functions into the sets the replay needs:

- **Excluded functions** - every function the Call Stack's category mappings put in Extended Events or tracing, around 9,000 of them, so the replay can skip them
- **Event publishers and the event writer** - each event's `Publish` function, such as `XeSqlPkg::lock_acquired::Publish`, and `XE_BufferMgr::Reserve`, which writes an event into a session's buffer, for [placing events](#placing-events)
- **Instance methods** - `Open`, `GetRow`, `Close` and their batch mode and helper forms on every `CQScan*`, `CBpQScan*` and `CQueryScan*` class, and the methods of the plan's own `CXte*` and `CBpXte*` objects, whose calls are kept apart by the object they ran on
- **Memory functions** - the allocators and frees, for counting memory
- **Locator reads** - functions that take or return a page id, row id, page handle, columnstore object key, LSN or string, and pointer or reference arguments to simple values, for reading those values as the replay passes

A function only gets a role when every symbol at its address agrees on it, since identical code folding can put several functions at one address. The Windows heap and C runtime allocators are classified separately, from the export tables of `ntdll.dll`, `kernelbase.dll`, `kernel32.dll` and `ucrtbase.dll`. The sets are cached per PDB, relative to the module's base address, so a second recording on the same SQL Server build skips the walk and only rebases them.

The walk usually happens before there is anything to replay. Turning on **Record Full Trace** reads the paths of the modules SQL Server has loaded from `sys.dm_os_loaded_modules` and prepares their sets in the background, so by the time a recording finishes the cache is already warm.

## The bridge

The replay API is C++ interfaces, which .NET cannot call directly, so `InternalsViewer.Query.TimeTravelBridge` is a small native C++ DLL between the two. It exports a plain C API that the app calls through P/Invoke:

| Export | Does |
| --- | --- |
| `OpenTrace` | Loads the replay engine, opens and indexes the recording, reads the modules |
| `GetModuleCount` / `GetModule` | The recording's modules |
| `ReadCallTree` | The replay itself - takes the threads, the function sets and the locator reads, streams calls, spans, values and published events back through callbacks, and returns a call tree |
| `GetCallNodeCount` / `GetCallNodes` | The call tree's nodes - parent, address, object and call count |
| `CloseCallTree` / `CloseTrace` | Frees them |

Results come back while the replay runs rather than at the end - call log chunks, call spans, the values read through pointers and published events each through a callback, and progress per thread. Cancelling writes a flag the bridge checks as it reports progress, and interrupts the replay.

### One pass

The whole recording is replayed exactly once, from the first position to the last, with a callback on every call and return and on every gap. Two things decided that:

- **Threads** - replaying "only the current thread" still emulates every thread, and even hands the other threads' calls to the callback. A pass per thread costs a full replay each, where one pass that filters threads inside the callback costs a single replay however many threads there are.
- **Seeking** - moving the replay to a position to read something is slow. That is fine for one value and hopeless for millions.

So everything Full Trace needs is gathered inside that one forward pass, and nothing goes back. What a callback can do cheaply is read the current thread's registers and its own view of memory, and that is all the bridge relies on.

### Shadow stacks

TTD reports calls and returns, not a call tree, so the bridge keeps a shadow stack for each recorded thread:

- **A call** pushes a frame and adds one to the count on a node of the call tree, keyed by its parent, the function's address and, for an iterator method, the object it was called on - the value of RCX, which is how x64 passes `this`.
- **A return** pops every frame whose stack pointer it passes, rather than pairing it with one call. The recording comes in islands - execution leaves the recorded modules on every call into SQLOS or Windows - so calls and returns do not always pair up, and popping by stack pointer keeps a missed return from nesting everything after it under a stale frame.
- **A call into an unrecorded module**, such as SQLOS in `sqldk.dll`, is still seen at the call instruction in the recorded code, so its arguments are logged. The recording then has a gap, and when execution reappears at the call's return address the frame is closed and RAX read as its return value - which is how allocations into SQLOS get their pointers.
- **A large gap**, or the end of recording, clears the stack.
- **A return with nothing on the stack** belongs to a call that started before the recording did. The frame is kept, marked as having an unknown start.

Calls under an excluded function are not logged at all - an excluded frame is pushed and everything beneath it skipped. The event publishers and the event writer are checked before that, so they are still seen inside the Extended Events code that is excluded - see [Placing events](#placing-events).

### What the replay streams back

Each call produces three things:

- **A node count** - the call tree is the node table, with a count on each node, and comes back when the replay ends.
- **A call log row** - eight 64-bit values per call, logged per function and object: its sequence position, its thread and whether it returned, RCX, RDX, R8 and R9 at the call, RAX at the return, and its node. Each function's calls are kept in chunks of 16,384 and handed over column by column - every call's RCX together, then every RDX - and the app compresses each chunk with zlib on the thread pool while the replay carries on. Columns, because the values repeat: 41% of them are zero, and only 12% of a column's non-zero values are distinct, so a column compresses to around 6.5% of its raw size against 8.7% for rows. The log lives in memory and is read lazily.
- **A span** - when a frame closes, its start and end positions, the instructions the thread executed in between, its thread, its node, its call number and whether it returned or started before the recording. Spans are streamed 65,536 at a time and become the Flame Chart.

The call log row holds only the four integer argument registers and RAX. Stack arguments come in alongside the locator reads below: for each function in sqlmin and sqldk with more than four arguments, the fifth to eighth are read from the stack at the call, in one memory read per call, and kept with the locators as argument values rather than pages or rows. The floating point registers are not logged, which is why the [Arguments](/docs/user-guide/query/CallStack#arguments) tab has no column for them, and memory behind pointers is only read by the locator reads.

Alongside the calls come **published events** - an event published and written to an event session, with the position it was published at - see [Placing events](#placing-events).

### Locator reads

There is one exception to not reading memory. For the functions found to take or return a page id, a row id, a page handle, a columnstore object key, an LSN or a string, the bridge reads the value the argument points at while the replay is at the call - or at the return, for a value the function fills in - following a pointer or two where the value sits further away, as a page id does behind a page handle. These are thread-local reads inside the callback, so they cost almost nothing, where reading the same memory after the replay would mean a seek per call.

The values become **locators** - the pages, rows and columnstore segments, dictionaries and delete bitmaps that each call touched.

The same reads cover a pointer or reference argument to a simple value, such as a number, a flag or an enum. The bridge reads the value behind it, so the Arguments tab can show what was pointed at rather than an address that means nothing once the query is over.

## Mapping the replay onto SQL Server

The replay hands back addresses, counts, spans and values. The rest turns them into SQL Server's terms.

### The call tree

Each node's address is looked up in the module ranges and becomes a frame - module, offset, PDB and object - added to a call tree with its count. The frames are resolved to names with the same DIA bridge as sampled stacks, and calls from different places in the same function are merged into one node per function. A map from each raw node to its merged node is kept, so a call log row or a Flame Chart bar always knows its frame - which is what **Go To Call** follows.

The whole recording is kept, compilation included, so what is worked out from it - objects, allocations, memory and event links - sees every call. **Crop To Query** only changes what is shown, and can be switched without tracing again. Each thread's calls start well above the query - receiving the batch, compiling and setting up the statement - so with it on the Call Tree starts at the outermost `CMsqlExecContext::FExecute`, the statement's execution in `sqllang.dll`. A parallel worker's thread never runs beneath that frame, so a thread running operator code without it is shown from its top. The Flame Chart is cropped to match.

Anything left beneath Extended Events or tracing frames is then removed from the tree and the Flame Chart. Each node's activity band counts when its calls started, bucketed by trace position, and its locators are gathered from the call log, so a frame can show the pages and rows it read and the Call Tree's search can find frames by a page address, a RID or `rowgroup:N column:M`.

### Operators by iterator

Sampled stacks have to work out which frames belong to which operator from the shape of the stacks, because a frame does not record which object it ran on. A recording does. Each operator is an iterator object - a `CQScan*` class in row mode, `CBpQScan*` in batch mode - and the call tree already keeps the calls of each object apart, so two Index Seeks under a Nested Loops stay separate even though they run the same code at the same addresses. Iterator methods whose code is shared with some other function by identical code folding are left out, since RCX there might be anything.

The compiler gets in the way in one place. With profile-guided optimisation, `CQScanProfileNew::GetRow` - the wrapper that counts rows for the actual plan - does not call its child's `GetRow`. It calls `CQScanNew::GetRowOrReQualifyHelper` with the child iterator as its first parameter and the child's `GetRow` inlined into it, so for an Index Seek `CQScanRangeNew::GetRow` is never called at all. The helper is treated as an iterator method too, which works because its first parameter arrives in RCX just as `this` would. The statement's own frame is folded away in the same way, which is why a statement's entry is `CXStmtQuery::ErsqExecuteQuery`.

Which object belongs to which operator comes from how the plan is built. Before the first row, `CQueryScan::Setup` walks the compiled plan, and each of the plan's own objects - a `CXte*` class in row mode, `CBpXte*` in batch mode - builds the iterator for its operator. In row mode `CXte*::QScanGet` builds it and returns it, and in batch mode `CBpXte::CreateBatchIterators` and each `CBpXte*::PbpqsGet` build them. These calls nest exactly as the plan does, so the plan objects form a tree with the plan's shape, and their calls are kept apart by object just as the iterators' are.

That tree is aligned with the plan. A plan object scores against an operator when the iterator classes it constructed are ones the mappings give that operator, and loses points when they are not. Skipping a plan node or a plan object costs points too. The iterator each plan object built - its return value, or the object its constructor ran on - then takes that plan object's operator, and wrappers such as the profiling iterator are looked through to the iterator inside. This needs no row counts and no guesswork about stack shapes, only the calls that build the plan.

The linked objects give each operator its entry frames - for the Call Tree's operator view and its target badge - and its lifetime, from its object's first method call to its last, for the Flame Chart's Operators pane. The Arguments tab uses the same link to name an iterator or a plan object when its address turns up as a value.

### Memory

SQL Server allocates through SQLOS, which is not recorded. But every allocation the storage engine and query processor make is a call into it, and the call log already holds each call's registers, so memory is counted from the allocator calls:

- **Allocators by name** - memory objects' `Alloc`, `Realloc` and `Free` (`CMemObj`, `CMemThread` and the rest), `operator new` and `delete`, `MemoryClerkInternal` page allocations, `CQryMemManager`, hash table bucket pages, and the Windows heap, the C runtime and `VirtualAlloc`, counted only when it commits.
- **Size and pointer** - the size comes from the entry registers, and the pointer from RAX at the return.
- **Outermost only** - an allocator called inside another is not counted again. Page allocations made by the memory object machinery are its plumbing, since the memory object's own `Alloc` has already counted them.
- **Frees by pointer** - frees are matched to allocations by pointer, across threads, in trace position order, with frees first where they tie so an in-place reallocation works. That gives memory in use and its peak.
- **Clerks** - the first argument of a `MemoryClerkInternal` call is its memory clerk, and the two clerk snapshots name it. `MEMORYCLERK_SQLQERESERVATIONS` is the query's workspace memory, which is what the Flame Chart's **Grant In Use** line draws against the grant.
- **Operators** - an operator's memory in use is what was allocated inside its own calls on its thread and not yet freed, which is what the Flame Chart's Operators pane draws and gives the peak of.

The known gap is a memory object released as a whole. Its pages go back through the plumbing, so its memory is never seen to be freed, and in use only climbs for it.

### Placing events

The events in the `.xel` file have a timestamp, but no position in the recording, and an Extended Events timestamp is only accurate to a millisecond - far too coarse to find one call among millions. So events are placed by the calls that produced them rather than by time.

Every event is published through its own `Publish` function, such as `XeSqlPkg::lock_acquired::Publish`, and each session collecting it has the event written into its buffer by `XE_BufferMgr::Reserve`, with that session's buffer manager as its first argument. The bridge watches for both, even inside the Extended Events code that is otherwise excluded. Entering a publisher starts a pending event on that thread, and entering the writer while one is pending records a **published event** - the event's name, the session buffer it went to, the position and instruction count of the publish, and the nearest recorded frame above it. A publish that no session collects, or whose predicate rejects it, never reaches the writer and leaves nothing behind.

Other sessions are running too, `system_health` among them, so the buffer that most published events went to is taken to be this session's, and the rest are dropped. What is left is paired, thread by thread and in order, with the raw events read from the `.xel` file, matching on the event name. The order is the same on both sides, since a thread publishes one event at a time, and when a name does not match the pairing skips ahead a short way to get back in step. Each pair links an event to its place in the recording, which is how the Timeline's current event moves the Flame Chart's playhead.

The [Diagnostic log](/docs/user-guide/settings#diagnostic-log) records how many events were published, how many were written to the session and how many were paired and linked.

### No clock

TTD records instructions, not time. There is no timestamp, and the recording slows everything down, and not evenly, so a millisecond figure would not mean much anyway. This is why the Flame Chart measures in positions or instructions, and why events are placed by their publish calls rather than by time:

- **Position** is `sequence + steps / (largest step count + 1)`, which lines up across threads and is linear within a sequence. An earlier encoding squashed late steps together, and merged a hundred `GetRow` calls into one bar.
- **Instructions** is a clock kept for each thread, adding up the steps between callbacks. It is the closest the recording has to how long a call took, but it is counted separately for each thread, so the lanes do not line up.

## What comes out

| Extracted | Where it shows |
| --- | --- |
| Every call path, with its call count | The [Call Tree](/docs/user-guide/query/CallStack) |
| When each path ran across the recording | The Call Tree's activity band |
| Memory allocated beneath each frame | The Call Tree, and the [Flame Chart](/docs/user-guide/query/FlameChart)'s Memory view |
| Every call as a span on its thread | The Flame Chart |
| Each operator's frames, target and lifetime | The Call Tree's operator view and target badges, and the Flame Chart's Operators pane |
| Memory in use per operator | The Flame Chart's Operators pane |
| Every call's register arguments and return value | The [Arguments](/docs/user-guide/query/CallStack#arguments) tab and **Find** |
| The pages, rows and columnstore objects each call touched | The Call Tree's frames and search, and the Arguments tab |
| Where each event happened in the recording | The Flame Chart's playhead, following the Timeline |
| Calls replayed, operators matched, memory allocated, and events published, written and linked | The [Diagnostic log](/docs/user-guide/settings#diagnostic-log) |

The recording itself is deleted once it has replayed successfully. Everything above lives in memory for as long as the Query tab holds the result, and running the query again makes a new recording. **Export to file** saves it all - the call tree, the call log, the spans, the memory and the event links - so a run can be opened again without the recording. See [Exporting a run](/docs/user-guide/query#exporting-a-run).

## Limits

- **Two modules** - SQLOS's own work, scheduling and synchronisation, is not recorded. Calls into it are seen, but not what happens inside.
- **Pooled threads** - workers are pooled, so a worker's thread can run another task inside the recording window, a Query Store flush say, and its calls come along too.
- **Unmatched operators** - an operator whose iterator is inlined into its parent, or whose methods the compiler folded away, cannot be matched.
- **Opening cost** - a recording is slow to open and takes about its own size in memory, whatever is then read from it.

## In the source

- `src/Query/InternalsViewer.Query.TraceHarness/TraceHarness.cpp` - the elevated harness: the signature checks, the service restart, and attaching and stopping TTD
- `src/Query/InternalsViewer.Query.TimeTravelBridge/` - the native bridge over the TTD replay API: the shadow stacks, the call tree, the spans, the call log, the locator reads and the published events
- `src/Query/InternalsViewer.Query.FullTrace/` - the managed side:
  - `Recording/` - preparing a recording: finding WinDbg, the TTD copy, the licence check, the trace folder, starting the harness and the events it signals
  - `TraceLoader.cs` - the load that ties the steps together
  - `ReplaySession.cs` and `Native/` - opening the recording and calling the bridge
  - `Functions/` - the function sets the replay needs, from the PDBs
  - `CallTreeBuilder.cs` - mapping nodes to frames, and `QueryExecutionCropper.cs` - finding where the query's execution starts
  - `CallLog/` and `Arguments/` - the compressed argument log, and reading its values by the function's signature
  - `Locators/` - the page, row, columnstore object and referenced value reads
  - `Iterators/` - linking plan objects and iterators to the plan, and their lifetimes
  - `Memory/` - allocations, frees, clerks and memory in use
  - `PublishedEvents/` - placing events in the recording
  - `Timeline/` - the Flame Chart's spans and axes
  - `Export/` - the Full Trace part of an exported run
- `src/Query/InternalsViewer.Query/QueryRunner.cs` - the run sequence, and `Export/` - exporting and importing a run
