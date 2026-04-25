<template>
  <Card :class="cardClass">
    <template #content>
      <div class="flex justify-between items-start">
        <div>
          <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
            <slot name="label">{{ label }}</slot>
          </p>
          <h3 :class="valueClass">
            <slot name="value">{{ value }}</slot>
          </h3>
          <p v-if="subLabel" class="text-[10px] mt-1" :class="subLabelClass">
            <slot name="subLabel">{{ subLabel }}</slot>
          </p>
        </div>
        <div :class="iconBgClass">
          <slot name="icon">
            <i :class="icon"></i>
          </slot>
        </div>
      </div>
    </template>
  </Card>
</template>

<script setup lang="ts">
import Card from 'primevue/card'

defineProps<{
  label?: string
  value?: string | number
  subLabel?: string
  icon?: string
  cardClass?: string
  valueClass?: string
  iconBgClass?: string
  subLabelClass?: string
}>()
</script>

<style scoped></style>
