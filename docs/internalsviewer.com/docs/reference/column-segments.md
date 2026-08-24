# Column Segments

A column segment is the compressed values of one column for one row group, stored as a single LOB in the
columnstore's blob allocation unit. `sys.column_store_segments.data_ptr` locates it.

Everything below is reverse engineered from segments produced by SQL Server 2025, cross checked against
`DBCC CSINDEX` output and verified by decoding values back to the rows they came from. Where something is
unexplained it says so.

## Reading the arrays

Most of the difficulty in parsing a segment is that its arrays are each counted in a different way, and two of the
header fields are named after entries when they describe something else. Get these ideas straight first and the
rest follows.

### Counts, units and widths are three different things

|Idea|Meaning|
|----|-------|
|Count|How many things there are, or in one case how many units were set aside|
|Unit|A fixed size block the format counts in, **not** the size of one entry|
|Width|How many bytes one entry actually occupies|

For every array except the bookmarks the count is in units and the width has to be found some other way. The
number of entries is `count * unitSize / width`.

### The granularities

|Field|Counted in|Size|
|-----|----------|----|
|`Bookmark Count`|Bookmarks|8 bytes each|
|`RLE Array Count`|Native units|8 bytes each|
|`Bitpack Unit Count`|Bit pack units|8 bytes, 64 bits, each|
|Bookmark `Position`|32 bit words|4 bytes each|

`DBCC CSINDEX` is explicit about one of these, printing `RLE Array Count (In terms of Native Units)`. The bookmark
`Position` is the only quantity measured in four byte words, and it is the one most easily misread as a byte
offset.

## Blob layout

Both layouts share a header, a bookmark array and an RLE array. They differ only in what the runs point at, which
is what `RLE Type` selects.

```
Segment Header
Bookmark Array
RLE Array
    RLE Type = Bit Pack (3)   Bit Pack Array
    RLE Type = VLD (7)        VLD Header
                              Page Size Array
                              VLD Page [0..n]
```

The bookmark array starts at `+0x30` on a Bit Pack segment and `+0x32` on a VLD one. The two extra bytes are
always zero, belong to no field we have identified, and leave the bookmark array **not** eight byte aligned, so
alignment is not what they are for.

## Segment header

48 bytes, present in full on both types. A VLD segment still carries the bit pack fields, it just does not use
them.

|Offset|Size|Field|Notes|
|------|----|-----|-----|
|`+0x00`|4|Version|Always 1|
|`+0x04`|4|Lob Type|The blob's own kind. 1 segment, 2 numeric dictionary, 3 string dictionary|
|`+0x08`|4|Reserved|Always 0|
|`+0x0C`|4|Unknown|Always `0x7FFF` on every segment measured. Purpose unknown|
|`+0x10`|4|RLE Type|3 Bit Pack, 7 Variable Length Data|
|`+0x14`|4|Bookmark Count|Means something different on a VLD segment, see below|
|`+0x18`|4|Bookmark Distance|Rows between bookmarks|
|`+0x1C`|4|RLE Array Count|Eight byte units, **not** entries|
|`+0x20`|2|RLE Array Entry Size|Always 8. The size of a native unit, **not** the width of an entry|
|`+0x22`|2|Bitpack Entry Size|Bits per bit packed value|
|`+0x24`|4|Bitpack Unit Count|64 bit units|
|`+0x28`|8|Bitpack Min Id|Base the bit packed values are stored relative to|

`DBCC CSINDEX` reports both `+0x04` and `+0x10` as a lob type, distinguished only by a space in the name. They are
unrelated. `+0x04` says what kind of blob this is, `+0x10` says how its runs are encoded.

## Bookmark array

An entry is 8 bytes and means the same thing on both RLE types.

|Offset|Size|Field|
|------|----|-----|
|`+0x00`|4|Position|
|`+0x04`|4|End Row|

`Position` is a **count of 32 bit words** from the start of the RLE array:

```
byteOffset = Position * 4
entryIndex = Position * 4 / rleEntryWidth
```

With 8 byte entries a position steps 0, 2, 4 per entry. With 16 byte entries it steps 0, 4, 8. Every position
observed lands on an entry boundary, so the word granularity is finer than anything uses. It makes the field
independent of the entry width.

