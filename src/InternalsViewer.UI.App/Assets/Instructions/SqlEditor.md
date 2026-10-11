# SQL Editor

The [SQL Editor](view:SqlEditor) is where queries are edited and run. Enter SQL and click **Execute**. Press the **Stop** button to cancel
execution.

The command bar includes options for how the query will be executed:

## Simulate

**Simulate** opens the Simulation tab, which steps through the query's plan one operator at a time - the operator selected in the
Execution Plan, or the whole statement.

## Track query

**Track query** on the Query menu, on by default, runs the query with a trace. A modification query (insert/update/delete etc.) runs
inside a transaction that is rolled back once its events are captured, so the query can be replayed against the initial state of the
database.

If Track query is off the query runs without a trace, and a modification runs without a transaction - so it changes the data.

## Clear Buffer Pool

Clearing the buffer pool clears the in-memory data cache for the **entire server** before the query runs. This means that the query will
run 'cold' - reads will be physical reads from disk rather than logical reads from memory.

> **This option should not be used on a production server**

## Disable Read Ahead

Read-ahead is a performance optimization where SQL Server will anticipate reads/pages the query may need and fetches them ahead of time.

This option disables read-ahead for the session by running `DBCC TRACEON(652)` before the query executes.

Disabling Read Ahead gives a simpler read and toggling it on and off allows you to see the difference and effect of using it.

> Trace Flag 652 doesn't disable read ahead for all scenarios - there are certain operators and access methods where it is ignored.
> 
> Read-ahead reads can be seen in the Events list and Timeline where the read covers a range of pages, e.g. 'Read (Disk): 64 pages from (Page Address)'
> and the call stack will reference read ahead functions, e.g. `BPool::ReadAhead`.

## Results

If **Results** is selected the query data results will be included on the Results tab when the query has run.

## Messages

The messages tab includes messages/errors from the query and information about the post-query parsing/capture that runs for tracing.

## Tips

- `F5` executes a query
- **Ctrl + H** shows and hides the History, and **Ctrl + R** the Results and Messages
- Select text and execute to run just the selected portion of a query
- **Ctrl + mouse wheel** changes the editor font size

## Multi-statement scripts

If you have a script that requires setup/teardown and do not want to trace that part of it, select the SQL that will be traced, right click,
and choose 'Trace query selection'. This will trace only the highlighted portion while still running any other parts of the query.

Clear the selection by right clicking and choosing 'Clear query selection'.