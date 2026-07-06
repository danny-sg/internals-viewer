# Tutorial

In this tutorial we'll create a database from scratch. We'll run DDL (Data Definition Language) SQL to create objects, then run DML (Data Manipulation Language) SQL to insert and modify data to see how we can view it and the effects on the database storage internals.

This tutorial will cover:

- [Part 1 - Connecting and allocations](/docs/tutorial/1-connecting-and-allocations)
  - Connecting to a database
  - Viewing object allocations in the database
  - How to find object entry points

- [Part 2 - Viewing pages](/docs/tutorial/2-viewing-pages)
  - Viewing pages and records
  - The page header
  - How pages are linked together
  - Page types - Data, Index, LOB, and allocation pages

- [Part 3 - Indexes](/docs/tutorial/3-indexes)
  - Navigating indexes with the Index view
  - Heaps, clustered indexes, and non-clustered indexes
  - Index root, leaf, and levels

- Part 4 - Query
  - [Using the Query view](/docs/tutorial/query/1-using-the-query-view) - tracing a query and replaying it on the timeline
  - [Views and layout](/docs/tutorial/query/2-views-and-layout) - arranging the panes and watching the allocation map
  - [The execution plan](/docs/tutorial/query/3-execution-plan) - the plan connected to the timeline
  - [Scans vs seeks](/docs/tutorial/query/4-scans-vs-seeks) - the two access patterns on the Index view
  - [Joins](/docs/tutorial/query/5-joins) - Nested Loops, Merge, and Hash compared

- [Part 5 - LOB data](/docs/tutorial/5-lob-data)
  - How `VARCHAR(MAX)` values are stored - in row, off page, and split
  - LOB pointers, roots, and data chunks

To follow along you'll need a SQL Server instance where you can create a new database, and permission to connect with the `sysadmin` role - see [Permissions](/docs/introduction/permissions).

All of the SQL used in the tutorial is available as a single script: <a href="/internals-viewer/internals-viewer-tutorial.sql" download="internals-viewer-tutorial.sql">internals-viewer-tutorial.sql</a>

