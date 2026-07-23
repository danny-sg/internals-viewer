# Execution Plan

The [Execution Plan](view:ExecutionPlan) pane shows the captured plan as a tree of operators, connected to the
timeline. During replay it animates what is happening at the playhead - active operators highlight, rows stream
between operators as moving flow lines, and operators blocked waiting on their input are marked.

- Selecting an operator - here, on the timeline's Plan band, or in the Events pane - selects it everywhere
- **Right-click** an operator that reads an index to open that index, linked to the replay
- **Ctrl + mouse wheel** zooms, and the plan pans by dragging
- With [Call Stack](guide:CallStack) events captured, the **Flame Graph** toggle breaks each operator's bar down into
  the engine calls that made it up
