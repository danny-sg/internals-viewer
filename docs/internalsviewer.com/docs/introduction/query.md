# Query

The Query view runs SQL against the connected database while tracing what the storage engine does - every physical page read, lock, wait, and plan operator - then replays the activity on a timeline.

Open it with the **Query** button on the database toolbar.

## Running a query

Enter SQL in the editor and press **Execute**. The query runs with a trace session, and when it completes the captured activity loads into the timeline. **Messages** shows the output messages, e.g. row counts.

Two toolbar options make the storage engine activity more visible:

- **Clear Buffer Pool** - empties the buffer pool first (`DBCC DROPCLEANBUFFERS`) so every page the query touches is physically read. Don't use this on a server anyone else is using
- **Disable Read-Ahead** - makes the engine read pages individually instead of pre-fetching large blocks, giving a much clearer picture of the access pattern

The **Events** dropdown selects what the trace captures. **Locks** and **Waits** can be toggled off to cut noise, and **Callstack** captures the SQL Server call stack for each event - see [Call stacks](#call-stacks) below.

::: warning
Data modification queries (INSERT / UPDATE / DELETE) are run inside a transaction that is rolled back after the trace is captured, so the data is left unchanged.
:::

## The timeline

The timeline shows the captured activity against time, in lanes:

- **Plan** - the execution plan operators, one bar per operator showing when it was active
- **Read** - physical page reads
- **Lock** - locks acquired and released
- **Wait** - waits, where the query had to stop and wait for a resource

The playback controls replay the query like a recording - play, step, and speed - and the red playhead can be dragged to scrub through the trace.

- **Click an operator's bar** to select it and highlight when it actually streamed rows - blocking operators like a Sort consume their input for most of their lifetime and only stream at the end
- **Right-click an index** in the Plan lane to open it in the [Index Viewer](/docs/introduction/index-viewer), linked to the trace so pages light up as they are read

## Views

The **View** menu opens additional panes:

- **SQL Editor** - the query editor
- **Allocations** - the allocation map, highlighting pages as they are read during replay
- **Execution Plan** - the captured plan, connected to the timeline to show where data is streaming and where an operator is blocked
- **Events** - the raw list of captured events behind the timeline
- **Call Stack** - the decoded SQL Server call stack for the current event, when the Callstack event is enabled
- **Timeline** - the replay timeline
- **Settings** - trace options

Panes are tabs that can be dragged into any layout - drop a tab beside or below another pane to split the space, or onto a pane to stack them. **Reset Layout** restores the default.

::: details How this works
The query runs with an Extended Events session filtered to the connection, capturing page reads, locks, waits, per-operator profiles, and the execution plan. The events are matched to plan operators to build the timeline.

See [How query tracing works](/docs/deep-dives/query-tracing) for the details.
:::

## Call stacks

Enable **Callstack** in the Events dropdown and every captured event carries the SQL Server call stack that produced it - the chain of internal engine functions that were executing at the moment the event fired.

![Event options with the Call Stack pane](/docs/tutorial/images/screenshots/Query_event_options.png)

The **Call Stack** pane decodes the stack one row per frame:

- **Module** and **Category** - classifications derived by Internals Viewer: which part of the engine the frame belongs to (Storage Engine, Query Processor, SQL OS...) and what it is doing (Index Access, Row Access, Buffer Pool, Latching...)
- **Symbol** - the function itself as `module!Class::Method`, resolved from SQL Server's debugging symbols, with the offset into the function

This gives visibility into the engine functions behind each event. Reading down the stack for a single page read during a scan, for example: the row scanner moving to the next row, the index page manager fetching the next page, the buffer pool getting the page, and a latch suspending while the I/O completes - how the operator actually executed, not just that it read a page.

### Symbols

Turning stack frames into function names requires the debugging symbols (PDB files) for the exact SQL Server build being traced. Internals Viewer handles this automatically: the first time call stacks are processed, the required symbol files are downloaded from the Microsoft public symbol server and cached locally, and later traces resolve straight from the cache. Download progress is shown in Messages. There is nothing to install or configure - no debugging tools and no symbol server setup.

The cache location is the **Symbols Path** setting, in **Settings** on the start page. The default is `C:\Symbols`.

![Settings](/docs/tutorial/images/screenshots/Settings.png)

See [How query tracing works](/docs/deep-dives/query-tracing#resolving-call-stacks) for how the download and resolution work.

For a walkthrough, the tutorial's [Query section](/docs/tutorial/query/1-using-the-query-view) traces queries against a sample database - including [scans vs seeks](/docs/tutorial/query/4-scans-vs-seeks) and the [three join types](/docs/tutorial/query/6-joins).
