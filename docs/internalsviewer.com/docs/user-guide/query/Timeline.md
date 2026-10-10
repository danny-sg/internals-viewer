# Timeline

The Timeline shows the captured query activity against time, and replays it like a recording.

It is split into **bands**:

- **Log** - the transaction log records of a traced data modification, see [Log Records](/docs/user-guide/query/LogRecords)
- **Plan** - the execution plan operators, one bar per operator showing when it was active
- **Columnstore** - the work of a Columnstore Index Scan, which happens on compressed segments rather than pages - see [Columnstore](#columnstore) below
- **Read** - physical page reads, see [Reads](/docs/user-guide/query/Reads)
- **Lock** - locks acquired and released, see [Locks](/docs/user-guide/query/Locks)
- **Latch** - latches acquired and released, see [Latches](/docs/user-guide/query/Latches)
- **Wait** - waits, where the query had to stop and wait for a resource, see [Waits](/docs/user-guide/query/Waits)

Bands can be split into **lanes** that further categorize the events on the band - see each band's page for its lanes.

The Log and Columnstore bands only appear when the query produced those events, and the Lock, Latch and Wait bands need their event type to have been captured. Where a band is divided in two - Buffer and Disk on the Read band - the division collapses into a single lane when the timeline is too short to show it, and comes back as it is given more room.

Hovering over the timeline shows a tooltip describing the event under the pointer.

A query recorded with [Full Trace](/docs/user-guide/query/FullTrace) has no timed events, so while **Record Full Trace** is on the Timeline is hidden and the [Flame Chart](/docs/user-guide/query/FlameChart) takes its place.

## Playback

The playback controls replay the query like a recording - play/pause, step, and speed - and the red playhead can be dragged to scrub through the trace.

- The **step** buttons jump the playhead to the previous or next read event
- The **speed** button cycles through **0.5x**, **1x**, **5x**, and **10x**
- The **Threads** toggle overlays the worker threads of parallel operators on their bars in the Plan band - each worker drawn across its own active time and sized by its share of the rows, so time skew and data skew between threads are visible
- The **audio** toggle adds sound to the replay - a tone per read as the playhead sweeps, so the access pattern can be heard as well as seen

## Zoom and selection

The timeline can be zoomed with the **mouse wheel**, centred on the cursor, from the whole query down to individual events - with a scrollbar to move through the trace while zoomed in.

Dragging the handles either side of the playhead selects a time range. The selection scopes what is highlighted in the [Events](/docs/user-guide/query/Events) pane, making it easy to answer "what happened in this window". **Double-click** the playhead to clear the selection.

## Selecting and opening events

- **Click** an event to select it - this also selects the matching row in the Events pane
- **Double-click** an event that has a page associated with it (a read, a lock, a log operation) to open that page in the [Page Viewer](/docs/user-guide/page-viewer)
- **Click an operator's bar** in the Plan band to select it and highlight when it actually streamed rows - blocking operators like a Sort consume their input for most of their lifetime and only stream at the end
- **Right-click an index** in the Plan band to open it in the [Index View](/docs/user-guide/index-view), linked to the trace so pages light up as they are read

![Right-clicking an operator to open its index](/docs/user-guide/images/query-timeline-right-click-open-index-option.png)

## Columnstore

A Columnstore Index Scan reads compressed column segments rather than rows from pages, so its events get a band of their own, split into three:

- **Rowgroup** - the top. One track per rowgroup, with a bar for each column segment scanned. The rowgroup read and read-ahead events that fetch a rowgroup's segments from disk span the time until its last segment scan finished. Rowgroup elimination - where the scan skips a rowgroup from its segment metadata without reading it - is drawn in tracks across the top of the band. A parallel scan gets a lane per thread, so the rowgroups each worker took are side by side
- **Columnstore** - the middle, for events that belong to the scan as a whole: batch filters and bitmap filters, each labelled with how many rows survived of how many went in, and the per-rowgroup statistics of aggregate pushdown
- **Object Pool** - the bottom. The columnstore object pool is the cache the engine keeps decompressed segments and dictionaries in. Each lookup is a tick, green for a hit and red for a miss, in a track per column within its rowgroup, with the global dictionary and the delete bitmap in tracks of their own above the rowgroups

<!-- Screenshot: the Columnstore band with its Rowgroup, Columnstore and Object Pool sub-bands, and rails from the Read band up to the pool misses -->

A miss is what sends the engine to disk for a segment, so a miss is drawn until the read that fetched the object began, and the [Read](/docs/user-guide/query/Reads) band's rails run from each page read up to the miss it served, where there is one, rather than to the operator. The **Details** toggle in the [Events](/docs/user-guide/query/Events) pane shows the encoding, dictionary sizes and row counts behind each segment scan.
