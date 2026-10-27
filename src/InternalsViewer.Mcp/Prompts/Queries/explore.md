Explore the query run open in Internals Viewer. {{focus}}
{{scopeInstructions}}
Read the reading guide first (the guide tool with topic reading), then work from the evidence: list_tabs,
get_selection to see what the user is looking at, then query_summary, query_plan (estimated against actual rows,
warnings, the memory grant), query_reads (disk against buffer pool, read-ahead, extents), query_waits,
query_operators (start, first row, duration, threads) and the call tree of the operators that matter. For a Full
Trace, read the full-trace-investigations guide and go down to the functions, arguments and memory with
query_calltree, query_calls, query_memory and query_memoryhistory. Lead with the answer, then the evidence with the
ids the tools print, and open the key places in the app with the show_ tools as you explain them.
