# Full Trace

**Record Full Trace** on the **Trace** menu records the query with Microsoft's Time Travel Debugging (TTD), then
replays the recording. Instead of the call stacks captured when events fire, the [Call Tree](view:CallTree) holds
every call the query made - how many times it was made, the memory it allocated and the values it was passed.

> **Development and test instances only**
>
> Every run asks for administrator consent and can restart the SQL Server service, dropping every connection to it.
> The recorded query runs far slower than usual, and a few seconds of recording takes several gigabytes of disk.

## Before you start

- SQL Server has to be on the same machine as Internals Viewer
- WinDbg from the Microsoft Store provides the recorder - if it is missing, turning Full Trace on shows how to install it
- The TTD licence is shown the first time, and Full Trace stays off until it is accepted

## Turning it on

Turning on **Record Full Trace** shows a dialog explaining what a recording involves. While it is on, a red
**Full Trace** badge sits at the end of the menu bar - every **Execute** in this tab is recorded, and clicking the
badge turns it off.

A recording has no clock, so the Timeline, Events and Allocations panes are hidden, and the
[Trace Timeline](view:TraceTimeline) takes the Timeline's place. Turning Full Trace off puts the previous layout back.

## On Execute

1. A User Account Control prompt starts the recorder. If SQL Server has been recorded since it last started, its
   service is restarted first
2. Unless **Clear Buffer Pool** is on, the query runs once untraced and is rolled back, so reading pages from disk and
   compiling the plan stay out of the recording
3. The query runs under the recorder
4. The results and plan appear straight away, and the recording loads in the background. The Call Tree shows its
   progress, with a **Cancel** link

Keep the query small - the larger the recording, the longer it takes to load.

## What it shows

- [Call Tree](view:CallTree) - every call on the query's threads, merged into one tree with a count on each path and
  the memory allocated beneath each frame
- [Trace Timeline](view:TraceTimeline) - every call as a bar on its thread, in the order it happened
- **Arguments** - right-click a frame in the Call Tree and choose **Show Arguments** to see what each call was
  passed and returned
- [Execution Plan](view:ExecutionPlan) - each operator is matched to the code that ran it

**Export to file** on the **Query** menu keeps a Full Trace for later, without the recording itself.
