<script setup lang="ts">
import { ref, onMounted } from 'vue'

const ALL_WIDGET_TYPES = [
  { type: 'electricity', label: 'ค่าไฟฟ้า' },
  { type: 'solar', label: 'โซลาร์เซลล์' },
  { type: 'water', label: 'ค่าน้ำประปา' },
  { type: 'fuel', label: 'น้ำมันเชื้อเพลิง' },
  { type: 'maintenance', label: 'ซ่อมบำรุง' },
  { type: 'meeting', label: 'ห้องประชุม' },
  { type: 'postal', label: 'ไปรษณีย์' },
  { type: 'saraban', label: 'สารบรรณ' },
]

const selected = ref<string[]>([])

onMounted(() => {
  const raw = localStorage.getItem('tv_selected_widgets')
  if (raw) {
    try {
      const arr = JSON.parse(raw)
      if (Array.isArray(arr)) selected.value = arr
    } catch {}
  } else {
    selected.value = ALL_WIDGET_TYPES.map((w) => w.type)
  }
})

function toggle(type: string) {
  if (selected.value.includes(type)) {
    selected.value = selected.value.filter((t) => t !== type)
  } else {
    selected.value.push(type)
  }
  localStorage.setItem('tv_selected_widgets', JSON.stringify(selected.value))
}
</script>

<template>
  <div class="max-w-xl mx-auto p-6">
    <h1 class="text-2xl font-bold mb-6">เลือก Widget ที่จะแสดงบน TV Dashboard</h1>
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <label
        v-for="w in ALL_WIDGET_TYPES"
        :key="w.type"
        class="flex items-center gap-3 p-4 rounded-lg border border-gray-300 bg-white shadow hover:shadow-lg cursor-pointer transition"
      >
        <input
          type="checkbox"
          :value="w.type"
          v-model="selected"
          @change="toggle(w.type)"
          class="accent-blue-600 w-5 h-5"
        />
        <span class="text-lg">{{ w.label }}</span>
      </label>
    </div>
    <div class="mt-8 text-right">
      <span class="text-gray-500 text-sm">การเปลี่ยนแปลงจะมีผลทันทีที่ TV Dashboard</span>
    </div>
  </div>
</template>

<style scoped>
input[type='checkbox']:checked {
  accent-color: #2563eb;
}
</style>
