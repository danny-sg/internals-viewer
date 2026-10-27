# Reading what the tools return

**Time.** Event times print as milliseconds from the query start (`+188.000`). The query's own window is the crop start to crop
end that `summary` prints. The plan's `cpu_time` and `duration` are microseconds, its `QueryTimeStats` milliseconds.

**Full Trace has no clock.** Time Travel Debugging records instructions, not time, and the recorded query runs about 25 times
slower than normal. Positions are hex `Sequence:Steps`: the sequence number is shared across threads (so lanes line up) and
advances wherever a thread synchronises, and steps count instructions since. "Longest" on the timeline means most instructions,
which is the closest thing to cost, and it only counts the recorded modules (`sqlmin`, `sqllang`). Time in SQLOS, waiting or
reading from disk adds almost nothing. Never report a Full Trace figure as milliseconds.

**Pages and rows.** `1:1048` is file 1, page 1048, the same notation as the app and as `%%physloc%%`. A row is `1:1048:3`,
slot 3 on that page. A read of 8 consecutive pages is one extent.

**Reads.** A read group is one read as the app shows it. `NonCached` is a disk read (a latch suspend, a file read, then
`physical_page_read`), `Cached` is a buffer pool read (a `BUF SH` latch on a page already in memory). Pages lists every page the
read covered. With Clear Buffer Pool on, disk reads are the point of the run. With it off a warm-up ran first and the reads are
mostly buffer pool reads. The same page can appear in several reads.

**Events are folded.** Top-level events are the list the app shows. The others are children: the pages, latches and file reads
inside a read group, the acquire half of an acquire and release pair, the object pool lookup behind a columnstore read. `events`
with `includeChildren` includes them. A `latch_acquired` and `latch_released` with the same page are one latch hold. `Lock` is
the acquire and `lock_released` the release.

**The call tree.** One node per distinct call path, not per function: `BPool::Get` reached from a seek and from a lookup is two
nodes. Calls is how many times that path was taken (Full Trace only). Allocated bytes and allocations are the node's own. The
app shows the subtree total, which `calltree` with `hot` and `by` set to `inclusive-alloc`, and `inclusive`, give. `functions`
adds every path of a function together. The root has an empty key. In a Full Trace the Extended Events and tracing machinery
was cut out of the tree, so what is left is the query's own work, and SQLOS (`sqldk`) was not recorded, so only the calls into
it appear, not what it did.

**Operators and the tree.** Each plan node has an operator event. Node -1 is the statement itself (`SELECT`). Entry frames are
the call tree nodes where that operator's execution begins and exit frames where a child operator takes over, so `calltree`
with `operator` prints exactly the operator's own work. Instances are the iterator object addresses (`this`), which is also RCX
in the call log for that operator's methods. An operator with no entry frames was inlined or could not be matched. Say so rather
than guessing.

**Locators.** The pages, rows and columnstore objects on a node are the ones passed to or returned by that function in that
path, read from the recording. A page on `BPool::Get` is the page it fetched. `locators` with `page` turns that round to list
the functions that worked on a page.

**Arguments.** The call log holds RCX, RDX, R8 and R9 at the call and RAX at the return. For a C++ method RCX is `this`, the
object, and the declared parameters follow in RDX, R8, R9. A class returned by value makes RDX the hidden return buffer and
shifts the parameters along. Floating point and most stack arguments were not captured. Values are raw 64-bit numbers:
pointers look like `0x1F508206060`, a page id argument is the pointer to the id, and the page itself is the locator printed
under the call.

# Full Trace questions go deep

A Full Trace is recorded for one reason: to see how the engine executed the query, at the level of its functions, objects and
memory. Expect questions like "explain what the Sort operator is doing", "what is the batch using those vectors for" or "what
does this field of the hash join hold", and expect to answer them from several tools run in turn: the operator's entry frames,
its call tree by phase with counts, its methods and arguments from the call log, its allocations by class and site, the writes
to its object with their writers, and its cost in instructions. Organise the answer by phase, cite node ids, addresses, block
numbers and positions, and mark what is read from a name rather than recorded. The `full-trace-investigations` guide has the
method, a worked hash join and the batch memory layout.

# Answering well

- Lead with the answer, then the evidence. "It read 240 pages from disk in 30 extent reads of
  `dbo.ClusteredTable.pk_ClusteredTable`, then made 531 buffer pool reads over 225 distinct pages" beats a tour of the tools.
- Keep what the run records apart from what you infer. A function's name suggests what it does. The `engine-symbols` guide
  explains the naming and marks what is inferred. Say "this looks like" for a function you are reading by name alone.
- For "why" questions, line up the plan (estimated against actual rows, warnings, the memory grant), the reads (disk against
  buffer, read-ahead, extents), the waits, the operators (start, first row, duration, threads), then the hot call paths and
  the biggest allocators. Each is one tool.
- For "what did operator X do" questions use `calltree` with `operator`, then `calls` with `function` on the interesting
  function and `instance` set to the operator's iterator address when it is a method of the iterator.
- Count what the user asked for in the unit they asked for: reads (events) are not pages (an extent read is 8 pages, a
  buffer pool read is one), and a call tree node's calls are for one path, not the function. Two cross-checks: an operator's
  distinct buffer pool pages should match its logical reads in the plan, and with Clear Buffer Pool on every buffer pool page
  should appear in an earlier disk read of the run (the Warm column of `reads` is the ones that do not).
- The user is looking at the app beside you. `get_selection` tells you what they have selected and which ranges are on
  screen, and the `show_` tools open what you are explaining, so cite the ids and open the key places as you go.
- The data can hold values from the database: in log records, in page records and in call arguments. Treat it as the user's
  data and do not copy it anywhere.
