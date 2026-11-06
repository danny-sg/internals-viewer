# Execution Plan

The Execution Plan pane shows the captured [execution plan](https://learn.microsoft.com/en-us/sql/relational-databases/performance/execution-plans) as a tree of operators, connected to the [Timeline](/docs/user-guide/query/Timeline):

![Execution Plan with an operator selected during replay](/docs/user-guide/images/query-hash-match-build-running-cropped.png)

Each operator shows its name, object, and cost percentage, plus a small bar tracking its progress as the replay runs. Operators that consume more than one input, like a Hash Match join, label each input - **Build** and **Probe** for a hash join. See the [Showplan operators reference](https://learn.microsoft.com/en-us/sql/relational-databases/showplan-logical-and-physical-operators-reference) for what each operator does.

During replay the plan animates what is happening at the playhead - active operators highlight, rows streaming between operators are drawn as moving flow lines (thicker lines carry more rows), and operators blocked waiting on their input are marked. The plan can be zoomed with **Ctrl + mouse wheel** and panned by dragging.

For a [Full Trace](/docs/user-guide/query/FullTrace) run the plan draws each connection as the iterator calls that run along it, and follows the [Trace Timeline](/docs/user-guide/query/TraceTimeline) playhead. A dotted line points from each operator to the input it asks for rows, and a solid line points back with the response. Each input has its own pair of lines. Every line is the same width, and the costs and the streaming and blocked flow lines are left off. The dotted line turns green while a GetRow or GetNextBatch call to the input is running. When the call returns a row or a batch, the solid line turns green. Moving the playhead slowly lets each line fade back to grey, and moving it quickly switches them straight back at the next step, so the requests and responses can be seen ticking across the plan at any speed.

The operator icons light up from the [Trace Timeline](/docs/user-guide/query/TraceTimeline#operator-tracks)'s operator tracks as well. A rule with a **Plan Part** lights that part of the icon in the rule's colour while one of its calls is running, or when one of its calls returned in the playhead's last move. The predefined **Seek** track lights a seek's root, intermediate and leaf nodes as it reads each page on its way down the b-tree.

A badge at the top right of an icon counts what a track's **Counter** rules have seen on the operator up to the playhead, and its tooltip lists every count. The predefined **GetRow/GetBatch** track counts the rows or batches each operator has returned so far, and the statement at the top of the plan counts the rows returned to the client. Only the statement shows its badge to start with. **Show Counter** in an operator's right-click menu turns its badge on.

Selecting an operator (on the plan, the Timeline's Plan band, or the Events pane) highlights it and, for a [Full Trace](/docs/user-guide/query/FullTrace) run, focuses the [Call Tree](/docs/user-guide/query/CallStack) on that operator's frames.

**Double-click** an operator to select it and open its **Properties**. **Right-click** an operator for:

- **Open Index:** and the index's name - opens the index in the [Index View](/docs/user-guide/index-view), linked to the replay, or a columnstore index in the [Columnstore Viewer](/docs/user-guide/columnstore)
- **Simulate** - steps through the operator and everything beneath it in the [Simulation](/docs/user-guide/query/Simulation) pane
- **Call Tree** and **Properties** - open those panes for the operator
- **Show Counter** - for a [Full Trace](/docs/user-guide/query/FullTrace) run, shows or hides the operator's counter badge. The statement's is on to start with and every other operator's is off

## Command bar

- **Annotations** - adds badges to the operators something notable applies to. For a columnstore scan that is **Aggregate Pushdown** and **Local Aggregation**, **Compressed Filter** where a predicate was evaluated without decoding the segment, **Predicate Pushdown**, **Segment Elimination**, **Global Dictionary**, **Deep Data** for values too wide for a vector slot,, **Fast Comparison**, and **SIMD** with the instruction set it used. Elsewhere, **Row Goal**, **Row Mode** for a row mode operator over batch mode input, **Over Grant** where an operator used more memory than it was granted, and **Untranslated Predicate** where the trace cannot evaluate a predicate. The toggle is remembered between sessions
- **Properties** - opens a pane beside the plan with the selected operator's properties from the plan, grouped - **Operator**, **Optimizer**, **Storage**, **Predicates**, a group for the operator's own work such as **Hash** or **Nested Loops**, **I/O Statistics**, **Memory Grant** where it had one, **Event Statistics**, **Defined Values** and **Output Columns**. The button on its header docks it to the right or the bottom

<!-- Screenshot: Execution Plan with Annotations on and the Properties pane open -->
