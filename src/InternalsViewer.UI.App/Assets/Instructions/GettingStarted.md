# Getting Started

Query tracing allows you to run a SQL query and trace the individual engine events that occured and ran the query.

This includes page reads (disk and buffer pool), locks, latches, waits, and log records if the query is a modification query. You can also 
capture the call stack and execution plan.

Internals Viewer takes the raw captures and puts them together in a consolidated view that connects all of the information and allows you 
to step through the query as it actually executed.

To run a query trace:

- Select the events to capture in the _Events_ menu
- Open the [SQL Editor](view:SqlEditor) and enter a query
- Click **Execute**

Events will be displayed on the [Timeline](view:Timeline). You can press the Play button or move the playhead to move to different points 
on the timeline and view different visualization tabs for the query execution including:

- [Allocations](view:Allocations)
- [Execution Plan](view:ExecutionPlan)
- [Call Stack](view:CallStack)
- Indexes

Tabs can be grouped into different layouts. Drag the tab headers to dock and group.

> **Production Warning**
>
> A single query has the potential to generate thousands, or hundreds of thousands of events. Factors include the complexity of the query, 
> the number of rows it accesses, and the duration for the query.
>
> It is not recommended to run query tracing on a production database. There is a performance overhead to capturing events and captures 
> may include clearing the buffer pool (`DBCC DROPCLEANBUFFERS`) and running checkpoints (`CHECKPOINT`).