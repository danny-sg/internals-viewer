# Choosing events

Page reads are always captured. Everything else is opt-in - each event type adds detail to the timeline, and some add
overhead to the trace. Tick an item to change what the next trace captures:

- [{{ShowWaits}}] [Waits](option:ShowWaits) - where the query stopped and waited for a resource
- [{{ShowLatches}}] [Latches](option:ShowLatches) - the page-level protection around every read and modification.
  Held very briefly and in large numbers, so capturing them adds overhead
- [{{IncludeMemory}}] [Memory](option:IncludeMemory) - memory grants, hash spills, and sort warnings
- [{{IncludeCallStack}}] [Call Stack](option:IncludeCallStack) - the SQL Server call stack behind every event. The
  first trace downloads the server's debugging symbols, so it takes a little longer

Locks have their own submenu on the **Events** menu, choosing which lock categories to capture. Schema locks are
excluded by default - they span most of the query, so they would dominate the Lock band.

Options apply to the next **Execute**, so change them, run the query again, and compare.

[Back to overview](guide:Overview)
