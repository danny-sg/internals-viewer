# Call Tree

A [Full Trace](guide:FullTrace) records every call the query made inside SQL Server. The [Call Tree](view:CallTree)
pane merges them into a single tree with a count on each path, each frame classified with a module and category badge
and resolved to a `module!Class::Method` symbol. A query run without Full Trace has no Call Tree.

- **Focus**, on by default, crops the tree to the selected operator - Back and Forward retrace the navigation
- The search box filters the tree, and right-clicking a node gives Expand All, Collapse All, and Copy
- Selecting a frame shows a histogram of when that function was active across the query
- **Filter To** on a node crops the tree to it, and the filter button beside the search box hides or shows whole
  categories - the infrastructure categories are hidden by default

Symbols are downloaded automatically from the Microsoft public symbol server the first time a Full Trace needs them,
so the first trace takes a little longer. Progress is shown in Messages, and later traces resolve from the local cache.
