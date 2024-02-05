<script setup>
  import MarkerKey from '../components/MarkerKey.vue'
</script>

# Data Records

## FixedVar Format

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#193960">00</MarkerKey>|Status Bits A
|<MarkerKey foreground="#ffffff" background="#2C5C5B">00</MarkerKey>|Status Bits B
|<MarkerKey foreground="#ffffff" background="#266AAE">00</MarkerKey>|Column Count Offset
|<MarkerKey foreground="#ffffff" background="#518183">00</MarkerKey>|Column Count
|<MarkerKey foreground="#ffffff" background="#8AB7BD">00</MarkerKey>|Null Bitmap
|<MarkerKey foreground="#ffffff" background="#606264">00</MarkerKey>|Variable Length Column Count
|<MarkerKey foreground="#ffffff" background="#2D563A">00</MarkerKey>|Variable Length Column Offset Array
|<MarkerKey foreground="#00" background="#D6DAD4">00</MarkerKey>|Fixed Length Value
|<MarkerKey foreground="#00" background="#C2D0CB">00</MarkerKey>|Variable Length Value
|<MarkerKey foreground="#ffffff" background="#d85240">00</MarkerKey>|Forwarding Stub

## CD Format

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Record Header
|<MarkerKey foreground="#ffffff" background="#518183">00</MarkerKey>|Column Count
|<MarkerKey foreground="#00" background="#B6F2D0">00</MarkerKey>|Column Descriptor
|<MarkerKey foreground="#ffffff" background="#345D7F">00</MarkerKey>|Short Data Cluster Array
|<MarkerKey foreground="#00" background="#BBD9E8">00</MarkerKey>|Short Field Value
|<MarkerKey foreground="#ffffff" background="#ab5384">00</MarkerKey>|Long Data Header
|<MarkerKey foreground="#ffffff" background="#DEBED0">00</MarkerKey>|Long Data Offset Count
|<MarkerKey foreground="#ffffff" background="#B28D8A">00</MarkerKey>|Long Data Offset Array
|<MarkerKey foreground="#ffffff" background="#735a6d">00</MarkerKey>|Long Data Cluster Array
|<MarkerKey foreground="#00" background="#e2bbe8">00</MarkerKey>|Long Field Value

## Sparse Vector

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Sparse Columns
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Sparse Column Offsets
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Sparse Column Count
|<MarkerKey foreground="#ffffff" background="#7ea597">00</MarkerKey>|Complex Header