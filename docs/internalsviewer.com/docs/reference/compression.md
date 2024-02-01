<script setup>
  import MarkerKey from '../components/MarkerKey.vue'
</script>

# Compression

## Compression Info

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#06411a">00</MarkerKey>|Header
|<MarkerKey foreground="#ffffff" background="#8EBC49">00</MarkerKey>|Page Modification Count
|<MarkerKey foreground="#ffffff" background="#26994C">00</MarkerKey>|Size
|<MarkerKey foreground="#ffffff" background="#26994C">00</MarkerKey>|Length
|<MarkerKey foreground="#00" background="#ECECEC">00</MarkerKey>|Anchor Record
|<MarkerKey foreground="#00" background="#ECECEC">00</MarkerKey>|Dictionary

## Dictionary

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#06411a">00</MarkerKey>|Entry Count
|<MarkerKey foreground="#ffffff" background="#06411a">00</MarkerKey>|Dictionary Entry Offset Array
|<MarkerKey foreground="#ffffff" background="#06411a">00</MarkerKey>|Dictionary Value
