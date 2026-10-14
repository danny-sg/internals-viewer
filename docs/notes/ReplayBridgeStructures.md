# Full Trace replay bridge structures

The bridge takes a set of function lists in, replays the trace once, and passes results back through callbacks while it runs. When the
replay finishes, it hands back the call tree.

| Structure | Direction | Purpose |
|---|---|---|
| `ReplayFunctions` | Managed only | All the function lists for a trace, grouped by role |
| `InstanceMethod` | In | A function whose calls are keyed by the object they run on |
| `LocatorRead` → `PointerRead` | In | A pointer to follow in an argument or return value |
| `PublishedEventFunctions` / `EventPublisher` | In | The extended event publishers and the event writer |
| `TraceHandle` | Handle | An open trace in the bridge |
| `CallNode` | Out (after replay) | One node of the call tree |
| Call log chunk | Out (callback) | Calls with their registers, column-major |
| `CallSpan` | Out (callback) | The positions where one call started and ended on its thread |
| `PointerValue` | Out (callback) | A value read through a `PointerRead` |
| `PublishedEvent` | Out (callback) | An extended event written to a session's buffer |

All addresses are absolute addresses in the recorded process. A "position" is a TTD position: a sequence number and a step within it.
An "instruction count" is the thread's running total of executed instructions. The flame chart's Instructions axis uses it.

## ReplayFunctions

The functions the replay treats specially, grouped by role. It's built per module and combined for the trace.

| Field | Type | Meaning |
|---|---|---|
| `ExcludedFunctions` | `ulong[]` | Left out of the tree along with everything they call. These are XEventInfrastructure frames, and Tracing frames with no iterator badge. |
| `InstanceMethods` | `InstanceMethod[]` | Calls keyed by the object they run on |
| `LocatorReads` | `LocatorRead[]` | Pointers to follow for page, row, columnstore and referenced values |
| `PublishedEvents` | `PublishedEventFunctions` | Event publishers and the event writer |
| `MemoryFunctions` | `MemoryFunction[]` | Allocators and frees. Never passed to the bridge. `AllocationMapper` uses them after the replay. |

**Where it comes from:**

1. `ReplayFunctionResolver` classifies every symbol in sqlmin, sqllang and sqldk.
2. A function only gets a role if every symbol at its address agrees on it.
3. The result is relative to the module and cached by PDB identity (name, GUID and age).
4. For each run, `Rebase` moves it to the module's load address. ASLR means that address can change between runs.
5. `Merge` combines the modules. The heap and C runtime functions from ntdll, kernelbase, kernel32 and ucrtbase come from their PE
   exports, not symbols, and are merged in the same way.

## InstanceMethod

A function whose calls are told apart by the object they're called on.

| Field | Meaning |
|---|---|
| `Address` | The function |
| `Register` | Which argument register holds the object: 0–3 for RCX, RDX, R8, R9 |

**Which register:**

- For a member function it's RCX (`this`).
- For a static function it's the keyed pointer argument, for example the iterator passed to `GetRowOrReQualifyHelper`.

**What the bridge does with it:**

- The register's value becomes the call's instance.
- The instance is part of the call tree node key and the call log key. So two scans of the same class keep separate nodes and separate
  call logs.
- Any function not in the list gets instance 0.
- `PackInstanceMethod` sends each entry as one `ulong`: the address, with the register in the top 4 bits (shift 60).

`InstanceMethodCollector` finds them: iterator classes in sqlmin, plus plan object classes.

## LocatorRead and PointerRead

A value read through a pointer in one of a function's arguments, or in its return value. `LocatorRead` is the managed definition, and
`PointerRead.From` turns it into the native layout.

