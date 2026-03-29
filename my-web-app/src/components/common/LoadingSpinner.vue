<script setup lang="ts">
import ProgressSpinner from 'primevue/progressspinner'

interface Props {
  message?: string
  size?: 'sm' | 'md' | 'lg'
}

const props = withDefaults(defineProps<Props>(), {
  message: '',
  size: 'md',
})

const sizeMap: Record<string, string> = {
  sm: '2rem',
  md: '3rem',
  lg: '4rem',
}

const spinnerSize = sizeMap[props.size] ?? sizeMap['md']
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-3 py-8">
    <ProgressSpinner
      :style="`width: ${spinnerSize}; height: ${spinnerSize}`"
      strokeWidth="4"
      animationDuration=".75s"
    />
    <p v-if="message" class="text-sm text-gray-500">{{ message }}</p>
  </div>
</template>
