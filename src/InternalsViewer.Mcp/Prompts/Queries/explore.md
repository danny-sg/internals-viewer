Explore the query run open in Internals Viewer. {{focus}}
{{scopeInstructions}}
Explain how SQL Server executed it: the mechanism, not a tuning review. Read the reading guide first (the guide tool
with topic reading), then work from the evidence top-down: list_tabs, get_selection to see what the user is looking
at, then query_summary for what the query asks, query_plan for the operators in execution order and what each is asked
to do, query_operators (start, first row, threads), then what each operator did: query_reads for the pages it read
(disk against buffer pool, read-ahead, extents) and query_calltree with operator for the engine functions behind it.
For a Full Trace, read the full-trace-investigations guide and go on down to the calls, arguments and memory with
query_calls, query_memory and query_memoryhistory. Stop at the depth the question asks and offer the next step down.
Lead with the answer, then the evidence with the ids the tools print, and open the key places in the app with the
show_ tools as you explain them.
For a Full Trace, end by offering to walk the user through what you found on the Trace Timeline. If they accept, read
the trace-timeline guide and show your findings as the steps of one trace_walkthrough, in the order you explained them.