| LocatorRead | PointerRead | Meaning |
|---|---|---|
| `Function` | `Function` | The function |
| `Register` | `Register` | Where the pointer is (numbering below) |
| `IsReadAtReturn` (`bool`) | `IsReadAtReturn` (`uint`, 0 or 1) | Read when the call returns, not when it's made |
| `Offset` | `Offset` | Byte offset of the final 8-byte value, from the last pointer |
| `Path` (`int[]?`) | `Hops`, `FirstHop`, `SecondHop`, `ThirdHop` | Pointers to follow first, up to three. Each hop is the offset of the next pointer from the current one. |
| `Kind` | (not passed) | How the managed side interprets the value |

**Register numbering** (shared with the bridge):

- 0–3: RCX, RDX, R8, R9.
- 4–7: the fifth to eighth arguments, read from the stack when the call is made.
- 8: the return value (RAX). This only makes sense when read at return.

**Read at return:**

- An argument read at return uses the pointer captured when the call was made. That's how out parameters and the hidden return buffer
  are read: RCX points at memory the callee fills in.
- `TraceCallLog` prefers an at-return read over a call-time read of the same register on the same call.

**Kind** (`LocatorKind`):

| Kind | Meaning |
|---|---|
| `Page`, `Row` | Storage identities, shown as locators on the call |
| `ColumnStoreObject` | A columnstore object key. It's wider than 8 bytes, so it takes several reads at consecutive offsets. |
| `Referenced` | The value behind a pointer or reference argument, shown in the Arguments grid in place of the address |

**How reads are built and limited:**

- `LocatorReadResolver.ReadsOf` builds them from a function's signature (`FunctionSignature`, through `LocatorType`).
- `PointerRead.From` throws if a path has more than three hops (`MaximumHops`).
- A zero pointer, a zero hop, or memory the trace has no value for ends the read, and nothing is passed back.

## PublishedEventFunctions and EventPublisher

The functions the replay watches to find where each extended event was published.

**PublishedEventFunctions**

| Field | Type | Meaning |
|---|---|---|
| `Publishers` | `EventPublisher[]` | Event publishers, one `Publish` function per event type |
| `Writers` | `ulong[]` | Event writers: the addresses of `XE_BufferMgr::Reserve` in sqldk, shared by every event |

| Member | What it does |
|---|---|
| `GetPublishEventName(className, methodName)` | Returns the event name when the method is `Publish` and the class is `<Package>Pkg::<event>` with no further nesting. Otherwise returns null. |
| `IsEventWriter(className, methodName)` | `XE_BufferMgr::Reserve` |
| `From(classifications)` | Collects both lists from a module's `FunctionClassification`s |
| `Merge` / `Rebase` | As on `ReplayFunctions` |

**EventPublisher**

| Field | Meaning |
|---|---|
| `Address` | The `Publish` function |
| `Event` | The event name, as it appears in the `.xel` file, for example `lock_acquired` |

**How the bridge uses them:**

1. Entering a publisher starts a pending event on that thread.
2. Entering the writer while an event is pending records a `PublishedEvent`. The writer's RCX is the session's buffer manager.
3. Both checks run before exclusion. So they still fire inside the extended events code, which is itself excluded from the tree.
4. A publish that never reaches a writer leaves nothing behind. That happens when no session collects the event, or a predicate
   rejects it.

## TraceHandle

A `SafeHandle` around a trace opened by the bridge.

- `OpenTrace` returns it once the replay engine has loaded the trace file and read its module list.
- Releasing the handle calls `CloseTrace`.
- `ReplaySession` owns it, and serialises repeated replays of the same trace on one handle.

## CallNode

One node of the call tree: a function, on an instance, called from a parent node. `GetCallNodes` returns the full tree after
`ReadCallTree` finishes.

| Field | Meaning |
|---|---|
| `Parent` | Index of the calling node, or -1 for a root |
| `Reserved` | Padding, so the 64-bit fields line up with the native struct |
| `Address` | The function |
| `Instance` | The instance register value for an instance method, otherwise 0 |
| `Calls` | Number of calls to this function, on this instance, from this parent |

There's one node per distinct (parent, address, instance). Nodes are only ever appended, so a node index received in a callback during
the replay matches the final tree.

## Call log chunk (callback, not a struct)

