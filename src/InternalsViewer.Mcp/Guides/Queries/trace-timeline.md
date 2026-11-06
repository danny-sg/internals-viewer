# Driving the Trace Timeline

The Trace Timeline shows a Full Trace run in three panes: Operators (the query's iterators over time), Memory, and Calls
(the flame chart of engine functions, one lane per thread). Its axis is positions, hex `Sequence:Steps`, or instructions,
and never milliseconds. The `trace_` tools change what the user sees, so say what you are about to show before you show it.

## Options and tracks

- `trace_options` lists the panes, the scale, the highlights, the operator tracks, the selected call, the pins and the open
  indicator. `trace_set_option` changes one, for example `pane-calls` to show the flame chart.
- Use the Operators pane for the query's iterator view and the Memory pane for detail.
- `trace_track_add` adds a rule that marks a function on the Operators pane, for example `SearchPage`. A rule given an
  existing track's name joins that track as another kind of tick with its own label and colour. `trace_track_set` shows or
  hides a track and `trace_track_remove` removes the rules that were added.

## Finding and moving

- `trace_find_functions` is a fuzzy search of the call graph to settle ambiguous names.
- `trace_goto_call` selects and zooms to a call, such as the first `GetRow`, and conditions narrow the calls considered.
- `trace_step` steps the playhead by call, same call or a visible Operators track, landing on the end of each tick, so
  `GetRow` steps from row to row and `Reads` from read to read. `trace_search` and `trace_zoom` search and zoom.
- `query_references` is the call tree's Find. Give it a value, usually an object address, and it names the type and lists
  every function the value was called on, passed to or returned by.

## Crop To query

Crop To Query, on by default, hides what runs outside the query's execution from the call tree and the Trace Timeline:
compilation and setup before it, and teardown after it. The tools still see everything, so an arrow, a pin or the playhead
at a hidden call would point at nothing the user can see.

- The call search looks inside the crop first, so "the first `GetRow`" is the first one the query executed. It only goes
  outside the crop when the function has no calls inside it.
- When `show_call`, `set_playhead`, `trace_goto_call`, `trace_pin`, `trace_indicate` or `trace_walkthrough` would point at
  something the crop hides, it turns Crop To Query off and says so in its result. Tell the user you opened it up and why,
  for example that the call is part of compiling the plan rather than running it.
- To show the compilation phase on purpose, turn `crop-to-query` off first with `query_set_option`, so the search covers
  it, and turn it back on when you move on.

## Pins

`trace_pin` pins lines at every call of a function or at positions, to show when they happen. A click on the timeline or a
playhead move hides the pins, so move the playhead first and pin last.

## Watching a field change

To show how an operator keeps its state, such as how a TOP counts the rows it has returned:

1. Find the field. `query_operators` gives the iterator (`this`), `query_memoryhistory --iterator <this>` lists its blocks,
   and `query_memoryhistory --block N` shows the writes field by field. A counter is a small field written once per row, its
   value going up by one each time. `query_object_graph` lists the objects the block points to, when the state lives in
   one of those.
2. `memory_watch add` pins it with a type and a label, for example `int` at `0x40` labelled "Rows Returned". The watch is
   temporary. `memory_watch clear` removes the ones you added and leaves the user's own.
3. Open a view that shows the watch, `show_memory` for the block's bytes and Watches tab, or `show_object_graph` for the
   box with the watch under its address.
4. `trace_scope` with the positions of the field's first and last writes scopes the playhead to that stretch and zooms to it.
   Turn on `playing` with `trace_set_option` and the playhead repeats the stretch, so the user sees the value change again
   and again until it is stopped. `trace_scope` with no positions clears the scope when you move on.

## Indicators

`trace_indicate` points arrows at calls on the flame chart with a comment, for example "here is where the build side is read".
Each target is a function and which of its calls (first, last, next, previous, all, or the nth), with a short label when
there is more than one arrow. The arrows and the comment are one group. The user can drag the comment out of the way, the
arrows follow it, and its close button closes the whole group. A new indicator replaces the open one. `trace_indicate_clear`
removes it when the conversation moves on to something else.

The comment is Markdown. Put function, class and field names in backticks so they show in a monospaced font, for example
"`CQScanHash::GetRow` reads the probe side". Use **bold** sparingly, and keep it to a sentence or two.

## Walking the user through

Each indicator or walkthrough step moves the playhead to the start of the first call it points at, so the call stack,
the memory tabs and any watches show that moment. Give `playhead` as `end` to show what the call left behind, for example a
counter after it went up, `none` to leave the playhead, or a position. The user can step back with Previous.

A walkthrough holds the conversation while it waits for the user, so offer one rather than starting it. When the user
accepts:

1. Plan every step from what you have already explained, in the same order, one finding per step.
2. Call `trace_walkthrough` once with all the steps. Keep each comment to a sentence or two that says what the user is looking
   at and why it matters. The app shows the first step, and its Next button moves to the next one without another call, so
   stepping is instant. The comment shows which step it is, for example "2 of 5", and the last step has Finish. Each step
   zooms the flame chart to its calls.
3. It waits about 30 seconds and says how far the user got. While it is still open, call `trace_indicate_wait` with the id.
4. When the user went through every step, or closed it part way, stop and ask what they would like to look at next.

Use `trace_indicate` with `next` only for a single step, or when the next step depends on something you can only work out
after the user has seen this one.
