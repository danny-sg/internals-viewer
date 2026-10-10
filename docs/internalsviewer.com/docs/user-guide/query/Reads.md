# Reads

The Read band shows read operations - the database retrieving pages. It has two lanes:

- **Buffer** - pages retrieved from the [Buffer Pool](/docs/user-guide/allocations#buffer-pool) (memory)
- **Disk** - pages retrieved from disk

Each read shows its duration. **Click** a read once to select it in the Events pane. **Double-click** to open the page in the [Page Viewer](/docs/user-guide/page-viewer).

A rail connects each read to what caused it - the operator's bar on the Plan band, or for a columnstore scan the [object pool](/docs/user-guide/query/Timeline#columnstore) miss it served.

Reads of allocation pages - IAM pages and the database's own pages such as PFS - are drawn in grey with no rail, since they are the engine finding its way rather than the query's data.

## Buffer reads

A buffer read means the page was already in memory - identified in the trace by a `BUF SH` latch, acquired to pin the page in the buffer pool while it is read.

When the playhead passes a buffer read on a run with **Clear Buffer Pool** on, it flags briefly on the [Allocations](/docs/user-guide/query/Allocations) pane - the flag is transient, representing "this page was accessed, but the access is ephemeral". Without Clear Buffer Pool most pages start in memory, so buffer reads are the query's real reads, and they stay marked from the read to the end of the query, as disk reads do. The Query view's Index panes work the same way.

## Disk reads

A disk read means the page had to be physically read from the data file. The sequence in the trace is a latch suspend, then a file read, then the physical page read completing.

When the playhead passes a disk read it persists on the [Allocations](/docs/user-guide/query/Allocations) pane, representing "this page has been read from disk and loaded" - unlike a buffer read, this marker stays.

### Contiguous vs scatter/gather

A file read can cover a single page (**Contiguous**) or several pages in one I/O (**Scatter/Gather**). Multi-page reads are labelled `Read (Disk): n pages from (start page address)` in the Events pane, where a single-page read just shows the page address.

Whether reads are contiguous or scatter/gather depends on the query, the underlying table or index type, and whether the [Disable Read-Ahead](/docs/user-guide/query/Editor) option is used - though even with read-ahead disabled, scatter/gather reads can still occur depending on the access pattern.

### Reads per extent

SQL Server will access pages on an extent basis, loading the eight pages of an extent together. Because of this, more pages can appear to be accessed than the query's I/O statistics report. Internals Viewer pushes the root page read to the start of an extent read, but subsequent index levels may appear out of order as their pages were already read as part of an earlier extent.
