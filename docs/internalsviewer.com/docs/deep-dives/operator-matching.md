# How operator matching works

The Call Tree can show you one plan operator's calls on their own - the frames that Hash Match executed, without the
thread startup above it or the Index Scan's work below it. Doing that means answering a question SQL Server does not
answer for you: **which part of the call tree belongs to which operator?**

The Call Tree comes from a [Full Trace](/docs/deep-dives/full-trace), and this is how a Full Trace answers that question.

## Nothing tells you directly

Showplan gives the **plan** - the operators, their nesting, their costs. But the relational operators **emit no events
of their own**. Only the data-access leaves publish anything. A Stream Aggregate over an Index Scan never announces
itself. It has no reads, no locks and no waits.

The call tree gives **frames**. A frame is a function, and a function belongs to a class, not to an operator. Two Index
Seeks under a Nested Loops run identical code at identical addresses. Ask "which operator does this frame belong to?" of
the function alone and there is no answer.

What tells them apart is `this`, the object a method was called on. A call stack does not record it, but a recording
does. On x64 `this` arrives in RCX, and the replay keeps each iterator object's calls on nodes of their own. So the
question becomes two smaller ones: which object belongs to which operator, and where in the tree each object starts.

## Objects, not names

Each operator runs as an iterator object - a `CQScan*` class in row mode, `CBpQScan*` in batch mode - built from the plan
object for its node before the first row. Which plan node each iterator belongs to comes from the calls that build the
plan, not from the iterator's class - see [Operators by iterator](/docs/deep-dives/full-trace#operators-by-iterator).

That matters because a name belongs to the **class**, so it cannot tell instances apart. Six nested hash joins are six
`CQScanHash` objects, and a rule that asks which frames are "Hash Match" claims all six runs for every one of them. An
object is exactly one operator.

## Finding where an object starts

Internals Viewer walks the call tree down from each thread's root, carrying the iterator object the walk is currently inside.
A frame called on a different object from the one around it is where that object's execution begins - an **entry
frame**. Frames called on the same object further down are part of the same execution and are not entries. Frames of
functions that are not iterator methods carry no object and leave the walk where it is. Plan objects (`CXte*`,
`CBpXte*`) are passed over, since they build iterators rather than run them.

Every entry frame of every object linked to an operator's plan node becomes one of that operator's entry frames.

## A segment is two boundaries

An operator's slice of the tree is bounded by two sets of frames:

- **Entry frames** - where the operator starts executing. `CQScanHash::Open` and `CQScanHash::GetRow` for a Hash Match.
- **Exit frames** - where it hands off to something else.

Exits are **stated, not derived**: an operator's exit frames are the entry frames of the operators beneath it in the plan.
A child's entry *is* the parent's exit, so exits are read off the plan rather than inferred from the tree.

The pair is what makes a segment. Cut at the entries and you drop the preamble above. Cut at the exits and you drop the
operators nested below. What is left is what this operator did. Where the Call Tree stops at an exit it shows a link to
the nested operator, so that operator's work is a click away rather than repeated in place.

Both sets are kept on the Full Trace result as an `OperatorFrameMap`. Each entry frame is also labelled with the
operators that start there, which is where the Call Tree's target badge comes from.

## The details that turned out to matter

**An operator is rarely one frame.** A hash join builds under `Open` and probes under `GetRow`. A Sort pulls every row
during `Open`, so the scan beneath it runs under `BuildSortTable` rather than under a `GetRow`. Under parallelism the
same operator runs on several workers, as several objects. An operator having several entry frames is normal and
correct, not a symptom.

**Wrappers are looked through.** For the actual plan each iterator is wrapped in a profiling iterator
(`CQScanProfileNew`, `CQScanLightProfileNew`) that counts its rows. A wrapper is an object with calls of its own, so it
would otherwise take the operator's entry. An object whose frames the mappings name as an iterator, but never as the
start of a plan operator, is a wrapper and is skipped, so the operator is entered at the iterator inside it.

**The compiler moves code around.** With profile-guided optimisation the profiling wrapper does not call its child's
`GetRow`. It calls `CQScanNew::GetRowOrReQualifyHelper` with the child iterator as its first parameter, and the child's
`GetRow` is inlined into it. The helper is treated as an iterator method, which works because its first parameter
arrives in RCX just as `this` would. Iterator methods folded together with other functions by identical code folding
are left out, since RCX there might be anything.

**Statements have no iterator.** The statement (node -1) is not an object the plan builds. Its entry frames are found
above its nearest matched operators instead. From each of their entry frames the walk goes up to the first frame the
mappings name as that statement's start, such as `CXStmtQuery::ErsqExecuteQuery` for a `SELECT`.

## What it cannot do

**Operators with no object of their own.** A Compute Scalar is often inlined into its parent and has no iterator, and an
iterator whose methods the compiler folded away leaves no frames to find. Either way the operator resolves to no entry
frame, and the row says **"no stack"** rather than borrowing its parent's bounds. That matters: rendering the enclosing
operator's segment a second time under a different name would read as that operator having done the work. Empty is what
was actually found, and the operator keeps its place in the plan. Its work stays inside its parent's segment, which is
where it ran.

## In the source

- `src/Query/InternalsViewer.Query.FullTrace/Iterators/OperatorFrameMap.cs` - finding each object's entry frames
  and assigning them to operators and statements
- `src/Query/InternalsViewer.Query.FullTrace/Iterators/OperatorFrameMap.cs` - the entry and exit frames of each operator
- `src/Query/InternalsViewer.Query.FullTrace/Iterators/IteratorPlanLinker.cs` - linking iterator and plan objects to plan
  nodes
- `src/Query/InternalsViewer.Query.FullTrace/Symbols/Categories/Mappings/Operators.txt` - which frames name an iterator
  and which start a plan operator
- `src/Tests/InternalsViewer.Query.FullTrace.Tests/Iterators/OperatorFrameMapBuildTests.cs` - the cases above
