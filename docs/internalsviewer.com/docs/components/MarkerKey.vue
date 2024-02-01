<template>
  <div :style="{ backgroundColor: background, color: foreground }" class="marker-key">
    <slot></slot>
  </div>
</template>

<script>
export default {
  props: {
    background: {
      type: String,
      default: '#ffffff'
    },
    foreground: {
      type: String,
      default: '#000000'
    }
  }
}
</script>

<style>
.marker-key {
  width: 2.5em;
  text-align: center;
  padding: 4px;
  font-family: monospace;
  border: 1px solid #000000;
}
</style>