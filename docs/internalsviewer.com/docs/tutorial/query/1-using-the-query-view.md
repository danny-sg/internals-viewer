# Using the Query view

So far we've looked at the database at rest. The Query view watches what the storage engine actually does when a query runs - it traces the query and replays the activity on a timeline.

This section of the tutorial covers:

- [Using the Query view](/docs/tutorial/query/1-using-the-query-view) - executing a query with a trace and reading the timeline
- [Views and layout](/docs/tutorial/query/2-views-and-layout) - the panes and how to arrange them
- [The execution plan](/docs/tutorial/query/3-execution-plan) - the plan connected to the timeline
- [Scans vs seeks](/docs/tutorial/query/4-scans-vs-seeks) - the two fundamental access patterns compared
- [Indexes and data](/docs/tutorial/query/5-indexes-and-data) - where the data lives: key lookups, covering indexes, and RID lookups
- [Joins](/docs/tutorial/query/6-joins) - the three physical joins and their access patterns

It uses the `dbo.ClusteredTable` table and `IX_ClusteredTable_TextField` index created in Parts 2 and 3.

## Open the Query view

Click the **Query** button on the database toolbar. This opens a Query tab for the connected database with a SQL editor.

![Query view](/docs/tutorial/images/screenshots/Query.png)

## Execute a query

Enter a query and press **Execute**:

```SQL
SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  TextField LIKE 'This is row 123%'
```

Internals Viewer runs the query with a trace session and captures what the engine did - physical page reads, locks acquired and released, waits, page splits, the execution plan, and more. When the query completes the captured activity is loaded into the timeline at the bottom.

Two toolbar options control how visible the storage engine activity is:

- **Clear Buffer Pool** - SQL Server caches pages in memory (the buffer pool), and a cached page produces no physical read. Clearing the buffer pool first means every page the query touches has to be read from disk, so the reads show up in the trace. This runs `DBCC DROPCLEANBUFFERS` - don't do this on a server anyone else is using!
- **Disable Read-Ahead** - when scanning, SQL Server normally reads large chunks of pages ahead of the scan. Disabling read-ahead makes the engine read pages individually, which is slower but much easier to follow.

> [!NOTE]
> Data modification queries (INSERT / UPDATE / DELETE) are run inside a transaction that is rolled back after the trace is captured, so you can experiment without permanently changing the data.

## The timeline

The timeline at the bottom shows the captured activity against time, in lanes:

- **Plan** - the execution plan operators, one bar per operator showing when it was active
- **Read** - physical page reads
- **Lock** - locks acquired and released
- **Wait** - waits, where the query had to stop and wait for a resource

![Query timeline](/docs/tutorial/images/screenshots/Query_timeline_cropped.png)

The playback controls replay the query like a recording - play, step forward, step back, and a speed control. The red cursor marks the current position, and you can drag it to scrub through the trace.

Zoom in on the Read lane - each tick is a single 8 KB page being read from disk. For our indexed query there should only be a handful of reads: the root-to-leaf seek we walked manually in Part 3. Now try a query that can't use the index:

```SQL
SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  TextField LIKE '%row 123%'
```

The leading wildcard forces a scan, and the Read lane fills with page reads as the engine works through the whole table. [Scans vs seeks](/docs/tutorial/query/4-scans-vs-seeks) digs into this contrast.

## Operators and the iterator model

The bars in the Plan lane make more sense with a picture of how a plan actually executes. An execution plan is a tree of _iterators_: execution starts at the top, and each operator asks the operator below it for a row, which asks the operator below it, and so on down to the operator reading pages. Rows are pulled up through the tree one at a time - the SELECT at the top pulls a row, and a chain of requests ripples down and a row ripples back up. An operator's bar in the Plan lane spans the time it was active in this process.

How an operator responds to that pull is what divides them into two kinds:

- **Streaming operators** hand each row on as soon as they receive it. A scan, a seek, a Compute Scalar, a Nested Loops join - rows flow through them continuously from the first request.
- **Blocking operators** can't produce their first row until they have consumed their entire input. A Sort is the clearest example: it can't emit the first row (the smallest value) until it has seen the last input row, because the last row might be the smallest.

Clicking an operator's bar in the timeline selects it and highlights when it actually streamed rows. Try a query that has to sort:

```SQL
SELECT NumberField
      ,TextField
FROM   dbo.HeapTable
ORDER  BY TextField
```

![Sort operator selected showing blocking behaviour](/docs/tutorial/images/screenshots/Query_operator_sort_selected.png)

Click the Sort's bar: for almost its whole lifetime it is consuming its input - pulling every row up from the scan below - and only at the very end does it stream its output to the SELECT above. The gap between "active" and "streaming" is the blocking behaviour made visible. Compare it with the scan feeding it, which streams for its entire bar.

This distinction runs through the rest of this section - it is why a hash join has two phases, and why one blocking operator low in a plan delays everything above it.

Next: [Views and layout](/docs/tutorial/query/2-views-and-layout)
