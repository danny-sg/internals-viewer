<script setup>
  import MarkerKey from '../components/MarkerKey.vue'
</script>

# Data Records

## FixedVar Format

The standard row format for uncompressed pages. Fixed length columns are stored first at fixed offsets, followed by the variable length columns located via an offset array.

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#193960">00</MarkerKey>|Status Bits A|Record type and status flags - whether the record has a null bitmap, variable length columns, and versioning information|
|<MarkerKey foreground="#ffffff" background="#2C5C5B">00</MarkerKey>|Status Bits B|Additional status flags - currently only whether the record is a ghost forwarded record|
|<MarkerKey foreground="#ffffff" background="#266AAE">00</MarkerKey>|Column Count Offset|Two byte offset to the Column Count, marking the end of the fixed length data|
|<MarkerKey foreground="#ffffff" background="#518183">00</MarkerKey>|Column Count|Number of columns in the record|
|<MarkerKey foreground="#ffffff" background="#8AB7BD">00</MarkerKey>|Null Bitmap|One bit per column, set to 1 if the column value is null|
|<MarkerKey foreground="#ffffff" background="#606264">00</MarkerKey>|Variable Length Column Count|Number of variable length columns stored in the record|
|<MarkerKey foreground="#ffffff" background="#2D563A">00</MarkerKey>|Variable Length Column Offset Array|Two bytes per variable length column giving the offset where each value ends - each value starts where the previous one ended|
|<MarkerKey foreground="#00" background="#D6DAD4">00</MarkerKey>|Fixed Length Value|Fixed length column values, stored in column order at fixed offsets|
|<MarkerKey foreground="#00" background="#C2D0CB">00</MarkerKey>|Variable Length Value|Variable length column values, located via the offset array|
|<MarkerKey foreground="#ffffff" background="#d85240">00</MarkerKey>|Forwarding Stub|Left behind when a heap row is moved to another page. Contains the RID of the row's new location so non-clustered index pointers stay valid|

## CD Format

The CD (Compressed Data) format is used on pages with row or page compression. Instead of fixed offsets, a column descriptor encodes how each value is stored so no space is wasted.

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Record Header|Single byte identifying the record as CD format, with flags including whether the record has versioning information and a long data region|
|<MarkerKey foreground="#ffffff" background="#518183">00</MarkerKey>|Column Count|Number of columns in the record|
|<MarkerKey foreground="#00" background="#B6F2D0">00</MarkerKey>|Column Descriptor|Four bits per column describing how the value is stored - null, a short value of 0 to 8 bytes, a long value, or a page dictionary symbol|
|<MarkerKey foreground="#ffffff" background="#345D7F">00</MarkerKey>|Short Data Cluster Array|Columns are grouped into clusters of 30. One byte per cluster gives the combined size of its short values, so a value can be located without scanning every descriptor|
|<MarkerKey foreground="#00" background="#BBD9E8">00</MarkerKey>|Short Field Value|Values of 8 bytes or less, stored in column order in the short data region|
|<MarkerKey foreground="#ffffff" background="#ab5384">00</MarkerKey>|Long Data Header|Single byte header for the long data region at the end of the record|
|<MarkerKey foreground="#ffffff" background="#DEBED0">00</MarkerKey>|Long Data Offset Count|Number of entries in the Long Data Offset Array|
|<MarkerKey foreground="#ffffff" background="#B28D8A">00</MarkerKey>|Long Data Offset Array|Two bytes per long value giving the offset where each value ends|
|<MarkerKey foreground="#ffffff" background="#735a6d">00</MarkerKey>|Long Data Cluster Array|Cluster entries for the long data region, used to jump to the values for a cluster of columns|
|<MarkerKey foreground="#00" background="#e2bbe8">00</MarkerKey>|Long Field Value|Values longer than 8 bytes, stored in the long data region|

## Sparse Vector

