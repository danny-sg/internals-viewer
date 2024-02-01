<script setup>
  import MarkerKey from '../components/MarkerKey.vue'
</script>

# Data Records

## FixedVar Format

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#193960">00</MarkerKey>|Status Bits A
|<MarkerKey foreground="#ffffff" background="#8AB7BD">00</MarkerKey>|Null Bitmap
|<MarkerKey foreground="#ffffff" background="#518183">00</MarkerKey>|Column Count
|<MarkerKey foreground="#ffffff" background="#606264">00</MarkerKey>|Variable Length Column Count
|<MarkerKey foreground="#ffffff" background="#2D563A">00</MarkerKey>|Variable Length Column Offset Array
|<MarkerKey foreground="#00" background="#D6DAD4">00</MarkerKey>|Fixed Length Value
|<MarkerKey foreground="#00" background="#C2D0CB">00</MarkerKey>|Variable Length Value
|<MarkerKey foreground="#ffffff" background="#313240">00</MarkerKey>|Uniquifier
|<MarkerKey foreground="#ffffff" background="#313240">00</MarkerKey>|RID
|<MarkerKey foreground="#ffffff" background="#313240">00</MarkerKey>|Down Page Pointer
