<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/services/api'
import { useAppToast } from '@/composables/useAppToast'

import Card from 'primevue/card'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import InputNumber from 'primevue/inputnumber'
import ToggleSwitch from 'primevue/toggleswitch'
import Tag from 'primevue/tag'
import Divider from 'primevue/divider'

defineOptions({ name: 'TvDashboardEdit' })

const route = useRoute()
const router = useRouter()
const toast = useAppToast()

const isEditMode = computed(() => !!route.params.id)
const dashboardId = computed(() => route.params.id as string | undefined)

interface WidgetItem {
  widgetType: string
  label: string
  sortOrder: number
  isVisible: boolean
}

const ALL_WIDGET_TYPES = [
  { type: 'electricity', label: 'ค่าไฟฟ้า (กฟภ./กฟน.)', icon: 'pi-bolt', color: '#F59E0B', bgClass: 'bg-amber-50 border-amber-200' },
  { type: 'solar', label: 'Solar Cell', icon: 'pi-sun', color: '#EAB308', bgClass: 'bg-yellow-50 border-yellow-200' },
  { type: 'water', label: 'ค่าน้ำประปา', icon: 'pi-wave-pulse', color: '#06B6D4', bgClass: 'bg-cyan-50 border-cyan-200' },
  { type: 'fuel', label: 'น้ำมันเชื้อเพลิง', icon: 'pi-car', color: '#F43F5E', bgClass: 'bg-rose-50 border-rose-200' },
  { type: 'maintenance', label: 'ซ่อมบำรุงอาคาร', icon: 'pi-wrench', color: '#F97316', bgClass: 'bg-orange-50 border-orange-200' },
  { type: 'meeting', label: 'ห้องประชุม', icon: 'pi-users', color: '#6366F1', bgClass: 'bg-indigo-50 border-indigo-200' },
  { type: 'postal', label: 'ไปรษณีย์', icon: 'pi-envelope', color: '#3B82F6', bgClass: 'bg-blue-50 border-blue-200' },
  { type: 'saraban', label: 'สารบรรณ', icon: 'pi-folder-open', color: '#8B5CF6', bgClass: 'bg-violet-50 border-violet-200' },
]

const name = ref('')
const description = ref('')
const isActive = ref(true)
const refreshIntervalSeconds = ref(60)
const slideDurationSeconds = ref(10)
const widgets = ref<WidgetItem[]>(
  ALL_WIDGET_TYPES.map((w, i) => ({
    widgetType: w.type,
    label: w.label,
    sortOrder: i,
    isVisible: true,
  }))
)

const isLoading = ref(false)
const isSaving = ref(false)

const fetchDashboard = async () => {
  if (!dashboardId.value) return
  isLoading.value = true
  try {
    const res = await api.get(`/TvDashboard/${dashboardId.value}`)
    const d = res.data
    name.value = d.name
    description.value = d.description ?? ''
    isActive.value = d.isActive
    refreshIntervalSeconds.value = d.refreshIntervalSeconds
    slideDurationSeconds.value = d.slideDurationSeconds

    // Merge saved widgets with ALL_WIDGET_TYPES (add any new types not yet saved)
    const savedMap = new Map<string, WidgetItem>()
    for (const w of d.widgets) {
      savedMap.set(w.widgetType, w)
    }
    widgets.value = ALL_WIDGET_TYPES.map((wt, i) => {
      const saved = savedMap.get(wt.type)
      return saved
        ? { widgetType: saved.widgetType, label: saved.label, sortOrder: saved.sortOrder, isVisible: saved.isVisible }
        : { widgetType: wt.type, label: wt.label, sortOrder: 1000 + i, isVisible: false }
    })
    widgets.value.sort((a, b) => a.sortOrder - b.sortOrder)
  } catch (err) {
    toast.fromError(err, 'ไม่สามารถโหลดข้อมูล Dashboard ได้')
  } finally {
    isLoading.value = false
  }
}

onMounted(fetchDashboard)

const moveUp = (index: number) => {
  if (index === 0) return
  const arr = [...widgets.value]
  const a = arr[index] as WidgetItem
  const b = arr[index - 1] as WidgetItem
  arr[index - 1] = a
  arr[index] = b
  arr.forEach((w, i) => (w.sortOrder = i))
  widgets.value = arr
}

const moveDown = (index: number) => {
  if (index >= widgets.value.length - 1) return
  const arr = [...widgets.value]
  const a = arr[index] as WidgetItem
  const b = arr[index + 1] as WidgetItem
  arr[index] = b
  arr[index + 1] = a
  arr.forEach((w, i) => (w.sortOrder = i))
  widgets.value = arr
}

const getWidgetMeta = (type: string) => ALL_WIDGET_TYPES.find((w) => w.type === type)

const visibleCount = computed(() => widgets.value.filter((w) => w.isVisible).length)

