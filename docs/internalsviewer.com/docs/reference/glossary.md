# Glossary

Terms used in Internals Viewer and in SQL Server storage internals generally, in alphabetical order.

### Allocation Unit {#allocation-unit}

A set of pages belonging to one object, of one type of data. A table or index has up to three allocation units: `IN_ROW_DATA` (regular rows), `LOB_DATA` (large object values stored off-row), and `ROW_OVERFLOW_DATA` (variable length values pushed off-row when a row exceeds 8060 bytes). Each allocation unit has its own [IAM chain](#iam) and entry points.

### Anchor Record {#anchor-record}

Part of the [Compression Info](/docs/reference/compression) structure on a PAGE compressed page. Holds a per-column anchor value. Records on the page store only the difference from the anchor (column prefix compression).

### B-Tree {#b-tree}

The balanced tree structure used by SQL Server indexes. A single root page at the top, leaf pages at the bottom, and intermediate levels in between. All leaf pages are the same distance from the root, so any key can be found in the same small number of page reads.

### BCM (Bulk Changed Map) {#bcm}

Allocation page tracking which extents have been modified by minimally logged (bulk) operations since the last log backup. One bit per extent. Also called an ML (Minimally Logged) map page.

### Boot Page {#boot-page}

Page `(1:9)` in every database. Holds database-wide metadata, including the database version and the location of the system base tables.

### Buffer Pool {#buffer-pool}

SQL Server's in-memory cache of database pages. A page already in the buffer pool is read from memory with no physical I/O, which is why the Query view offers a Clear Buffer Pool option (`DBCC DROPCLEANBUFFERS`) to make page reads visible.

### CD (Compressed Data) Format {#cd-format}

The record format used on pages with row or PAGE compression. A 4-bit column descriptor per column encodes how each value is stored, so values take only the space they need. See [Data Records](/docs/reference/data-records#cd-format).

### Clustered Index {#clustered-index}

An index where the leaf level is the table data itself, stored in key order. A table can have only one clustered index, and the table only exists once - the clustered index _is_ the table.

### Clustering Key {#clustering-key}

The key column(s) of a clustered index. Non-clustered indexes on a clustered table use the clustering key to point back to the row.

### DAC (Dedicated Admin Connection) {#dac}

A special diagnostic connection to SQL Server that, among other things, allows the system base tables to be queried. Internals Viewer reads the base tables directly from the pages instead.

### DCM (Differential Changed Map) {#dcm}

Allocation page tracking which extents have changed since the last full backup, used by differential backups. One bit per extent.

### Down Page Pointer {#down-page-pointer}

The page address stored in an index record above the leaf level, pointing to the page at the next level down that covers the record's key range. An index seek follows down page pointers from the root to the leaf.

### Extent {#extent}

A group of eight contiguous pages (64 KB), the unit SQL Server uses to manage allocations. A _uniform_ extent belongs entirely to one object, while a _mixed_ extent can have pages from different objects (off by default since SQL Server 2016).

### Forwarding Stub / Forwarded Record {#forwarding-stub}

When a heap row is updated and no longer fits on its page, the row is moved and a forwarding stub is left at the original [RID](#rid) pointing to the new location. Non-clustered indexes keep pointing at the stub, avoiding a cascade of index updates.

### GAM (Global Allocation Map) {#gam}

Allocation page tracking which extents in a file are allocated, one bit per extent (1 = free, 0 = allocated). The first GAM is page `(1:2)` and each GAM covers about 4 GB of file, repeating at that interval.

### Ghost Record {#ghost-record}

A record that has been logically deleted but not yet physically removed. The delete marks the record as a ghost, and a background ghost cleanup task removes it later. The page header's Ghost Record Count tracks them.

### Heap {#heap}

A table without a clustered index. Heap pages are not linked together and have no order - the only way to find them is the [IAM chain](#iam). Rows in a heap are addressed by [RID](#rid).

### IAM (Index Allocation Map) {#iam}

Allocation page tracking which extents belong to a specific [allocation unit](#allocation-unit), one bit per extent. An IAM page covers about 4 GB of one file. Further ranges or files chain additional IAM pages together via the page header - the _IAM chain_. Internals Viewer's allocation map is a render of every object's IAM chain.

### Index Level {#index-level}

A page's level in the [B-Tree](#b-tree), stored in the page header. Level 0 is the leaf, and the root has the highest level.

### Leaf {#leaf}

The bottom level of an index. For a clustered index the leaf pages are the data pages. For a non-clustered index they contain the index keys plus a pointer to the row (clustering key or RID).

### LOB (Large Object) {#lob}

Data types that can exceed the size of a page, e.g. `VARCHAR(MAX)`, `NVARCHAR(MAX)`, `VARBINARY(MAX)`, `XML`. Large values are stored out of row on LOB (Text/Image) pages, with a LOB pointer left in the data record.

### Lock {#lock}

The mechanism SQL Server uses to isolate concurrent transactions, taken at row, page, or object granularity. The Query view's Lock timeline lane shows locks being acquired and released during a traced query.

### LSN (Log Sequence Number) {#lsn}

The unique, ever-increasing identifier of a transaction log record. Every page header stores the LSN of the last log record that modified it, which is how recovery knows whether a logged change has been applied to the page.

### Mixed Extent {#mixed-extent}

An extent whose pages can belong to different objects. Used for first allocations in older versions of SQL Server, and off by default since SQL Server 2016 (`MIXED_PAGE_ALLOCATION`).

### Non-Clustered Index {#non-clustered-index}

A separate [B-Tree](#b-tree) over one or more columns, with its own pages. Leaf records contain the index key plus a pointer back to the row - the [clustering key](#clustering-key) for a clustered table, or a [RID](#rid) for a heap.

### Null Bitmap {#null-bitmap}

Part of a record with one bit per column, set when the column value is null. Lets the engine skip reading storage for null values.

### Operator {#operator}

A step in an execution plan, e.g. Index Seek, Clustered Index Scan, Hash Match, Nested Loops. Each operator consumes rows from its inputs and produces rows for its parent. The Query view's Plan timeline lane shows when each operator was active during execution.

### Page {#page}

The fundamental 8 KB (8192 byte) unit of storage. Everything in a database - rows, index records, LOB data, allocation bitmaps, metadata - is stored in pages. Each page starts with a 96 byte [header](/docs/reference/page-header).

### Page Address {#page-address}

The location of a page, written as `(File Id:Page Id)`, e.g. `(1:704)`. The physical position in the file is Page Id × 8192 bytes. Anywhere Internals Viewer shows an address in this format it can be clicked to open the page.

### PFS (Page Free Space) {#pfs}

Allocation page tracking the status of every page: whether it is allocated, how full it is, and whether it is an IAM page or has ghost records. One byte per page, so a PFS page covers 8088 pages. The first is `(1:1)` and they repeat at that interval.

### Read-Ahead {#read-ahead}

The storage engine's optimisation of reading pages ahead of a scan in large chunks. Efficient, but it makes individual page reads harder to follow, which is why the Query view can disable it for a trace.

### RID (Row Identifier) {#rid}

The physical address of a row in a heap, in `(File Id:Page Id:Slot Id)` format. Used by non-clustered indexes on heaps - a RID lookup goes straight to the page and slot.

### Root Page {#root-page}

The single page at the top of an index [B-Tree](#b-tree), the entry point for index seeks. Listed as an entry point in the Allocation Info table.

### SGAM (Shared Global Allocation Map) {#sgam}

Allocation page tracking mixed extents with at least one free page, one bit per extent. The first is page `(1:3)`. Largely idle in modern databases where mixed extents are disabled.

### Slot {#slot}

A record's position on a page. The _slot offset array_ at the end of the page holds a two byte offset per record. The array defines the logical (key) order of the records regardless of their physical position on the page.

### Sparse Column {#sparse-column}

A column declared `SPARSE`, optimised for mostly-null data. Null sparse columns take no space at all, and non-null values are stored in a [sparse vector](/docs/reference/data-records#sparse-vector) at the end of the record.

### Torn Bits {#torn-bits}

Page header field used by page verification. With `TORN_PAGE_DETECTION` it holds a bit per 512 byte sector to detect partial writes. With the default `CHECKSUM` option it holds the page checksum.

### Uniform Extent {#uniform-extent}

An extent where all eight pages belong to the same object. The default since SQL Server 2016.

### Uniquifier {#uniquifier}

A value added to duplicate keys in a non-unique clustered index so every row has a unique key for non-clustered indexes to reference. Only stored on rows that are actually duplicates.

### Wait {#wait}

Time a query spends stopped, waiting for a resource - a page read to complete (`PAGEIOLATCH_*`), a lock held by another transaction, and so on. The Query view's Wait timeline lane shows waits occurring during a traced query.
