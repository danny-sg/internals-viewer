# Log Records

Trace mode captures the transaction log records behind data modification queries, and lets them be applied to a page one at a time to see exactly what changed. See [Log Records](/docs/user-guide/query/LogRecords) for how this works and what each field in the Log Operations panel means.

This part deletes a single row from `dbo.HeapTable` and watches the delete happen at the byte level - on the heap page itself, on its non-clustered index, and on the PFS page that tracks it.

## Step 1 - Delete a row

With **Trace** on in the SQL Editor, run:

```SQL
DELETE FROM dbo.HeapTable WHERE NumberField = 100
```

This is the first row inserted back in [Part 1](/docs/tutorial/1-connecting-and-allocations), so it's easy to recognise afterwards.

## Step 2 - The index delete

Find the delete event for `IX_HeapTable_NumberField` on the timeline and double-click it to open the index page in the Page Viewer. Two log operations show in the **Log Operations** panel - the index page first, since the non-clustered index is checked before the heap.

Tick the operation. It's a `LOP_DELETE_ROWS` / `MARK_AS_GHOST` - rather than physically removing the row, it flips a bit in the record's **Status Bits A** byte marking it as a ghost, and increments the page header's **Ghost Record Count**. The row's bytes are still sitting right there on the page - only its status has changed.

## Step 3 - The PFS update

A page that gains a ghost record needs to be flagged for cleanup, which means a PFS update - `LOP_SET_BITS` / `PFS`. Open the PFS page covering this page's range (double-click the PFS event, or type its address into the Page Viewer) and switch to the **PFS** tab.

Applying the operation selects the affected page on the PFS map and shows its ghost icon appear - the single byte change that marks the page as containing ghost records, the same icon seen on the [Allocations](/docs/user-guide/allocations#pfs-page-free-space) PFS overlay.

## Step 4 - The heap delete

Back on the heap page itself, the delete is a `LOP_DELETE_ROWS` / `HEAP` operation - applied differently again. A heap row has no ghost state to flip. Instead the operation zeroes out the row's slot offset table entry, so nothing points at the row's bytes any more even though they're still physically present on the page.

## The point

None of these three operations actually erased anything. A delete is, at the byte level, a handful of very cheap flag flips and pointer changes - marking rows as ghosts, zeroing slot entries, updating a PFS byte. The [Ghost Cleanup](https://learn.microsoft.com/en-us/sql/relational-databases/ghost-row-cleanup-process-guide) background task is what does the real work later, using exactly the PFS ghost record flag from Step 3 to find which pages need attention.

::: tip Try it yourself
The same three-step pattern - index ghost, PFS flag, heap slot zero - plays out for clustered indexes and updates too, with one difference worth comparing: an update that grows a row past its slot's free space can't just flip a bit or zero a pointer, and has to relocate the row instead. See [Log Appliers](/docs/reference/log-appliers#splices-vs-page-surgery) for how Internals Viewer reconstructs that case.
:::

Next: [Part 5 - LOB data](/docs/tutorial/5-lob-data)
