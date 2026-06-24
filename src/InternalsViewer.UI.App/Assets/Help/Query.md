# Query View

The Query view runs a query and then shows how SQL Server executed it — which operators
ran, which pages were read, which locks were taken, and where the time was spent. It links
the execution plan, a timeline of low-level engine events, and the live
[allocation map](Allocations.md), connecting the request to the engine's activity.

> **Note: SQL Server connections only**
> The Query view requires a live server to run and trace queries, so it is available for
> **SQL Server** connections but not for offline [data files](Connections.md). Open it
> from the **Query** button on the [Allocations](Allocations.md) command bar.

> **Image placeholder — query-overview**
> The Query view with the SQL Editor and the Timeline below it.
>
> _To be added. Source: `temp-screenshots/Query.png`; crop: trim OS title bar, keep full width._

> **Concept: tracing a query**
> Running a query here does more than return rows — it records a trace of the engine's
> activity: every page **read**, every **lock** taken, every **wait**, and each
> **execution-plan operator** as it runs. These recorded *events* drive the Timeline and
> the other panels.

## The panels

The Query view is composed of several panels that can be shown, hidden and rearranged.
Use the **View** menu to toggle any of them:

> **Image placeholder — query-view-menu**
> The View menu open, listing SQL Editor, Allocations, Execution Plan, Events, Timeline, Settings, Reset Layout.
>
> _To be added. Source: `temp-screenshots/Query_view_options.png`; crop: the open View menu._

| Panel | What it shows |
| --- | --- |
| **SQL Editor** | Where SQL is written and run. |
| **Execution Plan** | The graphical plan SQL Server used (operators, cost, data flow). |
| **Allocations** | A [live allocation map](Allocations.md) highlighting the pages the query touched. |
| **Events** | The raw list of traced engine events, with filtering. |
| **Timeline** | A time-based playback of the execution (along the bottom). |

> **Note: docking and layout**
> The panels are dockable tabs — they can be placed side by side or stacked. The
> arrangement is retained between sessions. **View ▸ Reset Layout** restores the default.

## SQL Editor

The query is written here (the editor is the Monaco editor used in VS Code, with SQL
syntax highlighting) and then run.

> **Image placeholder — query-sql-editor**
> The SQL Editor with a query and the Execute / Clear Buffer Pool / Disable Read-Ahead / Messages toolbar.
>
> _To be added. Source: `temp-screenshots/Query.png`; crop: the SQL editor pane and its toolbar._

| Control | What it does |
| --- | --- |
| **Execute** | Runs the query and traces its execution. |
| **Clear Buffer Pool** | Empties SQL Server's in-memory page cache before running, exposing the physical reads the query requires. |
| **Disable Read-Ahead** | Turns off SQL Server's read-ahead prefetching, so reads appear one page at a time on the timeline. |
| **Messages** | Shows the row count and any error messages from the run. |

> **Concept: buffer pool and read-ahead**
> SQL Server keeps recently used pages in memory (the **buffer pool**) and reads ahead of
> a scan to hide disk latency. Both improve performance but hide the underlying I/O.
> Clearing the buffer pool and disabling read-ahead cause the engine to perform the full
> work, so the timeline reflects every page read the query requires.

> **Warning:** These options affect the whole instance's cache behaviour — another reason
> to use a development server rather than production.

## Execution Plan

A graphical view of the plan SQL Server chose: each box is an **operator**, arrows show
the flow of rows between them, and each operator shows its **estimated cost** as a
percentage of the whole.

> **Image placeholder — query-plan**
> The graphical execution plan (SELECT ← Hash Match ← Index Scans with cost percentages), alongside the Allocations panel.
>
> _To be added. Source: `temp-screenshots/Query_layout_with_plan.png`; crop: the Execution Plan panel._

> **Concept: the execution plan**
> SQL is declarative — it specifies *what* is wanted, not *how* to obtain it. The query
> optimiser decides the *how*, producing an execution plan: the sequence of operations
> (scans, seeks, joins, sorts) that produces the result. Reading right to left, data flows
> from the source operators toward the final result. The cost percentages identify the
> expensive steps.

## Timeline

The timeline along the bottom plays back the execution over time (the scale is in
milliseconds).

> **Image placeholder — query-timeline**
> The expanded timeline: a Plan row with operator bars and the Read, Lock and Wait rows of event ticks below it.
>
> _To be added. Source: `temp-screenshots/Query_timeline.png`; crop: trim OS title bar, keep full width._

Rows:

- **Plan** — each execution-plan operator drawn as a bar spanning the time it was active,
  indicating which operator dominated.
- **Read** — page-read (I/O) events, one tick per page touched.
- **Lock** — locks taken during execution.
- **Wait** — waits the query incurred (time spent not running, e.g. waiting on I/O or a
  resource).

Playback controls:

- **Play / pause** and **step** buttons move a **playhead** across the timeline.
- The **speed** control (e.g. `1x`) sets playback speed.
- **Trace** controls the capture.
- As the playhead moves, the **active operator** is highlighted, linking the timeline to
  the [execution plan](#execution-plan).

> **Concept: reads, locks and waits**
> - **Reads** indicate how much data the engine moved, and whether it came from memory or
>   disk.
> - **Locks** show how the query protected the data it touched, and where it might block
>   other sessions.
> - **Waits** are time the query spent stalled. Performance problems often appear here: a
>   slow query is frequently mostly waiting.

## Allocations panel

The [allocation map](Allocations.md) from the Database view, here highlighting the pages
the running query touched. Separate layers distinguish **I/O**, **Page** and **Lock**
activity, and user objects from system objects, indicating where in the database the query
did its work.

> **Image placeholder — query-allocations**
> The Allocations panel beside the SQL/Timeline, with the touched pages highlighted on the map.
>
> _To be added. Source: `temp-screenshots/Query_layout_with_allocations.png`; crop: the Allocations panel._

## Events

The complete list of traced engine events behind the timeline. Filtering (including the
option to hide **system objects**) narrows the list. Selecting an event ties back to the
timeline and plan.

## See also

- [Allocations](Allocations.md) — the database map this view builds on.
- [Pages](Pages.md) — open any page the query read to see it decoded.
- [Indexes](Indexes.md) — examine the indexes the plan's operators used.
