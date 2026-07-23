# SQL Editor

The SQL Editor is where a query is written and run. The editor has SQL syntax highlighting and IntelliSense aware of the connected database's schema.

Its command bar has **Execute**, plus a set of toggles:

![SQL Editor command bar](/docs/user-guide/images/query-empty-default-layout-cropped.png)

- **Trace** - when on, data modification queries (INSERT / UPDATE / DELETE) are traced with log recording, so their [log records](/docs/user-guide/query/LogRecords) can be inspected and applied to a page. When off, data modification queries still run and roll back as normal, but no log records are captured
- **Clear Buffer Pool** - empties the buffer pool first (`DBCC DROPCLEANBUFFERS`) so every page the query touches is physically read. Don't use this on a server anyone else is using
- **Disable Read-Ahead** - makes the engine read pages individually instead of [pre-fetching large blocks](https://learn.microsoft.com/en-us/sql/relational-databases/reading-pages), giving a much clearer picture of the access pattern
- **Results** - when on, the query returns its result set, shown in the **Results** tab. When off, results are discarded - useful for cutting down noise on queries where only the storage engine activity matters
- **Messages** - shows the **Messages** tab, e.g. row counts and other output messages

While a query is running, Execute is replaced by a **Stop** button to cancel it.

::: tip
- **Ctrl + Enter** executes from the keyboard
- If text is selected in the editor, Execute runs just the selection
- **Ctrl + mouse wheel** changes the editor font size, and the size is remembered
:::

## Multi-statement queries

Only one statement can be traced at a time. Executing a query with multiple statements or `GO` batches gives the error:

> Multi-statement queries cannot be traced. Select a single statement then right click and choose 'Trace query selection'.

For scripts where the statement of interest needs setup or teardown around it - building a temp table first, say - mark just that statement: select it, right-click, and choose **Trace query selection**.

![Trace query selection on the editor's right-click menu](/docs/tutorial/images/screenshots/query-multi-statement-trace-query-selection.png)

The marked statement stays highlighted in the editor:

![The marked statement highlighted in the editor](/docs/tutorial/images/screenshots/query-multi-statement-trace-query-selected.png)

On **Execute**, everything before the marked statement runs first as untraced setup, the marked statement runs with the trace, and everything after it runs as untraced teardown - so the timeline shows only the statement of interest.

To remove the marker, right-click and choose **Clear query selection**.
