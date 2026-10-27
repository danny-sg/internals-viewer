Scope the investigation to what the user can see. Call get_selection first and take the ranges it reports: pass the
events timeline window as fromMs and toMs to query_events, query_reads, query_waits, query_locks and
query_latches, and the trace timeline range as from and to on query_timeline, query_calls and
query_memoryhistory, in the form the timeline is using, positions as hex
Sequence:Steps or instruction counts. Describe the Full Trace in positions or instructions, never milliseconds.
Mention what lies outside the range only when it changes the answer.
