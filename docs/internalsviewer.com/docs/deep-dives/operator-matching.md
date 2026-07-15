# How operator matching works

The Callstack view can show you one plan operator's call stack on its own - the frames that Hash Match executed, without
the thread startup above it or the Index Scan's work below it. Doing that means answering a question SQL Server does not
answer for you: **which part of this call stack belongs to which operator?**

This is how that question gets answered, and why it is answered the way it is.

## Nothing tells you directly

The engine gives us two things that look like they should connect, and don't.

`package0.callstack` gives an **ordered list of frames** for an event - a read, a lock, a latch. That is the shape of the
stack, but the frames are return addresses; the symbols come separately from the PDBs, which are an address-to-name map
with no notion of who called whom.

Showplan gives the **plan** - the operators, their nesting, their costs. But the relational operators
**emit no events of their own**. Only the data-access leaves publish anything. A Stream Aggregate over an Index Scan
never announces itself; it has no reads, no locks, no waits.

So the operator that you want to isolate is the one thing that leaves no trace. This single fact drives the whole design.

## Everything is built from the leaves

If operators emit nothing, an operator's frames cannot be found from its own events - it usually has none.

They are found from the leaves of its **plan subtree** instead. A Stream Aggregate appears on the stacks of the reads its
Index Scan issued, because that is literally how it ran: the aggregate called the scan, the scan read a page, and the
read event captured the whole chain on its way down. Walking up from those leaves reaches the aggregate.

This is why the scope of an operator is always its subtree's events, never its own. Scope a Hash Match to events carrying
its own node id and you get nothing at all.

It is also why the leaf is the only trustworthy starting point. The **event** knows which plan node it belongs to; the
**frame** does not. Two Index Seeks under a Nested Loops run identical code at identical addresses and differ only by
`this`, which nothing in a call stack records. Ask "which operator does this frame belong to?" and there is no answer.
Ask "for this event, what did it pass through?" and there always is.

## A segment is two boundaries

An operator's slice of the stack is bounded by two sets of frames:

- **Entry frames** - where the operator starts executing. `CQScanHashNew::GetRow` for a Hash Match.
- **Exit frames** - where it hands off to something else.

Exits are **stated, not derived**: an operator's exit frames are its descendants' entry frames. This looks like
duplication and isn't. An operator with no events of its own has nothing to derive an exit *from* - there is no evidence
in its own data about where it stopped. But its children know where they started, and a child's entry *is* the parent's
exit. So exits are read off the plan, not inferred from the stack.

The pair is what makes a segment. Cut at the entry and you drop the preamble above; cut at the exits and you drop the
operators nested below. What is left is what this operator did.

## Two signals, because neither covers the other's ground

Deciding an entry frame uses two independent signals.

**The mapping file names the frame's operator.** `Operators.txt` maps iterator classes to plan operators -
`CQScanHash*` is a Hash Match, `CQScanRange*` is an Index Seek *or* a Key Lookup. This is the only thing that separates a
**chain**. When a Stream Aggregate's only events are its Index Scan's, the two own exactly the same set of events; no
amount of event data can tell them apart. Only a name can.

**Ownership needs no names.** A frame beneath which only one operator's subtree ran, whose caller's subtree is wider, is
where that operator's work branched away from its siblings'. This separates a **branch**, and it is exactly what names
are worst at - one iterator class serves operators the plan names unrelatedly, and a missing name costs the parent its
trim as well as the child its segment.

The mapping is tried first and ownership fills the gaps. The reverse is tempting and was tried: it loses. "Where this
node's work branches off" is not "where it was entered" - a Nested Loops re-enters its inner side once per outer row, so
a lookup's work branches away at every row-release the loop drives, and ownership honestly reports all dozen of them. One
is the entry; ownership cannot say which. Naming has no such trouble, and where it is wrong it is wrong in a bounded way.

## The details that turned out to matter

**An operator is rarely one frame.** A hash join spans `Open`, `Iterate`, `ConsumeBuild`, `ReadRow` - all its own, all
matched by the same rule. Stopping at the first match found walking up from a leaf enters it at `ReadRow` and leaves the
build it was called from outside the segment. The entry is the **outermost frame of the first contiguous run**.

**The run has to be contiguous.** That is what separates it from simply taking the outermost match. When a Nested Loops
drives its inner side, its frame appears again further up with the inner operator's frames in between. The run breaks
there, so the operator is entered at the copy the leaf actually came out of, rather than at the outer repeat - which
would swallow the whole recursion.

**Blocking operators run on two branches.** A hash join builds under `Open` and probes under `GetRow`; a Sort pulls every
row during `Open`, so the scan beneath it runs under `BuildSortTable` rather than under a `GetRow`. An operator having
several entry frames is normal and correct, not a symptom.

**Not every matched event ran inside the plan.** A scan's `Object/IS` lock is taken while the statement validates its
schema and released as it cleans up - correctly the scan's lock, nowhere near the scan. Those frames read as "only this
node ran below me" and offer themselves as entries. The filter is structural rather than nominal: the plan's execution
always runs through an iterator, and the preamble around it never does. A fact about the shape of the stack does not
decay the way a list of names does.

**An iterator's constructor is not execution.** `CQueryScan::Setup` builds the whole plan before any of it runs, so a
wait taken while a hash iterator lays out its partitions sits under `CQScanHash::CQScanHash` - an iterator frame, but
construction.

## Where an operator ends and an event begins

A read's stack descends a hundred frames into the buffer pool. Those frames are real, and they are not the operator's
subject matter - they are the read's.

**Access barriers** mark where a unit of storage work begins. `BPool::Get` is the line: above it is the operator asking
for a page, below it is the machinery of fetching one. An operator's segment stops there, and selecting the read shows
what is underneath. Inlining it would bury the operator under the same hundred frames repeated for every page it touched.

The same line bounds an event's stack from the other side. Select a read and you get its descent, cut at the barrier
above it - so the operator's frames are not repeated in the read, and the read's frames are not repeated in the operator.
One line, two views.

## What it cannot do

**Genuinely merged siblings.** Two same-kind operators whose paths through the tree collapse to one node cannot be
separated - not by names, not by ownership, not by anything. The frames are the same frames. This is honest ambiguity and
the view shows both, which is truthful: they really do run the same code.

**Operators with no frames at all.** A Compute Scalar is often inlined and has no iterator of its own. It resolves to no
entry frame, and the row says **"no stack"** rather than borrowing its parent's bounds. That matters: rendering the
enclosing operator's segment a second time under a different name would read as that operator having done the work.
Empty is what was actually found, and the operator keeps its place in the plan, so the chain around it stays intact with
the middle link simply carrying nothing.

**Nested operators of the same kind.** The current known limitation. A view over several tables can produce six Hash
Matches nested inside each other; they all run `CQScanHash`, and the mapping cannot say which is which, so each claims
the others' frames. The fix is a third signal - the plan already says what order the operators appear in on any given
stack, so walking up from a leaf and *aligning* the frame runs against that leaf's chain of plan ancestors fixes each run
by its position rather than its name.

## Measured, not assumed

Every wrong guess about this feature came from inferring the plan backwards from a pasted call stack.

`OperatorScopeIntegrationTests` runs real queries against a live server and dumps what actually happened: which frame
each operator was entered through, whether that came from the mapping or from ownership, how many events reached it, and
what the segment then contains. It reads the answer off the plan instead of reconstructing it by eye. When a theory about
operator matching disagrees with those dumps, the theory is wrong.
