# Seeking a key through an index

Show the user how SQL Server finds one key by walking the B-tree in the Index tab, one visible step at a time. Every tool call
below changes what the user sees, so narrate each one before you make it.

## Before you start

1. `list_tabs` and find the Index tab for the index. If it is not open, `show_index` with the allocation unit id (get it from
   `allocations_objects`).
2. `index_structure` gives the root page and the number of levels. Say how many pages the seek will touch, one per level.
3. The key column and its order are on the leaf rows. Use the clustering key for a clustered index.

## At each level

1. `index_select_page` on the page, starting with the root. For the root use `index_zoom` first when the user needs to see it.
2. `index_records` lists the page's records in slot order, with the key values and the child page each one points to.
3. Binary search the slots for the last record whose key is less than or equal to the target.
   - Probe visibly: before reading each probe, `index_select_row` on the slot you are comparing, and say what you compared and
     which half is left.
   - The first record of a non-leaf page holds no real key. It is the lowest possible value and covers everything below the
     second record.
   - Stop when the probed key is the last one at or below the target. That record is the one to follow.
4. `index_select_row` on that record, then `index_select_page` on its child page. That is the next level.
5. Repeat until the page is at height 0, the leaf.

## At the leaf

Binary search the slots the same way, comparing the target with the row's key. The leaf holds the actual rows, so when a slot's
key equals the target the seek is done.

1. When the key matches, `show_page` the leaf and `page_select_slot` on the slot. `page_select_marker` highlights a column.
2. When no slot matches, the key does not exist. Say which two neighbouring keys it would sit between.

## Notes

- The number of reads is the number of levels, not the number of rows. Say so at the end with the levels walked.
- `index_records` shows the selected page only. Select the page before reading it, and do not guess a page from row counts.
- A nonclustered index leaf holds the key and a row locator, not the row. Say that a lookup into the clustered index or heap
  follows, and offer to continue it.
