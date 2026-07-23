# Timeline

The [Timeline](view:Timeline) shows the captured activity against time and replays it like a recording.

It is split into bands:

- **Plan** - the execution plan operators, one bar per operator showing when it was active
- **Read** - physical page reads, split into Buffer (from memory) and Disk lanes
- **Lock** - locks acquired and released, bucketed per object and coloured by category
- **Latch** - page latches acquired and released
- **Wait** - where the query stopped and waited for a resource

## Playback

- Press play, or drag the red playhead to scrub through the trace
- The step buttons jump to the previous or next read
- The speed button cycles 0.5x, 1x, 5x, and 10x
- **Threads** overlays the worker threads of parallel operators on their bars, showing skew between threads
- The audio toggle plays a tone per read as the playhead sweeps

## Working with events

- **Mouse wheel** zooms, centred on the cursor, from the whole query down to individual events
- **Click** an event to select it in the [Events](view:Events) pane, or **double-click** to open its page
- **Click** an operator's bar to highlight when it actually streamed rows
- **Right-click** a scan or seek to open its index, linked to the replay
- Drag the handles either side of the playhead to select a time range, and **double-click** the playhead to clear it