Sparse column values are stored in a sparse vector - a structure at the end of the record that only stores the sparse columns that have a value.

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Complex Header|Two byte header identifying the complex column type - 5 is an in row sparse vector|
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Sparse Column Count|Number of sparse columns with a value stored in the vector|
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Sparse Columns|Array of two byte column ids identifying which sparse columns are stored|
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Sparse Column Offsets|Two bytes per column giving the offset where each value ends|

## LOB Pointers

When a value is stored off-row the data record holds a pointer structure in its place - a LOB pointer to the root of the value's LOB structure, or a row overflow pointer to a variable length value pushed off the page.

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#D84315">00</MarkerKey>|Pointer Type|The type of pointer - LOB pointer, LOB root, or row overflow|
|<MarkerKey foreground="#ffffff" background="#4E342E">00</MarkerKey>|Timestamp|Value linking the pointer to its LOB data, used by `DBCC CHECKTABLE` for consistency checks|
|<MarkerKey foreground="#ffffff" background="#A1887F">00</MarkerKey>|Level|Level of the LOB structure the root points into|
|<MarkerKey foreground="#000000" background="#D7CCC8">00</MarkerKey>|Update Seq|Update sequence number|
|<MarkerKey foreground="#ffffff" background="#FF8F00">00</MarkerKey>|Overflow Level|Level field of a row overflow pointer|
|<MarkerKey foreground="#ffffff" background="#FFA000">00</MarkerKey>|Overflow Length|Total length of the off-row value a row overflow pointer covers|
|<MarkerKey foreground="#ffffff" background="#313240">00</MarkerKey>|RID|Row Identifier (File Id:Page Id:Slot Id) of the LOB record the pointer leads to|
|<MarkerKey foreground="#000000" background="#ECECEC">00</MarkerKey>|Unused|Padding / unused bytes|

## LOB Records

Off-row values are stored in LOB records on LOB (Text/Image) pages. Small values sit in a single record, while larger values form a tree - a root record linking to data records, with internal records added as levels when the value grows.

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#193960">00</MarkerKey>|Status Bits A|Record type and status flags|
|<MarkerKey foreground="#ffffff" background="#2C5C5B">00</MarkerKey>|Status Bits B|Additional status flags|
|<MarkerKey foreground="#ffffff" background="#795548">00</MarkerKey>|Length|Length of the record|
|<MarkerKey foreground="#ffffff" background="#5D4037">00</MarkerKey>|Blob Id|Identifier shared by all the records that make up one LOB value - the in-row pointer carries the same id|
|<MarkerKey foreground="#ffffff" background="#9d481b">00</MarkerKey>|Blob Type|What part of the LOB structure the record is - Data (the value or a chunk of it), Internal (a record of links - the root and any intermediate levels of a MAX type's tree), SmallRoot (a small value held in the root record itself), or LargeRoot (a root of links used by other LOB formats)|
|<MarkerKey foreground="#ffffff" background="#8D6E63">00</MarkerKey>|Max Links|Maximum number of child links the record can hold|
|<MarkerKey foreground="#ffffff" background="#6D4C41">00</MarkerKey>|Current Links|Number of child links in use|
|<MarkerKey foreground="#0277BD" background="#E3F2FD">00</MarkerKey>|Level|Level of the record in the LOB tree|
|<MarkerKey foreground="#000000" background="#BCAAA4">00</MarkerKey>|Size|Size of the data held in a SmallRoot record|
|<MarkerKey foreground="#000000" background="#EFEBE9">00</MarkerKey>|Data|The LOB value bytes - the whole value in a SmallRoot, or one chunk in a Data record|
|<MarkerKey foreground="#000000" background="#FFB74D">00</MarkerKey>|Child Offset|Per child link, the cumulative offset into the value the link covers up to - a byte position can be found by picking the right link|
|<MarkerKey foreground="#000000" background="#FFE0B2">00</MarkerKey>|Child Length|Length covered by a child link|
|<MarkerKey foreground="#ffffff" background="#313240">00</MarkerKey>|RID|Row Identifier of the child record a link points to|
