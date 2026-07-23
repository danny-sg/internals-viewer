# SQL Editor

The [SQL Editor](view:SqlEditor) is where a query is written and run. It has SQL syntax highlighting and IntelliSense
aware of the connected database's schema.

The command bar toggles change how the next query is traced:

- **Trace** - capture the transaction log records of data modifications, so they can be replayed on a page - see
  [Log Records](guide:LogRecords)
- **Clear Buffer Pool** - empty the buffer pool first, so every page the query touches is physically read. Avoid on a
  server anyone else is using
- **Disable Read-Ahead** - read pages individually instead of pre-fetching large blocks, for a clearer access pattern
- **Results** / **Messages** - show or discard the query's result set, and show the messages output

Tips:

- **Ctrl + Enter** executes from the keyboard
- If text is selected, Execute runs just the selection
- **Ctrl + mouse wheel** changes the editor font size
- Data modifications (INSERT / UPDATE / DELETE) run inside a transaction that is rolled back after the trace, so the
  data is left unchanged

## Multi-statement scripts

Only one statement can be traced at a time. To trace one statement out of a larger script, select it, right-click, and
choose **Trace query selection** - everything before it runs as untraced setup, and everything after as untraced
teardown.
