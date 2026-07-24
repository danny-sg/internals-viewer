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

# Events List

The [Events](view:Events) list is the raw data behind the timeline - every captured event in a list, in time order.
This is where to find the precise detail behind a tick on the timeline: which page a read touched, what lock mode was
taken, or what the engine was waiting on.

- The search box filters across every field, and columns sort by clicking their headers
- Multi-page reads have an expander showing the individual pages within the read
- Page addresses are links - click to open the page as a tab in this view, or **Shift + click** for a separate
  top-level tab
- The status bar at the bottom counts the events by type
- Selecting a time range on the timeline highlights the events inside it