Calls, passed back in chunks per function and instance. Each call has 8 values, sent column-major: value `v` of call `c` is at
`columns[v * calls + c]`.

| Value | Meaning |
|---|---|
| 0 | Sequence of the call |
| 1 | Thread id, with bit 32 set once the call has returned |
| 2–5 | RCX, RDX, R8, R9 at the call |
| 6 | Return value (RAX), once returned |
| 7 | Call tree node index |

**When chunks are flushed:**

| Trigger | When |
|---|---|
| Normal | At 16,384 calls of a function, once none are still running |
| Forced | At 65,536 calls of a function, even with calls still running. Those calls lose their return value and at-return reads. |
| Idle | Every 262,144 calls across all functions. Flushes each function with at least 256 calls and none still running. |
| End | Whatever is left when the replay finishes |

A call's row (its index within its function and instance) is how `CallSpan.Call` and `PointerValue.Call` refer to it.

## CallSpan

The positions where one call started and ended on its thread.

| Field | Meaning |
|---|---|
| `StartSequence`, `StartSteps` | Position at entry |
| `EndSequence`, `EndSteps` | Position at exit |
| `StartInstructions`, `EndInstructions` | The thread's instruction count at entry and at exit |
| `Node` | Call tree node index |
| `Thread` | Thread id |
| `Call` | The call's row in the call log for its function and instance. `uint.MaxValue` when it has none. |
| `Flags` | `TraceSpanFlags` |

**Flags:**

| Flag | Value | Meaning |
|---|---|---|
| `Returned` | 1 | The call returned normally |
| `StartUnknown` | 2 | The frame was already running when the replay first saw it. Its start is that first sighting, not its entry. |

**When a span is recorded:** when its frame is popped. That's on a return, when exception unwinding leaves the frame, at a large gap
in the recording, or at the end of the replay. Only a real return sets `Returned`.

**When `Call` has no row:** the frame was already running when the replay first saw it, which is the `StartUnknown` case.

**Ordering, exclusion and flushing:**

- Spans arrive in the order their frames end, so children come before their parents.
- Excluded frames produce no span. Their time counts in the parent's span.
- Spans are flushed every 65,536 and at the end.

## PointerValue

A value read through a `PointerRead` during one call.

| Field | Meaning |
|---|---|
| `Function`, `Instance` | The call log key |
| `Call` | The call's row in that log |
| `Value` | The 8 bytes read at the end of the pointer path |
| `Register`, `Offset`, `IsReadAtReturn` | Which `PointerRead` produced it. One function can have several. |

**How it's matched up:**

- `TraceCallLog` finds the `LocatorRead` by (function, register, offset, at-return) to know the value's `Kind`.
- For each call and register, an at-return value wins over a call-time one.
- Values are flushed every 65,536 and at the end.

## PublishedEvent

An extended event the replay saw published and written into a session's buffer.

| Field | Meaning |
|---|---|
| `Publisher` | The `Publish` function. Its `EventPublisher` gives the event name. |
| `SessionBuffer` | The writer's RCX: the `XE_BufferMgr` of the session it was written to |
| `Sequence`, `Steps` | Position of the publish |
| `Instructions` | The thread's instruction count at the publish |
| `Node` | The nearest recorded caller in the call tree. The extended events code itself is excluded. |
| `Thread` | Thread id |

**One record per session buffer:** a publish written to two sessions gives two records, one per buffer. Repeated writes of the same
publish into the same buffer are recorded once.

**When pending publishes are dropped:** when the stack unwinds past the `Publish` frame, and at a large gap.

**Flushing:** every 65,536 and at the end.

**How `PublishedEventLinker` uses them:**

1. It keeps only those in the busiest buffer, which is our session.
2. It groups them by thread and orders them by position.
3. It pairs them in order with the events read from the `.xel` file on that thread, by event name. It can skip ahead up to 64 entries
   to get back in step.
4. Each pair becomes a `TraceEventLink`, which gives the event its node, position and instruction count in the trace.
