# Query view

The Query view runs SQL against the connected database while tracing what the storage engine does - every page read,
lock, latch, and wait - then replays the activity on the timeline.

## First trace

1. Enter a query in the [SQL Editor](view:SqlEditor) and press **Execute**
2. When it completes, press play on the timeline - or drag the red playhead to scrub through the trace
3. Click any event on the timeline to see its detail in the [Events](view:Events) pane

Turn on **Clear Buffer Pool** and **Disable Read-Ahead** on the editor's command bar to make every page the query
touches show up as a physical read.

## Watching the replay

- Open the [Allocations](view:Allocations) pane to watch pages light up on the allocation map as they are read
- Open the [Execution Plan](view:ExecutionPlan) pane to see rows flow between operators during the replay
- Right-click a scan or seek on the timeline's Plan band to open its index, linked to the replay

## Going deeper

- [Choose which events to capture](guide:ChoosingEvents)
- The full guide is at [internalsviewer.com](https://internalsviewer.com)
