# Call Stack

Capture [Call Stack](option:IncludeCallStack) events and every event carries the chain of internal SQL Server
functions that produced it. The [Call Stack](view:Callstack) pane merges them into a single tree for the query, each
frame classified with a module and category badge and resolved to a `module!Class::Method` symbol.

- **Focus**, on by default, crops the tree to the selected event or operator - Back and Forward retrace the navigation
- The search box filters the tree, and right-clicking a node gives Expand All, Collapse All, and Copy
- Selecting a frame shows a histogram of when that function was active across the query

Symbols are downloaded automatically from the Microsoft public symbol server the first time call stacks are processed,
so the first trace takes a little longer. Progress is shown in Messages, and later traces resolve from the local cache.
