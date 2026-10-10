# Execution Plan

The Execution Plan pane shows the captured [execution plan](https://learn.microsoft.com/en-us/sql/relational-databases/performance/execution-plans) as a tree of operators, connected to the [Timeline](/docs/user-guide/query/Timeline):

![Execution Plan with an operator selected during replay](/docs/user-guide/images/query-hash-match-build-running-cropped.png)

Each operator shows its name, object, and cost percentage, plus a small bar tracking its progress as the replay runs. Operators that consume more than one input, like a Hash Match join, label each input - **Build** and **Probe** for a hash join. See the [Showplan operators reference](https://learn.microsoft.com/en-us/sql/relational-databases/showplan-logical-and-physical-operators-reference) for what each operator does.

During replay the plan animates what is happening at the playhead - active operators highlight, rows streaming between operators are drawn as moving flow lines (thicker lines carry more rows), and operators blocked waiting on their input are marked. The plan can be zoomed with **Ctrl + mouse wheel** and panned by dragging.

Selecting an operator (on the plan, the Timeline's Plan band, or the Events pane) highlights it and, if [Call Stack](/docs/user-guide/query/CallStack) events were captured, focuses the Call Stack pane on that operator's frames.

**Double-click** an operator to select it and open its **Properties**. **Right-click** an operator for:

- **Open Index:** and the index's name - opens the index in the [Index View](/docs/user-guide/index-view), linked to the replay, or a columnstore index in the [Columnstore Viewer](/docs/user-guide/columnstore)
- **Trace** - steps through the operator and everything beneath it in the [Trace](/docs/user-guide/query/Trace) pane
- **Call Stack** and **Properties** - open those panes for the operator

## Command bar

- **Flame Graph** - breaks each operator down into the calls that made it up, see [below](#flame-graph). Available when **Call Stack** was captured, and on by default
- **Annotations** - adds badges to the operators something notable applies to. For a columnstore scan that is **Aggregate Pushdown** and **Local Aggregation**, **Compressed Filter** where a predicate was evaluated without decoding the segment, **Predicate Pushdown**, **Segment Elimination**, **Global Dictionary**, **Deep Data** for values too wide for a vector slot,, **Fast Comparison**, and **SIMD** with the instruction set it used. Elsewhere, **Row Goal**, **Row Mode** for a row mode operator over batch mode input, **Over Grant** where an operator used more memory than it was granted, and **Untranslated Predicate** where the trace cannot evaluate a predicate. The toggle is remembered between sessions
- **Properties** - opens a pane beside the plan with the selected operator's properties from the plan, grouped - **Operator**, **Optimizer**, **Storage**, **Predicates**, a group for the operator's own work such as **Hash** or **Nested Loops**, **I/O Statistics**, **Memory Grant** where it had one, **Event Statistics**, **Defined Values** and **Output Columns**. The button on its header docks it to the right or the bottom. For a run recorded with [Full Trace](/docs/user-guide/query/FullTrace) a **Traced Memory** group is added: the bytes the operator **Allocated** and **Freed**, its **Peak In Use**, anything **Still Held** at the end of the query, and the allocations broken down **By Memory Clerk** and by the function they were **Allocated By**

<!-- Screenshot: Execution Plan with Annotations on and the Properties pane open -->

## Flame Graph

When [Call Stack](/docs/user-guide/query/CallStack) events are captured, the **Flame Graph** toggle on the command bar replaces each operator's progress bar with an icicle chart of the calls made by that operator, coloured by the same category colours as the Call Stack pane - showing where an operator actually spent its time, not just its overall cost:

![Execution Plan with Flame Graph enabled](/docs/user-guide/images/query-view-execution-plan-with-flamegraph.png)
