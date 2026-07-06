<script setup>
  import MarkerKey from '../components/MarkerKey.vue'
</script>

# Page Header

Every page starts with a 96 byte header describing the page and its place in the database.

|Key|Name|Description|
|---|----|-----------|
||Allocation Unit Id|The allocation unit the page belongs to. Not stored directly in the header - derived from the Internal Object Id and Internal Index Id via the allocation metadata|
|<MarkerKey foreground="#D50000" background="#EDE7F6">00</MarkerKey>|Page Address|Address of this page in (File Id:Page Id) format|
|<MarkerKey foreground="#C51162" background="#E3F2FD">00</MarkerKey>|Page Type|What the page is used for, e.g. Data, Index, LOB, IAM, PFS, GAM|
|<MarkerKey foreground="#AA00FF" background="#EDE7F6">00</MarkerKey>|Next Page|Address of the next page at the same index level. `(0:0)` means no next page. Only maintained for index levels - heap pages are not linked|
|<MarkerKey foreground="#6200EA" background="#EDE7F6">00</MarkerKey>|Previous Page|Address of the previous page at the same index level. `(0:0)` means no previous page|
|<MarkerKey foreground="#304FFE" background="#BBDEFB">00</MarkerKey>|Internal Object Id|Internal id of the object the page is allocated to|
|<MarkerKey foreground="#2962FF" background="#BBDEFB">00</MarkerKey>|Internal Index Id|Internal id of the index the page is allocated to within the object|
|<MarkerKey foreground="#0277BD" background="#E3F2FD">00</MarkerKey>|Index Level|Level of the page in the index B-Tree. 0 is the leaf level, the root has the highest level|
|<MarkerKey foreground="#004D40" background="#E3F2FD">00</MarkerKey>|Slot Count|Number of slots (records) on the page, including ghost records|
|<MarkerKey foreground="#00C853" background="#E3F2FD">00</MarkerKey>|Fixed Length Size|Size in bytes of the fixed length portion of the records stored on the page|
|<MarkerKey foreground="#1B5E20" background="#E3F2FD">00</MarkerKey>|Free Count|Number of free bytes on the page|
|<MarkerKey foreground="#F9A825" background="#E3F2FD">00</MarkerKey>|Free Data Offset|Offset of the first byte after the end of the record data - where the next record would be written|
|<MarkerKey foreground="#827717" background="#E3F2FD">00</MarkerKey>|Reserved Count|Number of bytes reserved by active transactions, e.g. space freed by deletes that have not yet committed|
|<MarkerKey foreground="#FF6D00" background="#E3F2FD">00</MarkerKey>|Transaction Reserved|The number of bytes of Reserved Count reserved by the most recently started transaction|
|<MarkerKey foreground="#DD2C00" background="#E3F2FD">00</MarkerKey>|Torn Bits|Page verification information - torn page protection bits or the page checksum, depending on the database `PAGE_VERIFY` option|
|<MarkerKey foreground="#212121" background="#E3F2FD">00</MarkerKey>|Flag Bits|Bit flags describing the page, including which page verification type is in use|
|<MarkerKey foreground="#263238" background="#E3F2FD">00</MarkerKey>|LSN (Log Sequence Number)|LSN of the last log record that modified the page, used by recovery to decide if a logged change needs to be applied|
|<MarkerKey foreground="#455A64" background="#E3F2FD">00</MarkerKey>|Header Version|Version of the page header format - currently always 1|
|<MarkerKey foreground="#546E7A" background="#E3F2FD">00</MarkerKey>|Ghost Record Count|Number of ghost records on the page - records logically deleted but not yet physically removed by the ghost cleanup task|
|<MarkerKey foreground="#546E7A" background="#E3F2FD">00</MarkerKey>|Type Flag Bits|Bit flags with a meaning specific to the page type|
|<MarkerKey foreground="#6D4C41" background="#E3F2FD">00</MarkerKey>|Internal Transaction Id|Internal id of the most recent transaction to add to the reserved byte count|
