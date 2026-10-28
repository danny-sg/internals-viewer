# Scanning an index in key order

Show the user how SQL Server scans an index in order: go down one edge of the B-tree to the first leaf page, then follow the
leaf chain with the previous and next page pointers in each page header. Every tool call below changes what the user sees, so
narrate each one before you make it.

## Pick the edge

- Ascending (`ORDER BY` the key, or no order on a clustered index scan): go down the left edge and scan forward with the next
  page pointer.
- Descending (`ORDER BY` the key `DESC`, a backward scan): go down the right edge and scan backward with the previous page
  pointer.
- Say which one you chose and why. The key order of the index decides it, so an index on `(a DESC)` swaps them.

## Go down the edge

1. `list_tabs` and find the Index tab. If it is not open, `show_index` with the allocation unit id. `index_structure` gives the
   root and the number of levels.
2. `index_select_page` on the root, then `index_records`.
3. Take the first record for an ascending scan or the last record for a descending one. `index_select_row` on that slot, then
   `index_select_page` on its child page.
4. Repeat until the page is at height 0. No key comparison is needed, so say the edge costs one page per level.

## Scan the leaf chain

1. `index_records` on the leaf lists its rows in slot order. Read them from the first slot for ascending, from the last for
   descending, and `index_select_row` as you go when the user wants to follow along. A page of hundreds of rows is read as one
   block, so do not select every slot.
2. The page header holds the previous and next page. `index_nodes` with `parent` set to the level above, or `page_read` on the
   page, shows them. Follow next for ascending and previous for descending with `index_select_page` on that address.
3. Repeat. The chain ends where the pointer is 0:0, the last leaf for ascending and the first for descending.
4. Stop early when the user only wants a few pages, and say how many pages are left with the page counts from
   `index_structure`.

## Notes

- The chain is a doubly linked list at the leaf, so the non-leaf levels are not touched again after the descent.
- Next pages are often not the next physical page. `index_structure` counts the leaf pages out of physical order, which is the
  fragmentation that makes a scan jump around the file and defeats read-ahead.
- A scan in allocation order (an IAM scan, used when the order does not matter) skips the descent and the chain and reads the
  pages in file order. Use `allocations_extents` to show that order, and say which kind of scan it is.
- A nonclustered index leaf holds the key and a row locator, not the row.
