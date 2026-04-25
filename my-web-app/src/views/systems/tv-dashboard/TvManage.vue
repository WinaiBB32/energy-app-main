<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '@/services/api'
import { useAppToast } from '@/composables/useAppToast'

import Card from 'primevue/card'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'

defineOptions({ name: 'TvDashboardManage' })

const router = useRouter()
const toast = useAppToast()

interface DashboardItem {
  id: string
  name: string
  description?: string
  isActive: boolean
  refreshIntervalSeconds: number
  slideDurationSeconds: number
  widgetCount: number
  updatedAt: string
}

const dashboards = ref<DashboardItem[]>([])
const isLoading = ref(true)
const deleteDialog = ref(false)
const deletingId = ref<string | null>(null)
const isDeleting = ref(false)

const fetchDashboards = async () => {
  isLoading.value = true
  try {
    const res = await api.get('/TvDashboard')
    dashboards.value = Array.isArray(res.data) ? res.data : []
  } catch (err) {
    toast.fromError(err, 'ไม่สามารถโหลดรายการ Dashboard ได้')
  } finally {
    isLoading.value = false
  }
}

onMounted(fetchDashboards)

const openNew = () => router.push('/tv-dashboard/new')
const openEdit = (id: string) => router.push(`/tv-dashboard/${id}/edit`)

const openDisplay = (id: string) => {
  window.open(router.resolve(`/tv-display/${id}`).href, '_blank')
}

const confirmDelete = (id: string) => {
  deletingId.value = id
  deleteDialog.value = true
}

const doDelete = async () => {
  if (!deletingId.value) return
  isDeleting.value = true
  try {
    await api.delete(`/TvDashboard/${deletingId.value}`)
    toast.success('ลบ Dashboard สำเร็จ')
    deleteDialog.value = false
    await fetchDashboards()
  } catch (err) {
    toast.fromError(err, 'ไม่สามารถลบ Dashboard ได้')
  } finally {
    isDeleting.value = false
  }
}

const formatDate = (val: string) => {
  if (!val) return '-'
  const d = new Date(val)
  return d.toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <div class="p-4 md:p-6">
    <Card>
      <template #header>
        <div class="flex items-center justify-between px-5 pt-4 pb-2">
          <div class="flex items-center gap-3">
            <span class="pi pi-desktop text-indigo-500 text-2xl" />
            <div>
              <h2 class="text-xl font-bold text-gray-800">TV Dashboard</h2>
              <p class="text-sm text-gray-500">จัดการหน้าจอแสดงผลข้อมูลสรุปบนจอ TV</p>
            </div>
          </div>
          <Button
            label="สร้าง Dashboard ใหม่"
            icon="pi pi-plus"
            @click="openNew"
          />
        </div>
      </template>
      <template #content>
        <DataTable
          :value="dashboards"
          :loading="isLoading"
          striped-rows
          class="text-sm"
          empty-message="ยังไม่มี Dashboard"
        >
          <Column field="name" header="ชื่อ Dashboard" class="font-medium">
            <template #body="{ data }">
              <div>
                <div class="font-semibold text-gray-800">{{ data.name }}</div>
                <div v-if="data.description" class="text-xs text-gray-500 mt-0.5">{{ data.description }}</div>
              </div>
            </template>
          </Column>
          <Column header="สถานะ" style="width: 100px">
            <template #body="{ data }">
              <Tag
                :value="data.isActive ? 'เปิดใช้งาน' : 'ปิดใช้งาน'"
                :severity="data.isActive ? 'success' : 'secondary'"
              />
            </template>
          </Column>
          <Column header="Widget" style="width: 90px" class="text-center">
            <template #body="{ data }">
              <span class="font-semibold text-indigo-600">{{ data.widgetCount }}</span>
              <span class="text-gray-400 text-xs"> ชิ้น</span>
            </template>
          </Column>
          <Column header="Slide / Refresh" style="width: 140px">
            <template #body="{ data }">
              <div class="text-xs text-gray-600">
                <span>Slide: {{ data.slideDurationSeconds }}s</span><br>
                <span>Refresh: {{ data.refreshIntervalSeconds }}s</span>
              </div>
            </template>
          </Column>
          <Column header="แก้ไขล่าสุด" style="width: 120px">
            <template #body="{ data }">
              <span class="text-xs text-gray-500">{{ formatDate(data.updatedAt) }}</span>
            </template>
          </Column>
          <Column header="จัดการ" style="width: 180px">
            <template #body="{ data }">
              <div class="flex gap-1.5">
                <Button
                  icon="pi pi-desktop"
                  severity="success"
                  size="small"
                  v-tooltip="'เปิดจอ TV'"
                  @click="openDisplay(data.id)"
                />
                <Button
                  icon="pi pi-pencil"
                  severity="info"
                  size="small"
                  v-tooltip="'แก้ไข'"
                  @click="openEdit(data.id)"
                />
                <Button
                  icon="pi pi-trash"
                  severity="danger"
                  size="small"
                  v-tooltip="'ลบ'"
                  @click="confirmDelete(data.id)"
                />
              </div>
            </template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <Dialog v-model:visible="deleteDialog" header="ยืนยันการลบ" modal style="width: 380px">
      <p class="text-gray-700">ต้องการลบ Dashboard นี้ใช่หรือไม่? การลบไม่สามารถย้อนกลับได้</p>
      <template #footer>
        <Button label="ยกเลิก" text @click="deleteDialog = false" />
        <Button label="ลบ" severity="danger" :loading="isDeleting" @click="doDelete" />
      </template>
    </Dialog>
  </div>
</template>
