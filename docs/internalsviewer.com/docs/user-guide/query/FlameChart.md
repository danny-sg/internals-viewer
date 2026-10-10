# Flame Chart

The Flame Chart draws every call recorded by a [Full Trace](/docs/user-guide/query/FullTrace) - one lane per thread, one bar per call, each call nested beneath the call that made it. The bars use the [Call Stack](/docs/user-guide/query/CallStack)'s category colours.

The Call Stack merges every call down the same path into one row with a count. The Flame Chart keeps each call apart, in the order it happened, so a scan's `GetRow` calls show as a run of short bars, one per row, and a function called once for a long time as a single wide one. It takes the Timeline's place while **Record Full Trace** is on, and the **Flame Chart** item on the View menu, enabled only then, brings it back if it is closed.

<!-- Screenshot: the Flame Chart for a parallel query, with a lane per thread and the Operators pane open above them -->

## Position and instructions

TTD has no clock. A recording is a sequence of positions - a sequence number shared by every thread, plus the number of instructions the thread has executed since - so the ruler is never in milliseconds. The two toggles on the tab strip choose what a bar's width means:

- **Position** - the default. Width is the span of trace positions a call covered. Positions are shared by every thread, so the lanes line up against each other and parallel workers can be compared.
- **Instructions** - width is the number of instructions the call executed, which is the closest the recording has to how long a call took. Each thread counts its own instructions from zero, so every lane starts at the left and the lanes do not line up.

## Moving around

- **Mouse wheel** zooms in and out at the pointer
- **Shift + mouse wheel**, or a horizontal wheel, pans sideways
- **Ctrl + mouse wheel** scrolls through the threads
- **Drag** pans sideways, and a **middle-button drag** pans in both directions
- **Shift + drag** draws a rectangle and zooms to it
- **Double-click** a bar to zoom to it, or empty space to zoom back out to the whole recording
- **Click a thread's header** to expand that thread and collapse the others to their headers, and click it again to show them all

Rows shrink to fit the pane, from 16 pixels down to 2, before a scrollbar appears, and names are only drawn on rows of 11 pixels or more - zoom in, or collapse the other threads, to read them.

## Selecting a call

**Click** a bar to select the call. The Call Stack goes to its frame, and the [Arguments](/docs/user-guide/query/CallStack#arguments) tab follows with that call selected. Click the selected bar again, or the background, to deselect.

Hovering over a bar describes the call:

- The function and its category
- **Thread**, **Call** and **Depth** - the thread it ran on, which call of the function it was, and how deep in the stack
- **Instructions** - how many instructions it executed
- **Position** - the trace positions it started and ended at
- **Allocated**, **Freed** and **Retained At Return** - for a call that allocated memory, how much and in how many allocations, how much it freed, and how much was still held when it returned
- **Started Before The Recorded Call** - the call was already running when its part of the recording began, so its start is not known
- **Did Not Return** - the call had not returned when the recording stopped, or the recording lost it in a gap

## Following the Call Stack

Selecting a frame in the Call Stack re-roots the chart on that function. Only its calls and everything beneath them are drawn, zoomed to fit, so a function called from all over the query can be looked at on its own. Depths count from its outermost call, so a function that calls itself does not draw over itself. A thread that never ran the function - a parallel worker never runs beneath the coordinator's frames - shows what it was doing over the same trace positions instead. Deselect to go back to the whole recording.

The Call Stack's [category filter](/docs/user-guide/query/CallStack#filtering-by-category) applies here as well. A hidden category hides its calls and everything beneath them.

The **Lock** toggle on the tab strip keeps the chart where it is. Selecting a frame no longer re-roots it and clicking the background no longer deselects, but clicking a bar still selects that call in the Call Stack and Arguments - so one stretch of the recording can stay in view while you look through its calls.

The **Search** toggle opens a box above the chart. Calls whose function name does not contain every word typed are dimmed, so the matches stand out. **Escape** closes it.

## Operators

The **Operators** toggle adds a pane above the threads, with a row per plan operator in the same order and colours as the Timeline's Plan band. Each row runs from the operator's first call to its last. The stretches where one of its methods was on the stack are drawn in full colour and the rest shaded, and an operator that ran on several threads gets a track per thread.

This shows something the lanes cannot. An operator is only on a thread's stack while one of its methods runs, so a Hash Match sits beside its scans in the lanes rather than above them - but its iterator object, and the hash table it owns, live from its first call to its last. The Operators pane draws that lifetime.

- **Drag** the splitter under the pane to make its rows taller
- **Click** an operator to select it in the [Execution Plan](/docs/user-guide/query/ExecutionPlan) and dim everything except its calls and the calls beneath them
- **Double-click** an operator to zoom to its lifetime
- Hovering over an operator shows its thread, how many times each of its methods was called, its instructions and positions, the share of its lifetime spent inside its calls (child operators included) and its peak memory in use

<!-- Screenshot: the Operators pane above the thread lanes, with an operator selected and the other calls dimmed -->

## Memory

The **Memory** toggle shows where the query allocated memory:

- **The memory band** - along the bottom of the chart, **Allocated** is the memory allocated in each sliver of the recording, and **In Use** is the memory allocated and not yet freed, with its peak in the label. When the plan had a memory grant, the grant is drawn as a box labelled **Granted** and the line becomes **Grant In Use**, counting only the query's workspace memory so the two compare like for like. The band needs the Position axis, since each thread counts its instructions separately and they cannot be added together.
- **Raised calls** - the calls rise off the chart in 3D. A dropdown above the ruler picks **Memory: Allocated**, which raises each call by the memory it allocated itself, or **Memory: In Use**, which raises a surface along each call showing what it had allocated and not yet freed as it ran. Hover over a raised call to see its size, and drag one to change the angle and depth of the extrusion.

<!-- Screenshot: the Flame Chart with Memory on, calls raised by memory and the memory band along the bottom -->

## Playhead

Click the ruler to put a playhead on the chart, and drag along the ruler to move it. Its badge gives the position, as `sequence:steps` on the Position axis. The step back and step forward buttons move it to the start of the previous or next call on any visible thread, and double-clicking the ruler hides it again.

With **Memory** off, the calls under the playhead pop out of the chart, each raised by the memory it allocated, so scrubbing along shows which part of the stack is allocating. Drag a raised call to change the angle and depth, as with the memory view. With Memory on, the band marks the memory allocated and in use at the playhead.

::: details How this works
Each bar is a call span recorded during the Full Trace replay - the call's start and end positions, the instructions executed in between, its thread and its place in the call tree. Position is the sequence number plus the step count scaled within it, so it lines up across threads. Instructions is a clock kept per thread from the step counts.

See [How query tracing works](/docs/deep-dives/query-tracing#full-trace) for the details.
:::
