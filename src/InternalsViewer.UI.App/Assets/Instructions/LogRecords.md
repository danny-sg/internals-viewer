# Log Records

With **Trace** on in the [SQL Editor](view:SqlEditor), a data modification query (INSERT / UPDATE / DELETE) runs
inside a transaction, its transaction log records are captured, and the transaction is rolled back - so the data is
left unchanged.

The captured log records appear on the timeline, and opening a page the query changed shows a **Log Operations**
panel in the Page Viewer:

- Tick an operation to apply it to the page and see exactly which bytes changed
- Operations apply in order - ticking one applies everything it depends on first
- Untick to step back and compare the page before and after

Try it: delete a single row, double-click the delete event on the timeline, and watch the delete happen at the byte
level - a ghost bit flipped on the index record, a PFS flag set, and a slot entry zeroed on the heap, with nothing
actually erased.
