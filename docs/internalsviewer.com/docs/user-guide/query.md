# Query

The Query view runs SQL against the connected database while tracing what the storage engine does - every physical page read, lock, latch, wait, and plan operator - then replays the activity on a [Timeline](/docs/user-guide/query/Timeline).

Open it with the **Query** button on the database toolbar, or the **Query** link beside a SQL Server entry in the Start page's recent connections. It needs a live SQL Server connection.

## Running a query

Enter SQL in the editor and press **Execute**. With **Track query** on, as it is by default, the query runs with a trace session, and when it completes the captured activity loads into the timeline. The first run in a new Query tab also opens the **Allocations**, **Execution Plan** and **Events** panes.

The editor's command bar has the tracing options, and its tab strip the result panes - see [SQL Editor](/docs/user-guide/query/Editor) for the full set, and [Multi-statement queries](/docs/user-guide/query/Editor#multi-statement-queries) for tracing a single statement out of a larger script.

::: warning
Data modification queries (INSERT / UPDATE / DELETE) are run inside a transaction that is rolled back after the trace is captured, so the data is left unchanged. See [Log Records](/docs/user-guide/query/LogRecords) for how this works and what it captures. This only happens while **Track query** is on - with it off, a modification runs normally and changes the data.
:::

## Events menu

The **Events** menu selects what the trace captures:

![Events menu](/docs/user-guide/images/query-events-menu.png)

<!-- Screenshot (update): the Events menu - it now has Columnstore, and the menu bar has Index and Debugger -->

Page I/O is always captured. **Locks** opens a submenu of lock categories to capture (**Read**, **Update**, **Write**, **Schema**, **Range**, **Bulk**, or **None**/**Default**) - by default this excludes **Schema** locks, since they are held for a large part of the query's lifetime and would otherwise dominate the [Locks](/docs/user-guide/query/Locks) band. **Waits** and **Latches** can be toggled independently, as can **Columnstore** (segment scans, rowgroup reads and elimination, object pool lookups, batch and bitmap filters, and aggregate pushdown - on by default, see [Columnstore](/docs/user-guide/query/Timeline#columnstore)), **Memory** (grants, spills, and sort warnings, listed in the Events pane) and **Call Stack** - see [Call Stack](/docs/user-guide/query/CallStack).

By default **Waits**, **Columnstore** and **Call Stack** are on, and **Latches** and **Memory** off.

The lock categories, **Waits** and **Latches** also filter what is already loaded - turning one off hides its events and its band straight away, without running the query again. **Columnstore**, **Memory** and **Call Stack** change what the next run captures.

## Query menu

![Query menu](/docs/user-guide/images/query-options-menu.png)

<!-- Screenshot (update): the Query menu - Track query is now its first item -->

- **Track query** - on by default. Runs the query with a trace session, so its events, plan and log records are captured. With it off the query simply runs - see [Track query](/docs/user-guide/query/Editor#track-query)
- **Crop to query** - on by default. Limits the captured trace to the statement being run, rather than everything happening on the connection
- **Include system objects** - includes system tables and indexes in captured events, normally filtered out. It also filters what is already loaded

## Index menu

**Open all indexes** opens an [Index View](/docs/user-guide/index-view) pane for every rowstore index the plan seeks or scans, each linked to the replay, so a join's indexes can be watched side by side as it runs. A columnstore index is opened on its own from the plan or the timeline - see [Columnstore Viewer](/docs/user-guide/columnstore).

## Views

The **View** menu opens additional panes:

![View menu](/docs/user-guide/images/query-view-menu.png)

<!-- Screenshot (update): the View menu - it now has Flame Chart and Trace -->

- **SQL Editor** - the query [editor](/docs/user-guide/query/Editor)
- **Allocations** - the allocation map, scoped to the query - see [Allocations](/docs/user-guide/query/Allocations)
- **Execution Plan** - the captured plan, connected to the timeline - see [Execution Plan](/docs/user-guide/query/ExecutionPlan)
- **Events** - the raw list of captured events behind the timeline - see [Events](/docs/user-guide/query/Events)
- **Call Stack** - the decoded SQL Server call stack for the current event - see [Call Stack](/docs/user-guide/query/CallStack)
- **Timeline** - the replay timeline - see [Timeline](/docs/user-guide/query/Timeline)
- **Flame Chart** - every call recorded by **Record Full Trace** on the Debugger menu, which takes the Timeline's place while it is on - see [Flame Chart](/docs/user-guide/query/FlameChart)
- **Trace** - steps through the query's plan operator by operator - see [Trace](/docs/user-guide/query/Trace)
- **Reset Layout** - restores the default pane arrangement
- **Instructions** - a quick reference for the view, with links that open panes and switch capture options

Pages and indexes opened from the trace - by double-clicking a timeline event, clicking a page link in the Events pane, or right-clicking an operator - also open as panes, so everything about the query stays in one tab.

## Debugger menu

**Attach WinDbg to SQL Server**, **Connect to session** and **Detach** manage a WinDbg session that the Call Stack pane can send commands to - see [Sending Commands to WinDbg](/docs/user-guide/query/CallStack#sending-commands-to-windbg) - and **Search symbols** is covered in [Searching Symbols](/docs/user-guide/query/CallStack#searching-symbols).

**Record Full Trace** records each run with Microsoft's Time Travel Debugging while it is on, so the [Call Stack](/docs/user-guide/query/CallStack) holds every call the query made rather than the stacks sampled at each event - see [Full Trace](/docs/user-guide/query/FullTrace). It is for development and test instances only: it needs administrator consent, can restart the SQL Server service, slows the recorded query down considerably and writes very large files. The dialog shown when it is turned on explains the requirements, and the **Time Travel Warning** [setting](/docs/user-guide/settings#time-travel-warning) brings the dialog back once it has been dismissed. While it is on, a red **Full Trace** badge sits on the SQL Editor's tab strip - click it to turn recording off again. **Record Extended Events** adds the normal event set to the recording, at the cost of a larger trace - see [Extended Events in a recording](/docs/user-guide/query/FullTrace#extended-events-in-a-recording).

## Layout

Panes are tabs that can be dragged into any layout - drop a tab beside or below another pane to split the space, or onto a pane to stack them. While dragging, the drop zones highlight to show where the tab will land. The default layout is the SQL Editor above the Timeline.

The layout is remembered - the pane arrangement, timeline visibility, and the Events menu options, **Crop to query** and **Include system objects** are all restored the next time a Query tab is opened. **Track query** and **Record Full Trace** are not, and neither are the Index, Page, Columnstore and Trace panes opened from a query. **Reset Layout** on the View menu puts everything back to the default.

::: details How this works
The query runs with an Extended Events session filtered to the connection, capturing page reads, locks, waits, per-operator profiles, and the execution plan. The events are matched to plan operators to build the timeline.

See [How query tracing works](/docs/deep-dives/query-tracing) for the details.
:::

## Next steps

For a walkthrough, the tutorial's [Query section](/docs/tutorial/query/1-using-the-query-view) traces queries against a sample database - including [scans vs seeks](/docs/tutorial/query/4-scans-vs-seeks) and the [three physical join operators](/docs/tutorial/query/6-joins).