const save = async () => {
  if (!name.value.trim()) {
    toast.warn('กรุณาระบุชื่อ Dashboard')
    return
  }
  if (visibleCount.value === 0) {
    toast.warn('กรุณาเลือกอย่างน้อย 1 Widget')
    return
  }

  isSaving.value = true
  const payload = {
    name: name.value.trim(),
    description: description.value.trim() || null,
    isActive: isActive.value,
    refreshIntervalSeconds: refreshIntervalSeconds.value,
    slideDurationSeconds: slideDurationSeconds.value,
    widgets: widgets.value.map((w, i) => ({ ...w, sortOrder: i })),
  }

  try {
    if (isEditMode.value) {
      await api.put(`/TvDashboard/${dashboardId.value}`, payload)
      toast.success('อัปเดต Dashboard สำเร็จ')
    } else {
      await api.post('/TvDashboard', payload)
      toast.success('สร้าง Dashboard สำเร็จ')
    }
    router.push('/tv-dashboard')
  } catch (err) {
    toast.fromError(err, 'บันทึกไม่สำเร็จ')
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div class="p-4 md:p-6 max-w-3xl mx-auto">
    <Card>
      <template #header>
        <div class="flex items-center gap-3 px-5 pt-4 pb-2">
          <Button icon="pi pi-arrow-left" text rounded @click="router.push('/tv-dashboard')" />
          <span class="pi pi-desktop text-indigo-500 text-xl" />
          <h2 class="text-xl font-bold text-gray-800">
            {{ isEditMode ? 'แก้ไข Dashboard' : 'สร้าง Dashboard ใหม่' }}
          </h2>
        </div>
      </template>

      <template #content>
        <div v-if="isLoading" class="flex justify-center py-12">
          <span class="pi pi-spinner pi-spin text-3xl text-indigo-400" />
        </div>

        <div v-else class="space-y-6 px-1">
          <!-- Basic Info -->
          <section>
            <h3 class="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">ข้อมูลทั่วไป</h3>
            <div class="space-y-4">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">ชื่อ Dashboard <span class="text-red-500">*</span></label>
                <InputText v-model="name" placeholder="เช่น จอ TV ล็อบบี้อาคาร A" class="w-full" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">คำอธิบาย</label>
                <Textarea v-model="description" placeholder="รายละเอียดเพิ่มเติม (ไม่บังคับ)" rows="2" class="w-full" />
              </div>
              <div class="flex items-center gap-3">
                <ToggleSwitch v-model="isActive" />
                <span class="text-sm text-gray-700">เปิดใช้งาน</span>
                <Tag :value="isActive ? 'Active' : 'Inactive'" :severity="isActive ? 'success' : 'secondary'" />
              </div>
            </div>
          </section>

          <Divider />

          <!-- Timing -->
          <section>
            <h3 class="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">การตั้งเวลา</h3>
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">เวลาต่อ Slide (วินาที)</label>
                <InputNumber v-model="slideDurationSeconds" :min="5" :max="120" show-buttons class="w-full" />
                <p class="text-xs text-gray-400 mt-1">สไลด์จะเปลี่ยนทุก {{ slideDurationSeconds }} วินาที</p>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">รีเฟรชข้อมูล (วินาที)</label>
                <InputNumber v-model="refreshIntervalSeconds" :min="10" :max="3600" show-buttons class="w-full" />
                <p class="text-xs text-gray-400 mt-1">ดึงข้อมูลใหม่ทุก {{ refreshIntervalSeconds }} วินาที</p>
              </div>
            </div>
          </section>

          <Divider />

          <!-- Widget Selection -->
          <section>
            <div class="flex items-center justify-between mb-3">
              <h3 class="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                เลือก Widget ที่จะแสดง
              </h3>
              <span class="text-xs text-gray-400">เปิดใช้งาน {{ visibleCount }} / {{ widgets.length }}</span>
            </div>

            <div class="space-y-2">
              <div
                v-for="(widget, index) in widgets"
                :key="widget.widgetType"
                class="flex items-center gap-3 p-3 rounded-lg border transition-all"
                :class="[
                  getWidgetMeta(widget.widgetType)?.bgClass ?? 'bg-gray-50 border-gray-200',
                  !widget.isVisible && 'opacity-50'
                ]"
              >
                <!-- Icon -->
                <span
                  class="pi text-xl w-7 text-center shrink-0"
                  :class="getWidgetMeta(widget.widgetType)?.icon"
                  :style="{ color: getWidgetMeta(widget.widgetType)?.color }"
                />

                <!-- Label -->
                <div class="flex-1">
                  <span class="text-sm font-medium text-gray-700">{{ widget.label }}</span>
                </div>

                <!-- Visible Toggle -->
                <ToggleSwitch v-model="widget.isVisible" />

                <!-- Order Buttons -->
                <div class="flex flex-col gap-0.5">
                  <button
                    :disabled="index === 0"
                    class="w-6 h-5 flex items-center justify-center rounded text-xs text-gray-500 hover:bg-white/80 disabled:opacity-30"
                    @click="moveUp(index)"
                  >
                    <span class="pi pi-chevron-up text-xs" />
                  </button>
                  <button
                    :disabled="index === widgets.length - 1"
                    class="w-6 h-5 flex items-center justify-center rounded text-xs text-gray-500 hover:bg-white/80 disabled:opacity-30"
                    @click="moveDown(index)"
                  >
                    <span class="pi pi-chevron-down text-xs" />
                  </button>
                </div>
              </div>
            </div>
            <p class="text-xs text-gray-400 mt-2">ใช้ลูกศร ▲▼ เพื่อจัดลำดับการแสดงผล</p>
          </section>

          <Divider />

          <!-- Actions -->
          <div class="flex justify-end gap-3">
            <Button label="ยกเลิก" text @click="router.push('/tv-dashboard')" />
            <Button
              :label="isEditMode ? 'บันทึกการแก้ไข' : 'สร้าง Dashboard'"
              icon="pi pi-save"
              :loading="isSaving"
              @click="save"
            />
          </div>
        </div>
      </template>
    </Card>
  </div>
</template>