`End Row` is the row at which that entry ends, equivalently the row the next run starts at.

Bookmarks save the scan an RLE array would otherwise need. To find the value at a row:

```
bookmarkIndex = rowId / Bookmark Distance
```

then walk forward from that entry, adding run counts, until `rowId < endRow`. On a Bit Pack segment the division
needs no clamp, `Bookmark Count` being `ceil(rowCount / distance)` or one more.

### The VLD overcount

On a VLD segment the RLE array begins **16 bytes before** `bookmarkArrayOffset + Bookmark Count * 8`, so the last
two bookmark slots hold RLE entries 0 and 1. Both halves of that are measured: the declared count is exactly the
number of intervals the rows need, and the final two slots are byte for byte identical to the first two RLE
entries.

So a VLD segment has `Bookmark Count - 2` usable bookmarks and its last two intervals have none. A reader falls
back to the previous bookmark and scans further, so nothing breaks.

## RLE array

Every segment has one. It maps rows to values, and what a run points at is what `RLE Type` decides.

### Length and width

```
arrayBytes = RLE Array Count * 8
entryCount = arrayBytes / entryWidth
```

`entryWidth` is 8 or 16 bytes and **is not recorded anywhere in the blob**. `RLE Array Entry Size` reads 8 even on
a segment whose entries are 16 bytes.

It comes from the segment's catalog metadata instead:

```
scaled    = base_id >= 0 AND magnitude > 0
storedMax = (max_data_id / magnitude) - base_id
width     = scaled AND storedMax > 2147483647 ? 16 : 8
```

A literal run holds the data id relative to the base, in a **signed** field whose negatives mean a read run. Only
31 bits are usable, so an id past `int.MaxValue` forces the wider entry. A dictionary encoded segment leaves
`base_id` and `magnitude` at -1 and stores slot numbers, which are always small, so the guard matters. Without it
every dictionary segment is wrongly predicted wide.

A cross check: a run count can never be negative, so reading a 16 byte array as 8 byte entries splits a value in
half and produces a negative count.

### Entry layouts

8 bytes:

|Offset|Size|Field|
|------|----|-----|
|`+0x00`|4|Value, signed|
|`+0x04`|4|Run Count|

16 bytes:

|Offset|Size|Field|
|------|----|-----|
|`+0x00`|8|Value, signed|
|`+0x08`|4|Run Count|
|`+0x0C`|4|Run Kind. 1 read, 0 repeat and terminator|

The 8 byte entry keeps the run kind in the **sign bit** of its value. The 16 byte entry has no spare bit, the value
taking all 64, so the kind gets a field of its own. It is redundant with the sign in every segment measured.

### Run kinds

|Kind|Sign|Covers|Consumes|
|----|----|------|--------|
|Repeat|Value >= 0|`Run Count` rows, all reading one value|1 value|
|Read|Value < 0|`Run Count` rows, reading consecutive values|`Run Count` values|
|Terminator|Value 0, Count 0|Nothing|Nothing|

A repeat run holds its value differently by type. On a Bit Pack segment the data id is **inline** in the entry,
because it fits. On a VLD segment it is an **address**, because a wide value does not.

Slot consumption summed over the array equals the store's own value count, which is the arithmetic that proves the
addresses are real.

### What a run points at

On a **Bit Pack** segment a read run's value is `-index - 1`, where `index` is the ordinal of the first bit packed
value the run covers. Row *n* of the run reads `bitpack[index + n]`.

On a **VLD** segment both kinds carry a page and slot address:

|Bits|Field|
|----|-----|
|31|Read run|
|30|Repeat run|
|15-29|Slot, the value's ordinal within its page|
|0-14|Page, an index into the Page Size Array|

The store ordinal is `pageStart[page] + slot`, where `pageStart` accumulates the value counts of the preceding
pages. A read run continues across page boundaries without another entry. The address is only where it starts.

It is an address rather than a flat ordinal because each VLD page is compressed on its own. A flat ordinal would
force a walk of the page size array just to work out which page to expand.

