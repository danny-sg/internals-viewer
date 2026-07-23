# Call Stack

Enable **Call Stack** in the [Events menu](/docs/user-guide/query#events-menu) and every captured event carries the SQL Server call stack that produced it - the chain of internal engine functions that were executing at the moment the event fired. Internals Viewer combines the call stacks from every captured event into a single merged tree for the query - the **Call Tree**.

The tree decodes one row per frame:

- **Module** badge - which part of SQL Server the frame belongs to (Storage Engine, Query Processor, SQL OS, etc.)
- **Category** badge - what the frame is doing, classified by Internals Viewer (Query Operator, Row Access, Index Access, Page Access, Latching, Buffer Pool, etc.)
- **Symbol** - the function itself as `module!Class::Method`, resolved from SQL Server's debugging symbols, with the offset of the call within the function

Selecting a frame shows a small histogram of when that function was active across the query - a picture of whether it was a one-off or ran throughout.

The tree gives visibility into the engine functions behind each event. Reading down the stack for a single page read during a scan, for example: the row scanner moving to the next row, the index page manager fetching the next page, the buffer pool getting the page, and a latch suspending while the I/O completes - how the operator actually executed, not just that it read a page.

## Focus

![Call Stack pane with Focus on](/docs/user-guide/images/query-view-call-tree.png)

**Focus**, on by default, crops the tree to the event or operator currently selected - via the [Timeline](/docs/user-guide/query/Timeline), the [Execution Plan](/docs/user-guide/query/ExecutionPlan), or the Events pane. The header shows the selected event as a chip, with a **←** link to the right showing its parent (e.g. the statement it belongs to) - click it to navigate up to the parent's call stack. The **Back** and **Forward** buttons on the command bar undo/redo this navigation.

Turning Focus off shows the full call tree for the whole query, from the top-level statement execution down:

![Call Tree with Focus off](/docs/user-guide/images/call-tree-focus-off.png)

## Search and navigation

The search box filters the tree to matching frames. Right-clicking a node gives:

- **Expand All** / **Collapse All** - from that node down
- **Copy to clipboard** - copies a formatted, nested text representation of the stack from that node down

## Flame Graph

When Call Stack events are captured, the [Execution Plan](/docs/user-guide/query/ExecutionPlan) can optionally display a Flame Graph - an icicle chart of the calls per operator.

## Symbols

Turning stack frames into function names requires the debugging symbols (PDB files) for the exact SQL Server build being traced. Internals Viewer handles this automatically: the first time call stacks are processed, the required symbol files are downloaded from the Microsoft public symbol server and cached locally, and later traces resolve straight from the cache. Download progress is shown in Messages. There is nothing to install or configure - no debugging tools and no symbol server setup.

The cache location is the **Symbols Path** [setting](/docs/user-guide/settings#symbols-path). The default is `C:\Symbols`.

See [How query tracing works](/docs/deep-dives/query-tracing#resolving-call-stacks) for how the download and resolution work.
