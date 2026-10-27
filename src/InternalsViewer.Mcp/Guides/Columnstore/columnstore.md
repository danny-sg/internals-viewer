# Working with the Columnstore tab

A Columnstore tab opens one columnstore index. Its Structure document shows the row groups and its Metadata document lists the
segments and dictionaries. Opened segments, dictionaries, delta stores and delete bitmaps each get their own document.

## Read

- `columnstore_rowgroups` lists the row groups with state, rows, deleted rows, size and when each was created and closed.
- `columnstore_segments` lists one segment per column per compressed row group, with encoding, rows, size, nulls, the min and max
  data id and its dictionaries.
- `columnstore_dictionaries` lists the global dictionary of each column and the local dictionaries of its segments.
- `get_selection` lists the open documents, the regions and the markers that can be selected.

## Drive

- `show_columnstore` opens the index in a new Columnstore tab from an allocation unit id or a name.
- `columnstore_open` opens a Segment, Dictionary, DeltaStore or DeleteBitmap in its own document, or selects it when it is open.
  Segment needs `rowGroup` and `column`, Dictionary needs `column` and for a local one `dictionary`, DeltaStore needs `rowGroup`.
  Take the ids from the read tools.
- `columnstore_show` brings an open document forward by name. `columnstore_metadata_tab` switches Metadata between Segments and
  Dictionaries.
- `columnstore_region` switches the region tab of an open segment or dictionary and moves its hex view there.
- `columnstore_select_marker` selects a marker in the hex view by name. `columnstore_select_row` selects a row of a segment or an
  entry of a dictionary and moves the hex view to where it is stored.

## Explaining

Go from the row group to its segment to the bytes: open the segment, switch to the region that holds the data, then select a row
to show where its value is stored. A segment's data ids point into a dictionary, so open the dictionary beside it when the
encoding is a dictionary encoding. The `query_columnstore` tool shows what a query actually read from these structures.