<script setup lang="ts">
import { RouterView } from 'vue-router'
import Toast from 'primevue/toast'
</script>

<template>
  <RouterView />
  <Toast position="bottom-right" :pt="{ root: { style: 'z-index: 9999' } }" />
</template>
