# Simulation

The Simulation tab runs the query's plan again one step at a time, so each operator can be watched doing its work -
which page a scan reads next, which row a seek lands on, what goes into a hash table, when a sort lets its rows go.

It reads the database's real pages, but the operators are Internals Viewer's own, built to behave like SQL Server's.
That makes it a simulation rather than a recording - for what SQL Server itself did, see the [Timeline](view:Timeline),
or [Full Trace](guide:FullTrace) for every call it made.

## Opening a simulation

Run the query first, so there is a plan. Then:

- **Simulate** on the SQL Editor's command bar, or [Simulation](view:Simulation) on the View menu, simulates the
  operator selected in the [Execution Plan](view:ExecutionPlan), or the whole statement if nothing is selected
- **Right-click** an operator in the plan, or its bar on the Timeline's Plan band, and choose **Simulate**

The simulation covers the chosen operator and everything beneath it. If an operator beneath it cannot be simulated,
the tab does not open.

## Stepping

- **Step** takes one step - read a page, compare a row, pass a row to the operator above
- **Run** steps continuously at the **Speed** beside it, and **Run to end** goes straight to the end
- **Reset** goes back to the start - there is no step back
- **F5** runs and pauses, **Shift + F5** runs to the end, and **Ctrl + Shift + F5** resets and runs

In batch mode a step is a whole batch, since that is the unit batch mode works in.

## What it shows

- **An operator tab** for each operator - its inputs, its state and what it is working on. Scans and seeks draw the
  index tree, the heap's extents or the columnstore's row groups, with the current page and row picked out
- **Steps** - every step so far, newest at the top, indented by the operator's depth in the plan
- **Description** - what the selected operator does, its properties, its phases and the counters
- **Plan** - the part of the plan being simulated

Click the circle on an operator tab's header to set a breakpoint, and **Run** stops after that operator's next step.
**Layout** on the tab strip switches between nested, with each operator above its children as in the plan, and flat.

## What can be simulated

Most row mode operators can be simulated - scans, seeks and lookups, Nested Loops, Merge Join and Hash Match, Sort,
Stream Aggregate, Top, Filter, Compute Scalar, Segment and Sequence Project. In batch mode the Columnstore Index Scan,
Filter, Compute Scalar and Hash Match (Aggregate) can.

Parallelism, spools, Adaptive Join, window aggregates and data modifications cannot, so a parallel plan can only be
simulated beneath its exchanges.

> A predicate the simulation cannot translate is skipped, so the simulation can return rows SQL Server filtered out.
