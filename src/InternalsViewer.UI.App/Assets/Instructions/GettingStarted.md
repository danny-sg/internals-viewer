# Getting started

The Query view runs SQL against the connected database while tracing what the storage engine does - every page read,
lock, latch, and wait - then replays the activity on the timeline.

Follow the steps to trace your first query:

## 1 - Write a query

Open the [SQL Editor](view:SqlEditor) and enter a query against a table in the database. A simple `SELECT` against a
reasonably sized table works well - the more pages it reads, the more there is to watch.

## 2 - Make the reads visible

On the editor's command bar turn on **Clear Buffer Pool**, so every page has to be physically read instead of being
served from memory, and **Disable Read-Ahead**, so pages are read one at a time as the engine needs them.

## 3 - Execute

Press **Execute** (or **Ctrl + Enter**). The query runs with a trace session attached, and when it completes the
captured activity loads into the timeline at the bottom.

## 4 - Replay it

Press play on the timeline, or drag the red playhead to scrub through the trace. Each tick on the Read band is a page
being read.

- Open the [Allocations](view:Allocations) pane and replay again - pages light up on the allocation map as they are
  read
- Open the [Execution Plan](view:ExecutionPlan) pane to watch rows flow between operators
- Click any event to see its detail in the [Events](view:Events) pane

## 5 - Go deeper

- [Choose which events to capture](guide:ChoosingEvents) - waits, latches, memory, and call stacks
- [Read the timeline](guide:Timeline) - bands, playback, zoom, and selection
- [Trace a data modification](guide:LogRecords) - see a query's changes at the byte level, then roll them back

The full guide is at [internalsviewer.com](https://internalsviewer.com).
