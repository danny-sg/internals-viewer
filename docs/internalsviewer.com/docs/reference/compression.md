<script setup>
  import MarkerKey from '../components/MarkerKey.vue'
</script>

# Compression

## Compression Info

Pages compressed with PAGE compression have a CI (Compression Info) record after the page header, holding the structures shared by the records on the page.

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#06411a">00</MarkerKey>|Header|Single byte with flags for which structures are present - Has Anchor Record and Has Dictionary|
|<MarkerKey foreground="#ffffff" background="#8EBC49">00</MarkerKey>|Page Modification Count|Number of changes to the page since it was compressed, used to decide when the page is worth recompressing|
|<MarkerKey foreground="#ffffff" background="#26994C">00</MarkerKey>|Size|Total size of the compression info structure including the dictionary. Only present when the page has a dictionary|
|<MarkerKey foreground="#ffffff" background="#26994C">00</MarkerKey>|Length|Length of the compression info structure up to the end of the anchor record - the dictionary starts at this offset|
|<MarkerKey foreground="#00" background="#ECECEC">00</MarkerKey>|Anchor Record|A CD format record holding an anchor value per column. Records on the page store only the difference from the anchor value (column prefix compression)|
|<MarkerKey foreground="#00" background="#ECECEC">00</MarkerKey>|Dictionary|Values shared across the page (page dictionary compression). Records reference a dictionary entry by symbol instead of repeating the value|

## Dictionary

|Key|Name|Description|
|---|----|-----------|
|<MarkerKey foreground="#ffffff" background="#06411a">00</MarkerKey>|Entry Count|Number of entries in the dictionary|
|<MarkerKey foreground="#ffffff" background="#06411a">00</MarkerKey>|Dictionary Entry Offset Array|Two bytes per entry giving the offset where each dictionary value ends|
|<MarkerKey foreground="#ffffff" background="#06411a">00</MarkerKey>|Dictionary Value|The shared values, referenced from column descriptors as symbols|
