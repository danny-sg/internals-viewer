# Driving the Trace Timeline

The Trace Timeline shows a Full Trace run in three panes: Operators (the query's iterators over time), Memory, and Calls
(the flame chart of engine functions, one lane per thread). Its axis is positions, hex `Sequence:Steps`, or instructions,
and never milliseconds. The `trace_` tools change what the user sees, so say what you are about to show before you show it.

## Options and tracks

- `trace_options` lists the panes, the scale, the highlights, the operator tracks, the selected call, the pins and the open
  indicator. `trace_set_option` changes one, for example `pane-calls` to show the flame chart.
- Use the Operators pane for the query's iterator view and the Memory pane for detail.
- `trace_track_add` marks a function on the Operators pane, for example `SearchPage`. `trace_track_set` shows or hides a track
  and `trace_track_remove` clears tracks.

## Finding and moving

- `trace_find_functions` is a fuzzy search of the call graph to settle ambiguous names.
- `trace_goto_call` selects and zooms to a call, such as the first `GetRow`, and conditions narrow the calls considered.
- `trace_step` steps the playhead by call, same call, batch, row or allocation. `trace_search` and `trace_zoom` search and zoom.
- `query_references` is the call tree's Find. Give it a value, usually an object address, and it names the type and lists
  every function the value was called on, passed to or returned by.

## Pins

`trace_pin` pins lines at every call of a function or at positions, to show when they happen. A click on the timeline or a
playhead move hides the pins, so move the playhead first and pin last.

## Indicators

`trace_indicate` points arrows at calls on the flame chart with a comment, for example "here is where the build side is read".
Each target is a function and which of its calls (first, last, next, previous, all, or the nth), with a short label when
there is more than one arrow. The arrows and the comment are one group. The user can drag the comment out of the way, the
arrows follow it, and its close button closes the whole group. A new indicator replaces the open one. `trace_indicate_clear`
removes it when the conversation moves on to something else.

The comment is Markdown. Put function, class and field names in backticks so they show in a monospaced font, for example
"`CQScanHash::GetRow` reads the probe side". Use **bold** sparingly, and keep it to a sentence or two.

## Walking the user through

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
