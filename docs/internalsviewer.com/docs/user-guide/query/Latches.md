# Latches

The Latch band shows page [latches](https://learn.microsoft.com/en-us/sql/relational-databases/diagnose-resolve-latch-contention) acquired and released during the query - the lightweight, short-lived locks SQL Server uses to protect a page's in-memory consistency while it is being read or modified, distinct from the row/page/object [Locks](/docs/user-guide/query/Locks) used for transaction isolation.

A `BUF SH` latch is what identifies a [buffer read](/docs/user-guide/query/Reads#buffer-reads) - the page is already in memory, and the latch just pins it for the duration of the read. A latch suspend - the engine waiting for a latch to become available - is also the first step of a [disk read](/docs/user-guide/query/Reads#disk-reads), ahead of the file read and physical page read.

::: tip
Latches are held very briefly and in large numbers, so capturing them (via the [Events menu](/docs/user-guide/query#events-menu)) adds overhead to the trace. Turn them off if you don't need latch-level detail.
:::
