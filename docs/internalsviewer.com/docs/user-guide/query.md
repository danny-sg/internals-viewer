# Query

The Query view runs SQL against the connected database while tracing what the storage engine does - every physical page read, lock, latch, wait, and plan operator - then replays the activity on a [Timeline](/docs/user-guide/query/Timeline).

Open it with the **Query** button on the database toolbar.

## Running a query

Enter SQL in the editor and press **Execute**. The query runs with a trace session, and when it completes the captured activity loads into the timeline.

The editor's command bar has toggles for tracing options and result display - see [SQL Editor](/docs/user-guide/query/Editor) for the full set, and [Multi-statement queries](/docs/user-guide/query/Editor#multi-statement-queries) for tracing a single statement out of a larger script.

::: warning
Data modification queries (INSERT / UPDATE / DELETE) are run inside a transaction that is rolled back after the trace is captured, so the data is left unchanged. See [Log Records](/docs/user-guide/query/LogRecords) for how this works and what it captures.
:::

## Events menu

The **Events** menu selects what the trace captures:

![Events menu](/docs/user-guide/images/query-events-menu.png)

Page I/O is always captured. **Locks** opens a submenu of lock categories to capture (**Read**, **Update**, **Write**, **Schema**, **Range**, **Bulk**, or **None**/**Default**) - by default this excludes **Schema** locks, since they are held for a large part of the query's lifetime and would otherwise dominate the [Locks](/docs/user-guide/query/Locks) band. **Waits** and **Latches** can be toggled independently, as can **Columnstore** (segment scans, rowgroup reads and elimination, object pool lookups, batch and bitmap filters, and aggregate pushdown - on by default, see [Columnstore](/docs/user-guide/query/Timeline#columnstore)), **Memory** (grants, spills, and sort warnings) and **Call Stack** - see [Call Stack](/docs/user-guide/query/CallStack).

## Query menu

![Query menu](/docs/user-guide/images/query-options-menu.png)

- **Crop to query** - on by default. Limits the captured trace to the statement being run, rather than everything happening on the connection
- **Include System Objects** - includes system tables and indexes in captured events, normally filtered out

## Index menu

**Open all indexes** opens an [Index View](/docs/user-guide/index-view) pane for every index the plan reads - the seeks, scans and lookups - each linked to the replay, so a join's indexes can be watched side by side as it runs.

## Views

The **View** menu opens additional panes:

![View menu](/docs/user-guide/images/query-view-menu.png)

- **SQL Editor** - the query [editor](/docs/user-guide/query/Editor)
- **Allocations** - the allocation map, scoped to the query - see [Allocations](/docs/user-guide/query/Allocations)
- **Execution Plan** - the captured plan, connected to the timeline - see [Execution Plan](/docs/user-guide/query/ExecutionPlan)
- **Events** - the raw list of captured events behind the timeline - see [Events](/docs/user-guide/query/Events)
- **Call Stack** - the decoded SQL Server call stack for the current event - see [Call Stack](/docs/user-guide/query/CallStack)
- **Timeline** - the replay timeline - see [Timeline](/docs/user-guide/query/Timeline)
- **Flame Chart** - every call recorded by **Record Full Trace** on the Debugger menu, which takes the Timeline's place while it is on - see [Flame Chart](/docs/user-guide/query/FlameChart)
- **Reset Layout** - restores the default pane arrangement
- **Instructions** - a quick reference for the view

Pages and indexes opened from the trace - by double-clicking a timeline event, clicking a page link in the Events pane, or right-clicking an operator - also open as panes, so everything about the query stays in one tab.

## Debugger menu

**Attach WinDbg to SQL Server**, **Connect to session** and **Detach** manage a WinDbg session that the Call Stack pane can send commands to - see [Sending Commands to WinDbg](/docs/user-guide/query/CallStack#sending-commands-to-windbg) - and **Search symbols** is covered in [Searching Symbols](/docs/user-guide/query/CallStack#searching-symbols).

**Record Full Trace** records each run with Microsoft's Time Travel Debugging while it is on, so the [Call Stack](/docs/user-guide/query/CallStack) holds every call the query made rather than the stacks sampled at each event - see [Full Trace](/docs/user-guide/query/FullTrace). It is for development and test instances only: it needs administrator consent, can restart the SQL Server service, slows the recorded query down considerably and writes very large files. The dialog shown when it is turned on explains the requirements, and the **Time Travel Warning** [setting](/docs/user-guide/settings#time-travel-warning) brings the dialog back once it has been dismissed. While it is on, a red **Full Trace** badge sits on the SQL Editor's tab strip - click it to turn recording off again. **Record Extended Events** adds the normal event set to the recording, at the cost of a larger trace - see [Extended Events in a recording](/docs/user-guide/query/FullTrace#extended-events-in-a-recording).

## Layout

Panes are tabs that can be dragged into any layout - drop a tab beside or below another pane to split the space, or onto a pane to stack them. While dragging, the drop zones highlight to show where the tab will land.

The **Details** and **Timeline** buttons on the top right show and hide the two halves of the view - the pane area and the timeline - with a splitter between them to adjust the balance.

The layout is remembered - the pane arrangement, timeline visibility, and the Query and Events menu options are all restored the next time a Query tab is opened. **Reset Layout** on the View menu puts everything back to the default.

::: details How this works
The query runs with an Extended Events session filtered to the connection, capturing page reads, locks, waits, per-operator profiles, and the execution plan. The events are matched to plan operators to build the timeline.

See [How query tracing works](/docs/deep-dives/query-tracing) for the details.
:::

## Next steps

For a walkthrough, the tutorial's [Query section](/docs/tutorial/query/1-using-the-query-view) traces queries against a sample database - including [scans vs seeks](/docs/tutorial/query/4-scans-vs-seeks) and the [three physical join operators](/docs/tutorial/query/6-joins).
