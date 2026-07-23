# Log Appliers

This page covers how Internals Viewer applies a transaction log record to a page image, for [Log Records](/docs/user-guide/query/LogRecords) in the Query view. It follows the source in `InternalsViewer.TransactionLog/Appliers`.

## Applying a record

Each log operation type (`LOP_INSERT_ROWS`, `LOP_MODIFY_ROW`, `LOP_DELETE_ROWS`, `LOP_SET_BITS`, `LOP_MODIFY_HEADER`, etc.) has a matching applier:

- `InsertRowsApplier`, `ModifyRowApplier`, `DeleteRowsApplier` - row-level operations
- `ModifyColumnsApplier` - column-level modifications within a row
- `SetBitsApplier` - single-bit changes, e.g. PFS byte flags
- `SetFreeSpaceApplier` - page header free space accounting
- `ModifyHeaderApplier` - other page header field changes

All of these derive from `PageLogRecordApplier<TRecord>`, which runs the checks common to every page-scoped record before handing off to the type-specific logic:

1. The record's target page address must match the page being viewed
2. The record's `PreviousPageLsn` must match the page's current LSN - i.e. the page is at the exact point in its history the record expects to be applied on top of
3. If both checks pass, the type-specific `ApplyRecord` runs, and on success the record's own LSN is stamped into the page header

A failure at any step surfaces as the reason shown in the Page Viewer - a page/LSN mismatch, or an operation-specific reason such as a splice that would change a record's length without the row rebuild/relocate logic to support it.

Each change made to the page image is recorded as a `ChangeSpan(Offset, Length, Description)` - this is what drives the Region/Change/Offset/Length rows in the [Page Log Operations](/docs/user-guide/query/LogRecords#page-log-operations) panel, and the hex viewer highlight when a change is selected.

## Splices vs page surgery

Where a change fits in the same space it previously occupied, it is applied as a same-size **splice** - the byte range is overwritten in place, after verifying the page's current bytes match the log record's before-image.

Where a row changes size - an update that grows or shrinks a variable-length column - the applier instead rebuilds the row and places it:

- If the row is shrinking, or it's the last row before the free data offset, it's rewritten in place
- Otherwise it's relocated to the current free data offset, and the slot's offset table entry is repointed to the new location, leaving the old bytes behind as a hole in the page

This is the same mechanic SQL Server itself uses - a growing row that doesn't fit in place gets moved within the page rather than triggering a page split for every update.