::: warning
The address looks like `ordinal << 15` because pages usually hold a uniform 3640 values, making the page 0 and the
slot the ordinal for anything small. A segment with uneven pages shows the difference. One with pages of 5462,
5461 and 5079 values has `0x49EB0002` as page 2 slot 5078, ordinal 16001.
:::

### Minimum run length

A run is only worth its 8 bytes if it covers at least **64 rows**. Putting one group of every size from 1 to 400
into a row group produced runs for 64 to 400 and nothing shorter, the remainder going to the bit pack array.
Values repeated only twice produce no runs at all.

## Bit pack array

Present when `RLE Type` is Bit Pack and `Bitpack Unit Count` is above zero.

```
arrayBytes    = Bitpack Unit Count * 8
valuesPerUnit = 64 / Bitpack Entry Size
valueCount    = valuesPerUnit * Bitpack Unit Count
```

Values are packed into 64 bit units and never straddle one, so remainder bits at the top of a unit go unused. That
is why the entry size is always a value of `floor(64/k)`: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 16, 21, 32, 64.

A stored value is the data id minus `Bitpack Min Id`. Literal RLE values are **not** offset this way.

## Variable length data

Used when values are too wide to bit pack, meaning anything variable width or fixed width above 8 bytes.

```
VLD Header       24 bytes, value count and max string size
Page Size Array  element size and count, then one length per page
VLD Page [0..n]  each page's length comes from that array
```

### VLD page

|Compression|Header|Contents|
|-----------|------|--------|
|0|12 bytes|Values directly|
|1|14 bytes|Xpress Huffman payload that expands to the values|

The extra two bytes on a compressed page are `Payload Size` at `+0x0C`, which exists only when there is a payload
to size. The low nibble of the byte at `+0x04` selects the compression, so a page declares its own shape.

|Offset|Size|Field|
|------|----|-----|
|`+0x00`|4|Sub Lob Type, 9 for a value page|
|`+0x04`|1|Flags. Low nibble compression, high nibble unexplained|
|`+0x05`|1|Reserved|
|`+0x06`|2|Value Size. Negative means variable width|
|`+0x08`|4|Value Count|
|`+0x0C`|2|Payload Size, compressed pages only|

### Locating a value on a page

Fixed width, `Value Size` positive:

```
offset = slot * Value Size
```

Variable width, `Value Size` negative, uses a **reverse offset array** in the last `Value Count * 2` bytes of the
expanded values:

```
entryPosition = length - (slot + 1) * 2
```

Slot 0 is the last two bytes, slot 1 the two before it, laid out exactly as a data page's slot array is. Three
differences matter:

- It sits at the end of the **expanded payload**, so on a compressed page it does not exist until decompressed.
- It is a boundary array rather than offset plus length. A value ends where the next starts, and the last ends at
  the offset array itself.
- `0xFFFE` means null. A null has no offset, so finding a value's end means skipping forward past any nulls.

A page is fixed or variable width independently of its neighbours and a store can mix them. Nulls force a page to
be variable width, a fixed width page having no way to say a slot has no bytes.

## Worked examples

A bigint column, 1,048,576 rows, encoding 4:

```
RLE Array Count 4, RLE Array Entry Size 8   array is 32 bytes
base_id 0, magnitude 1, max_data_id 3,167,592,081
storedMax 3,167,592,081 > int.MaxValue      entries are 16 bytes
32 / 16                                     2 entries
```

Which is what `DBCC CSINDEX` prints, a read run and a terminator. Read as 8 byte entries it gives four, the second
of which has a run count of -1.

A datetime column, 20,000 rows, encoding 1:

```
RLE Array Count 2, RLE Array Entry Size 8   array is 16 bytes
base_id 188,244,121,616,681, magnitude 1, max_data_id 188,244,127,616,384
storedMax 5,999,703 < int.MaxValue          entries are 8 bytes
16 / 8                                      2 entries
```

Both have two entries. Only the unit count differs, because the width does.

## Open questions

- `+0x0C` is `0x7FFF` on every segment measured, spanning 13 data types and all five encodings.
- The two bytes between a VLD header and its bookmark array are always zero and break eight byte alignment.
- The high nibble of a VLD page's `Flags` varies but nothing yet correlates with it.
- A VLD segment's bookmark count and RLE array both lay claim to the same 16 bytes.
