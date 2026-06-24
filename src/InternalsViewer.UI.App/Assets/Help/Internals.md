# SQL Server Internals — a primer

This page explains the core storage concepts referenced throughout Internals Viewer. None
of it is required to use the application, but understanding these ideas makes the views
more meaningful. Each concept builds on the previous one.

> **Overview**
> A database appears as tables and rows when queried. Physically, it is a set of files
> containing fixed-size blocks. SQL Server's work is largely deciding **where to place
> data in those blocks** and **how to locate it again efficiently**. Internals Viewer
> exposes that machinery.

## Pages — the 8 KB building block

Everything SQL Server stores resides on a **page**: a fixed block of exactly **8 KB (8192
bytes)**. It is the smallest unit the engine reads or writes — to fetch a single row, SQL
Server reads the whole page that contains it.

Every page has the same structure: a **96-byte header** describing the page, a body of
data, and a **slot array** at the end recording where each row begins.

See the [Page view](Pages.md).

## Extents — pages in groups of eight

Managing pages individually would be inefficient, so SQL Server groups them. Eight
contiguous pages (8 × 8 KB = **64 KB**) form an **extent**, and space is usually allocated
an extent at a time.

- A **uniform** extent belongs entirely to one object.
- A **mixed** extent has its pages shared between several small objects.

## Allocation — tracking space

*Allocation* is the reservation of pages and extents for an object as it grows. SQL Server
tracks this with several **map pages**:

- **GAM** (Global Allocation Map) — which extents are free vs. used.
- **SGAM** (Shared GAM) — which mixed extents still have free pages.
- **PFS** (Page Free Space) — for each page, whether it is allocated and roughly how full.
- **IAM** (Index Allocation Map) — which extents belong to a particular table or index.

This is rendered as a map in the [Allocations / Database view](Allocations.md), and the
map pages themselves can be opened in the [Page view](Pages.md).

## Heaps, clustered and non-clustered indexes

How a table's rows are organised on its pages depends on its indexes:

- A **heap** is a table with no clustered index — rows are stored in no particular order.
- A **clustered index** stores the rows sorted by a key, so the index *is* the table.
- A **non-clustered index** is a separate structure holding the key plus a pointer back to
  the row.

## B-trees — how indexes locate rows

Indexes are organised as **B-trees**: a single **root** page at the top, optional
**intermediate** levels, and a **leaf** level at the bottom. To find a row, the engine
starts at the root and follows **down-page pointers** down the levels — only a few page
reads even in a large table. Pages at the same level are also chained left-to-right with
**previous/next pointers**, which makes range scans efficient.

See an index rendered as a tree in the [Index view](Indexes.md).

## Page addresses — `FileId:PageId`

A database can span several files, so a page is identified by which file and which page
within it, written `FileId:PageId` — for example `(1:9936)` is file 1, page 9936. This
notation appears throughout Internals Viewer, and in most places the address can be
clicked to open that page.

## Summary

| Concept | What it is | Where to see it |
| --- | --- | --- |
| Page | 8 KB storage block | [Pages](Pages.md) |
| Extent | 8 contiguous pages (64 KB) | [Allocations](Allocations.md) |
| Allocation maps | GAM / SGAM / PFS / IAM bookkeeping | [Allocations](Allocations.md), [Pages](Pages.md) |
| Heap / index | How rows are organised | [Allocations](Allocations.md), [Indexes](Indexes.md) |
| B-tree | The structure of an index | [Indexes](Indexes.md) |
| Execution | How a query uses the above | [Query](Query.md) |

These concepts are the foundation. Each view in Internals Viewer decodes further detail —
status flags, compression, row formats, RIDs — that builds on them.
