# Trace Timeline

The Trace Timeline draws every call recorded by a [Full Trace](/docs/user-guide/query/FullTrace) - one lane per thread, one bar per call, each call nested beneath the call that made it. The bars use the [Call Tree](/docs/user-guide/query/CallStack)'s category colours.

The Call Tree merges every call down the same path into one row with a count. The Trace Timeline keeps each call apart, in the order it happened, so a scan's `GetRow` calls show as a run of short bars, one per row, and a function called once for a long time as a single wide one. It takes the Timeline's place while **Record Full Trace** is on, or sits beside it when **Record Events** is on too, and the **Trace Timeline** item on the View menu, enabled only then, brings it back if it is closed.

<!-- Screenshot: the Trace Timeline for a parallel query, with a lane per thread and the Operators pane open above them -->

## Reading the chart

From top to bottom:

- **The ruler** - how far through the recording, in trace positions or instructions - see [Position and instructions](#position-and-instructions). Click it to put down the [playhead](#playhead)
- **The Operators pane** - with **View > Operators** on, a row per plan operator - see [Operators](#operators)
- **A lane per thread** - headed **Thread** and the operating system thread id
- **The memory band** - with **Memory** on, the memory allocated and in use along the bottom - see [Memory](#memory)

In a lane, each call is a bar running from where it started to where it ended. The calls it made sit directly beneath it, inside its width, so the outermost call is at the top of the lane and each row down is one level deeper in the stack. That is the reverse of a classic flame graph, which grows upwards, but it reads the same way as the Call Tree. A bar is coloured by its function's category and labelled with the function's name once it is wide enough to read.

- **A wide bar** - a call that ran for a long stretch, and on the Instructions axis, one that executed a lot of code
- **A run of thin bars** - the same function called over and over, such as a scan's `GetRow` once per row
- **A gap beneath a bar** - the call working in its own code rather than in a call it made, or in Extended Events and tracing code, which is left out of the chart
- **A pale strip** - calls too narrow to draw one by one, merged into a single strip. Zoom in to pull them apart

The Trace Timeline is not the [Flame Graph](/docs/user-guide/query/ExecutionPlan#flame-graph) on the Execution Plan, which breaks each operator's bar down into the calls sampled from its events.

## Position and instructions

TTD has no clock. A recording is a sequence of positions - a sequence number shared by every thread, plus the number of instructions the thread has executed since - so the ruler is never in milliseconds. **View > Scale** chooses what a bar's width means:

- **Position** - the default. Width is the span of trace positions a call covered. Positions are shared by every thread, so the lanes line up against each other and parallel workers can be compared. A position moves on by one whole step at each point where a thread synchronises with another or leaves the recorded code, however many instructions came before it. So short calls that cross one of those points come out about the same width, and calls that stay inside the recorded code without crossing one are thin.
- **Instructions** - width is the number of x64 machine instructions the call executed, including the calls beneath it, which is the closest the recording has to how long a call took. Only the storage engine and query processor are recorded, so time spent in SQLOS or Windows - allocating memory, waiting, reading from disk - adds next to nothing. Each thread counts its own instructions from zero, so every lane starts at the left and the lanes do not line up.

## Moving around

- **Mouse wheel** zooms in and out at the pointer
- **Shift + mouse wheel**, or a horizontal wheel, pans sideways
- **Ctrl + mouse wheel** scrolls through the threads
- **Drag** pans sideways, and a **middle-button drag** pans in both directions
- **Shift + drag** draws a rectangle and zooms to it
- **Shift + click** a bar to scope the chart to its function, or empty space to go back to the whole recording
- **Double-click** a bar to zoom to it, or empty space to clear the selected call and operator
- **Click a thread's header** to expand that thread and collapse the others to their headers, and click it again to show them all

The lanes shrink to fit every thread into the pane, and a call's name appears once there is room to read it - zoom in, or click a thread's header to give it the whole pane, to see more of them.

## Right-click menu

Right-clicking a bar gives:

- **Zoom to Selection** - only while a call is selected, wherever you right-click. Zooms to the selected call, as double-clicking it does, and scrolls its thread into view
- **Zoom to Fit** - wherever you right-click. Zooms back out to the whole recording
- **Scope** - scopes the chart to the call's function, as **Shift + click** does - see [Scoping to a function](#scoping-to-a-function)
- **Clear Scope** - only while the chart is scoped, wherever you right-click. Goes back to the whole recording
- **Open Call Tree** - opens the [Call Tree](/docs/user-guide/query/CallStack) at the call's frame, with the call selected in the [Arguments](/docs/user-guide/query/CallStack#arguments) tab. Right-clicking empty space just opens the pane, and if it is already open its tab is selected
- **Open Call Stack** - only over a call. Selects the call and opens the [Call Stack](#call-stack) pane, listing the calls that led to it with the values they were passed

<!-- Screenshot: the Trace Timeline's right-click menu over a call, with Scope and Open Call Tree -->

## Selecting a call

**Click** a bar to select the call. It is outlined, the Call Tree goes to its frame, and the [Arguments](/docs/user-guide/query/CallStack#arguments) tab follows with that call selected. Nothing is dimmed, so the rest of the chart stays readable. Only a [search](#search) dims the chart. Click the selected bar again, or the background, to deselect.

With a call selected, the arrow keys move the selection:

- **Up** - the call that made it
- **Down** - back down the [call stack](#call-stack), or from its innermost call to the first call it made
- **Left** and **Right** - the previous or next call at the same depth on its thread, even when a different call made it

With **View > Auto-Zoom** on, the chart zooms to fit the selected call whenever the selection changes, whether it was clicked here or selected in the Call Tree or **Arguments**.

Hovering over a bar describes the call:

- The function and its category
- **Thread**, **Call** and **Depth** - the thread it ran on, which call of the function it was, and how deep in the stack
- **Instructions** - how many instructions it executed
- **Position** - the trace positions it started and ended at
- **Allocated**, **Freed** and **Retained At Return** - for a call that allocated memory, how much and in how many allocations, how much it freed, and how much was still held when it returned
- **Started Before The Recorded Call** - the call was already running when its part of the recording began, so its start is not known
- **Did Not Return** - the call had not returned when the recording stopped, or the recording lost it in a gap

## Following the Call Tree

Choosing a call in the [Arguments](/docs/user-guide/query/CallStack#arguments) tab selects it in the chart, outlined as if its bar had been clicked, and scrolls it into view. If the chart is scoped somewhere that call is not, it goes back to the whole recording to show it. A frame in the Call Tree stands for every call made down that path, so selecting a frame there does not pick a call, and the chart is left as it is.

<!-- Screenshot: one call chosen in the Arguments tab, outlined in the Trace Timeline -->

The Call Tree's [category filter](/docs/user-guide/query/CallStack#filtering-by-category) applies here as well. A hidden category's calls are taken out and the calls they made move up into their place, so a category you have kept is never lost beneath one you have hidden. A category hidden with **Hide With Callees** takes everything beneath its calls with it.

## Scoping to a function

**Shift + click** a bar, or right-click it and choose **Scope**, to scope the chart to its function. Only that function's calls and everything beneath them are drawn, zoomed to fit, so a function called from all over the query can be looked at on its own. Depths count from its outermost call, so a function that calls itself does not draw over itself. A thread that never ran the function - a parallel worker never runs beneath the coordinator's frames - shows what it was doing over the same trace positions instead.

**Shift + click** empty space, or choose **Clear Scope** from the right-click menu, to go back to the whole recording.

![The Trace Timeline scoped to CQScanTableScanNew::GetRow](/docs/user-guide/images/flame-chart-rooted-on-frame.png)

## Search

The **Search** toggle opens a bar above the chart. Calls whose function name does not contain every word typed are dimmed, so the matches stand out. **Escape** closes it.

The filter button beside the box opens the same category badges as the Call Tree's [category filter](/docs/user-guide/query/CallStack#filtering-by-category), and they are one setting - hiding a category here hides it in the Call Tree too. The arrows after it move to the previous or next matching call, and are greyed out while nothing matches.

## Operators

**View > Operators** adds a pane above the threads, with a row per plan operator in the same order as the Timeline's Plan band. Operators are drawn in the Query Operator category's colour, and a Batch To Row adapter between batch and row mode operators is grey. Each row runs from the operator's first call to its last. The stretches where one of its methods was on the stack are drawn in full colour and the rest shaded, and an operator that ran on several threads gets a track per thread. Either side of that, a fainter bar shows how long the operator's iterator existed. It starts at the iterator's constructor, or for a batch mode operator the call that built it, and ends with the query's `CQueryScan::DestroyQuery`, where SQL Server frees all of a query's iterators together. When the recording started after an iterator was built, or stopped before the query was torn down, the fainter bar runs to the edge of the recording.

This shows something the lanes cannot. An operator is only on a thread's stack while one of its methods runs, so a Hash Match sits beside its scans in the lanes rather than above them - but its iterator object exists from the moment it is built until the query is torn down, and does its work from its first call to its last. The Operators pane draws both.

- **Drag** the splitter under the pane to make its rows taller
- **Click** an operator to select it in the [Execution Plan](/docs/user-guide/query/ExecutionPlan). While [Call Stack](#call-stack) is off, everything except its calls and the calls beneath them is dimmed
- **Double-click** an operator to zoom to its lifetime
- Hovering over an operator shows its thread, how many times each of its methods was called, its instructions and positions, the share of its lifetime spent inside its calls (child operators included) and its peak memory in use

![The Operators pane above the thread lanes, with the Table Scan selected and every call outside it dimmed](/docs/user-guide/images/flame-chart-operator-selected.png)

## Reads

**View > Reads** adds a lane under the operators showing every page the query asked the buffer pool for. Each read is drawn as in the Timeline's Read lane, a faint bar across the call with a solid tick at its end. A call to `BPool::Get` asks for a single page. A call to `BPool::ReadAhead` asks for a run of pages ahead of the scan, and is labelled with how many when there is room. Pages already in memory and pages read from disk are drawn the same way. The calls beneath a read in the lanes below show which it was, such as `BPool::GetFromDisk` when the page had to be read.

Reads are drawn in the Buffer Pool category's colour, and lines join each one to the operator whose call it was made in, as in the [Timeline](/docs/user-guide/query/Timeline)'s Read lane: a dotted line where the call starts, when the read is wide enough to tell it apart, and a solid line at its end for each page. Reads made outside every operator, such as metadata lookups while the query is set up, have no lines.

- Hovering over a read shows its page, or the first page and page count of a read ahead, with its function, operator, thread and position
- **Click** a read to select its call in the chart
- **Double-click** a read to zoom to it

## Memory

The **Memory** toggle shows where the query allocated memory:

- **The memory band** - along the bottom of the chart, **Allocated** is the memory allocated in each sliver of the recording, and **In Use** is the memory allocated and not yet freed, with its peak in the label. When the plan had a memory grant, the grant is drawn as a box labelled **Granted** and the line becomes **Grant In Use**, counting only the query's workspace memory so the two compare like for like. The band needs the Position axis, since each thread counts its instructions separately and they cannot be added together.
- **Raised calls** - the calls rise off the chart in 3D. A dropdown above the ruler picks **Memory: Allocated**, which raises each call by the memory it allocated itself, or **Memory: In Use**, which raises a surface along each call showing what it had allocated and not yet freed as it ran. Hover over a raised call to see its size, and drag one to change the angle and depth of the extrusion.

![Memory: Allocated - each call raised by the memory it allocated itself, with the memory band along the bottom](/docs/user-guide/images/flame-chart-memory-allocated.png)

![Memory: In Use - a surface along each call showing the memory it held as it ran](/docs/user-guide/images/flame-chart-memory-in-use.png)

The **Memory** toggle and the **Allocated** or **In Use** choice are remembered between sessions, and shared by every Trace Timeline.

## Playhead

Click the ruler to put a playhead on the chart, and drag along the ruler to move it. Its badge gives the position, as `sequence:steps` on the Position axis. The step back and step forward buttons move it to the start of the previous or next call on any visible thread, and double-clicking the ruler hides it again and clears the selected call and operator.

**Play** moves the playhead steadily along the chart, scrolling the view on when it reaches the edge, and stops at the end of the recording. It starts from the playhead, or from the left of the view when there is none or the playhead is already at the end. Click it again to pause.

With **Record Events** on, the Timeline drives the playhead too. As its current event changes, the playhead moves to where that event happened in the recording - see [Extended Events in a recording](/docs/user-guide/query/FullTrace#extended-events-in-a-recording).

With **Memory** and **Call Stack** off, the calls under the playhead pop out of the chart, each raised by the memory it allocated, so scrubbing along shows which part of the stack is allocating. Drag a raised call to change the angle and depth, as with the memory view. With Memory on, the band marks the memory allocated and in use at the playhead.

::: details How this works
Each bar is one call recorded during the Full Trace replay, with where it started and ended in the recording, the instructions it executed, its thread and its place in the call tree. Positions are shared by every thread, which is why that axis lines them up, while each thread counts its own instructions.

See [How Full Trace works](/docs/deep-dives/full-trace) for the details.
:::

## Call Stack

**Call Stack** on the tab strip opens a pane beneath the chart while a call is selected, listing the calls that led to it. Turned on with nothing selected, the pane opens as soon as a call is selected. **Open Call Stack** on the right-click menu selects a call and turns the pane on in one step. Its header has step back and step forward buttons, which select the previous or next call at the same depth as **Left** and **Right** do, and an **X** that closes it again. The stack sits under a row for the selected call's thread, each call shown as the function's signature with each parameter's type and the value it was passed, coloured as in the Call Tree's signatures. A dot before each call shows its category's colour, and hovering over it names the category. A value that decodes to an object shows the object's class instead of its declared type. `this` is shown without its address, unless it decodes to something such as an operator or an index, and its address is in the call's details. The innermost call comes first, highlighted and marked with a red arrow, and each call below it is the one that called it, as a debugger lists a stack. The pane selects the same call as the chart, and opens its details beside the stack. The values decode as they do in [Arguments](/docs/user-guide/query/CallStack#arguments), and the **Decode** setting is shared. Drag the splitter above the pane to resize it. The setting is remembered between sessions.

Selecting a call further up the stack, with **Up** or by clicking it in the pane, keeps the stack as it is and moves the selection along it, so **Down** comes back the same way. Selecting a call that is not on the stack, by clicking its bar or with **Left** and **Right**, starts a new stack ending at that call. The pane follows the [category filter](/docs/user-guide/query/CallStack#filtering-by-category) and **Scope**, so its calls are the ones drawn in the chart above it.

Clicking a call selects it in the chart, the Call Tree and **Arguments**, the same as clicking its bar, and opens its details beside the stack. These show its category, its module, `this` and the value it returns. They also show its **Call Id**, the number that **Source** shows in brackets, the trace positions it started and ended at, how many instructions it executed, and its **Arguments**, each with its number, type and value. Clicking an argument shows that value's details, as clicking it in the stack does. Right-clicking a call gives **Open In Call Tree**, **Zoom**, which fits the chart to that call, **Scope**, which scopes the chart to its function the same as the chart's own [Scope](#scoping-to-a-function), **Copy Signature**, which copies the line as it reads, with its types and values, **Copy Symbol**, which copies the function's name, and **Copy Address**, which copies the function's address. The copy button at the end of each call copies its signature, and every value in the details has a copy button beside it too. The value a call returned is in its details rather than the stack. The details are headed by the function's name. Clicking a value shows its details in place of the function's, and adds its argument number and type to the heading, such as `[3] SEBindings const *`. Right-clicking a value gives **Copy Decoded**, **Copy Value** and **Find**. The selected call's function, or the selected value, is highlighted in the stack in the same way as a search match, until the details are closed.

An object's details end with its **Source**, the path that brought it to the call, when there is one to trace. Each step, a little further in than the last, is a call further up the stack that was passed it too, and the path ends at the call that created it, marked with a green plus, such as its constructor or the allocation it was made in. Each step shows its call's number in the trace in brackets, so the numbers fall as the path goes back in time. A step that is not on the stack in the pane, such as a constructor that has already returned, opens to list the stack at that call, the function's callers in order, innermost first. An object that was in use before the trace began ends at the first call seen using it, without a plus.

Hovering over a step, or over a function listed under an opened step, highlights it and marks where its call starts with a green line from the call's bar down to the bottom of the chart. When the chart is not showing the call, because it is outside the part in view or outside the chart's scope or filter, a small map of the whole trace appears in the corner instead. It shows the step's call as a black line from its depth down, with the playhead in red when there is one, and the part in view shaded, so you can see how far away it is. When the chart is scoped, only the scope's calls in view are shaded. The map includes calls that the chart is hiding. Right-clicking a step, or a function listed under an opened step, gives **Go To**, which moves the playhead to the start of its call. If the call is outside the chart's scope, the scope is cleared first.
