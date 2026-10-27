# Working with the Page viewer

A Page tab shows one page: its header, slots and records in a hex viewer. `page_read` reads that page as text. The other `page_`
tools change what the user sees, so say what you are about to select before you select it.

## Open and read

1. `show_page` opens a page in a new Page tab, or in the Query tab's page dock when a Query tab is the target. `page_goto` loads
   another page into a Page tab that is already open.
2. `page_read` has no page parameter. It reads the page that tab shows, so open the page first.
3. `get_selection` shows what is selected: the slot, the marker (field or structure) with its offset and length, the view and the
   structures that can be selected.

## Select

- `page_select_slot` selects a slot, the hex viewer scrolls to it and its fields become the markers.
- The page header, the offset table and other structures are pseudo slots with negative numbers that differ by page type. Take
  them from the Structures line of `get_selection` and never assume a number. They are not slot numbers in the page.
- `page_select_marker` selects a field or structure by name, for example `Null Bitmap` or `Slot 0` (an offset table entry).
  Names come from `get_selection` and `page_read`.
- `page_scroll` moves the hex viewer to a byte offset, 0 to 8191, without changing the selection.
- `page_view` switches between Markers, Allocations, Pfs and Data. Only the views the page type has are available.
- `page_open_index` opens the index the page belongs to, from its root. It only works for index pages.

## Reading a row

A selected marker reports its offset and length in bytes. A fixed-length record starts with two status bytes and the end of the
fixed-length data, then the fixed columns, the column count, the null bitmap, the variable column count and offsets, and the
variable data. The offset table sits at the end of the page and grows backwards, so slot 0 is the last two bytes (8190-8191).
Select the pieces in that order when you explain a row, so the user sees each byte range.