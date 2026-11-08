# Execution Plan

The Execution Plan pane shows the captured [execution plan](https://learn.microsoft.com/en-us/sql/relational-databases/performance/execution-plans) as a tree of operators, connected to the [Timeline](/docs/user-guide/query/Timeline):

![Execution Plan with an operator selected during replay](/docs/user-guide/images/query-hash-match-build-running-cropped.png)

Each operator shows its name, object, and cost percentage, plus a small bar tracking its progress as the replay runs. Operators that consume more than one input, like a Hash Match join, label each input - **Build** and **Probe** for a hash join. See the [Showplan operators reference](https://learn.microsoft.com/en-us/sql/relational-databases/showplan-logical-and-physical-operators-reference) for what each operator does.

During replay the plan animates what is happening at the playhead - active operators highlight, rows streaming between operators are drawn as moving flow lines (thicker lines carry more rows), and operators blocked waiting on their input are marked. The plan can be zoomed with **Ctrl + mouse wheel** and panned by dragging.

For a [Full Trace](/docs/user-guide/query/FullTrace) run the plan draws each connection as the iterator calls that run along it, and follows the [Trace Timeline](/docs/user-guide/query/TraceTimeline) playhead. A dotted line points from each operator to the input it asks for rows, and a solid line points back with the response. Each input has its own pair of lines, hidden until a call lights them. Every line is the same width, and the costs and the streaming and blocked flow lines are left off. The dotted line shows in green while a GetRow or GetNextBatch call to the input is running. When the call returns a row or a batch, the solid line shows in green. When the playhead moves on, a line fades away over a time that follows how often it lights. A line that lit a while ago fades slowly, and one that lights again straight away switches off at once, so a fade never runs into the next one and the requests and responses can be seen ticking across the plan at any speed. The lit parts of the operator icons fade the same way.

The operator icons light up from the [Trace Timeline](/docs/user-guide/query/TraceTimeline#operator-tracks)'s operator tracks as well. A rule with a **Plan Part** lights that part of the icon in the rule's colour while one of its calls is running, or when one of its calls returned in the playhead's last move. The predefined **Seek** track lights a seek's root, intermediate and leaf nodes as it reads each page on its way down the b-tree.

Each operator's icon is grey until the **Open** track's tick shows it in colour, from the operator's Open call until it is closed. With the Open track turned off, the icons keep their colours. A scan, seek or lookup has a page beside its icon. The page shows in grey while a GetRow call to the operator is running, and flashes blue when the operator gets a page from the buffer pool. Each row the operator reads draws a line across the page where the row's slot sits, near the top for the first slot and near the bottom for the last. An IAM page shows an **I** and a PFS page a **P**, in the colours of the **Reads** track's **IAM Read** and **PFS Read** ticks.

Under the operator's name, the last page it got from the buffer pool up to the playhead shows as a link, such as `(1:200)`. Once the operator reads a row on that page, the link shows the row instead, as File Id:Page Id:Slot, such as `(1:200:5)`. Click it to open the page, at the row's slot. The link stays the same size as the plan zooms, and is hidden when the plan is zoomed out so far that the operator names are too small to read.

A badge at the top right of an icon counts what a track's **Counter** rules have seen on the operator up to the playhead, and its tooltip lists every count. The predefined **GetRow/GetBatch** track counts the rows or batches each operator has returned so far, and the statement at the top of the plan counts the rows returned to the client. Only the statement shows its badge to start with. **Show Counter** in an operator's right-click menu turns its badge on.

Selecting an operator (on the plan, the Timeline's Plan band, or the Events pane) outlines its icon and, for a [Full Trace](/docs/user-guide/query/FullTrace) run, focuses the [Call Tree](/docs/user-guide/query/CallStack) on that operator's frames. A Full Trace run leaves the outline off, so it does not compete with the parts of the icon the playhead lights.

**Double-click** an operator to select it and open its **Properties**. **Right-click** an operator for:

- **Open Index:** and the index's name - opens the index in the [Index View](/docs/user-guide/index-view), linked to the replay, or a columnstore index in the [Columnstore Viewer](/docs/user-guide/columnstore)
- **Simulate** - steps through the operator and everything beneath it in the [Simulation](/docs/user-guide/query/Simulation) pane
- **Call Tree** and **Properties** - open those panes for the operator
- **Show Counter** - for a [Full Trace](/docs/user-guide/query/FullTrace) run, shows or hides the operator's counter badge. The statement's is on to start with and every other operator's is off

## Guide Mode

**Guide Mode**, on the [Trace menu](/docs/user-guide/query#trace-menu), walks through a [Full Trace](/docs/user-guide/query/FullTrace) run on the Execution Plan. Turning it on also turns on **Record Full Trace**, with the same dialogs, and sets the layout to the SQL Editor, the Trace Timeline and the Execution Plan as tabs, with the plan showing, zoomed to fit the pane. The Trace Timeline shows only its **Operators** pane. The layout stays while it is on, run after run, and turning it off puts the previous layout and the Trace Timeline's panes back.

The step buttons on the Execution Plan's tab, and **Previous** and **Next** at the bottom of the panel under the plan, move the playhead to the previous or next step. Every tick on the [operator tracks](/docs/user-guide/query/TraceTimeline#operator-tracks) that are turned on is a step, at the start of its call. A rule with a return description adds a second step at the end of each call, so a GetRow call is two steps, such as "Top calls `GetRow` from Table Scan" and then "`GetRow` returns a row from Table Scan to Top". The plan lights up as it would at that point on the Trace Timeline. A panel under the plan has a header with the step's operator icon and name, and beneath it the rule's title in bold and its description, or its return description for a return, both as Markdown. Code in the text, such as `GetRow`, is drawn like the call's block on the Trace Timeline, in the colour of its category. Drag the line above the panel to resize it. A slider along the top of the plan shows how far through the steps the current one is, and dragging it moves through them.

While Guide Mode is on, **Play**, **Previous** and **Next** on the Query tab's command bar step through the same steps, with or without the Trace Timeline open, and the Trace Timeline's step menu is set to **Tracks**. **Play** holds on each step long enough to read the plan, for less time at a faster playback speed. Turning Guide Mode off puts the step menu back.

A rule marked as a **Phase**, such as the **Hash Table** track's **Build**, is stepped over. Next from the start of a phase goes to its end, and Previous from after it goes back to its start. The steps inside are still in the Steps pane and on the slider.

Describe the steps in the Operator Tracks tab, such as the predefined **Row Access** rule's `Reads row data at slot {Slot} on page {Page}`.

**Steps**, beside the step buttons, opens a pane to the right of the plan listing every step the query goes through, in order. Each row has the step's **Position** on the Trace Timeline, the **Operator** it happened in, which for a GetRow call is the operator that made the call and for its return the operator that returned, its **Name**, from the rule's title, and its **Description**, from the rule's description, or its return description for a return. Select a row to move the playhead to that step. The row follows Next and Previous as they move.

## Command bar

- **Annotations** - adds badges to the operators something notable applies to. For a columnstore scan that is **Aggregate Pushdown** and **Local Aggregation**, **Compressed Filter** where a predicate was evaluated without decoding the segment, **Predicate Pushdown**, **Segment Elimination**, **Global Dictionary**, **Deep Data** for values too wide for a vector slot,, **Fast Comparison**, and **SIMD** with the instruction set it used. Elsewhere, **Row Goal**, **Row Mode** for a row mode operator over batch mode input, **Over Grant** where an operator used more memory than it was granted, and **Untranslated Predicate** where the trace cannot evaluate a predicate. The toggle is remembered between sessions
- **Properties** - opens a pane beside the plan with the selected operator's properties from the plan, grouped - **Operator**, **Optimizer**, **Storage**, **Predicates**, a group for the operator's own work such as **Hash** or **Nested Loops**, **I/O Statistics**, **Memory Grant** where it had one, **Event Statistics**, **Defined Values** and **Output Columns**. The button on its header docks it to the right or the bottom

<!-- Screenshot: Execution Plan with Annotations on and the Properties pane open -->
