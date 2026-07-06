<script setup>
  import MarkerKey from '../components/MarkerKey.vue'
</script>

# Index Records

Index records use the same FixedVar layout as data records, with a smaller header and pointer fields for navigating the B-Tree.

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#193960">00</MarkerKey>|Status Bits A|Record type and status flags - whether the record has a null bitmap and variable length columns|
|<MarkerKey foreground="#ffffff" background="#8AB7BD">00</MarkerKey>|Null Bitmap|One bit per column, set to 1 if the column value is null. Only present if the index contains nullable columns|
|<MarkerKey foreground="#ffffff" background="#518183">00</MarkerKey>|Column Count|Number of columns in the record|
|<MarkerKey foreground="#ffffff" background="#606264">00</MarkerKey>|Variable Length Column Count|Number of variable length columns stored in the record|
|<MarkerKey foreground="#ffffff" background="#2D563A">00</MarkerKey>|Variable Length Column Offset Array|Two bytes per variable length column giving the offset where each value ends|
|<MarkerKey foreground="#00" background="#D6DAD4">00</MarkerKey>|Fixed Length Value|Fixed length key and included column values|
|<MarkerKey foreground="#00" background="#C2D0CB">00</MarkerKey>|Variable Length Value|Variable length key and included column values, located via the offset array|
|<MarkerKey foreground="#ffffff" background="#313240">00</MarkerKey>|Uniquifier|Value added to duplicate key values in a non-unique clustered index to make each key unique|
|<MarkerKey foreground="#ffffff" background="#313240">00</MarkerKey>|RID|Row Identifier in (File Id:Page Id:Slot Id) format - a direct pointer to a heap row, used by non-clustered indexes on heaps|
|<MarkerKey foreground="#ffffff" background="#313240">00</MarkerKey>|Down Page Pointer|Address of the page at the next level down in the B-Tree covering this record's key range. Present in records above the leaf level|
