# Working with the Allocations tab

An Allocations tab draws every page of a database file as a map and colours it by owner. Use it to answer which object owns
which pages and what the allocation maps record about them.

## Read

- `allocations_files` lists the database files with their sizes.
- `allocations_objects` lists every allocation unit: tables and indexes with their id, type, pages and the first IAM, root and
  first pages. The names here are the ones the select tools accept.
- `allocations_page` answers who owns one page, the PFS byte and the GAM, SGAM, DCM and BCM bits of its extent.
- `allocations_extents` gives the page ranges an allocation unit owns, per file, from its IAM chain. Single page slots of a mixed
  extent are listed separately from whole extents.

## Drive the map

- `allocations_select_object` dims everything that is not that object. A table name that matches several indexes selects its heap
  or clustered index, so name a nonclustered index in full. Call it with no object to clear the selection.
- `allocations_scroll` scrolls to an object's first page or to a page.
- `allocations_overlay` shows one of Gam, SGam, Pfs, BufferPool, Dcm or Bcm over the map, or none when omitted. BufferPool needs a
  server connection.
- `allocations_search` filters the object list by text.

## Explaining

Start from the object: `allocations_objects`, then `allocations_extents` to say where it lives, then select it so the user sees
the map dim around it. For a page, `allocations_page` first and then `allocations_scroll` to it. To walk the object's index, open
it with `show_index` and use the index-seek and index-scan guides.