<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import api from '@/services/api'

import Card from 'primevue/card'
import Button from 'primevue/button'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Message from 'primevue/message'

interface ServiceRequest {
  id: string
  workOrderNo?: string
  title: string
  category: string
  priority: string
  status: string
  requesterName: string
  createdAt: string | null
  updatedAt?: string | null
}

interface SparePart {
  id: string
  partCode: string
  partName: string
  unit: string
  quantityOnHand: number
  reorderPoint: number
}

interface IssueRequest {
  id: string
  requestNo: string
  status: 'pending' | 'approved' | 'rejected'
  requestedByName: string
  createdAt: string
}

const loading = ref(false)
const errorMessage = ref('')
const requests = ref<ServiceRequest[]>([])
const spareParts = ref<SparePart[]>([])
const issueRequests = ref<IssueRequest[]>([])

const externalStatuses = ['waiting_department_external_procurement', 'waiting_central_external_procurement', 'external_scheduled', 'external_in_progress']
const doneStatuses = ['resolved', 'closed']

const totalRequests = computed(() => requests.value.length)
const openRequests = computed(() => requests.value.filter((r) => !doneStatuses.includes(r.status)).length)
const externalQueueCount = computed(() =>
  requests.value.filter((r) => externalStatuses.includes(r.status)).length,
)
const completedCount = computed(() => requests.value.filter((r) => doneStatuses.includes(r.status)).length)
const pendingIssueCount = computed(
  () => issueRequests.value.filter((x) => x.status === 'pending').length,
)
const lowStockCount = computed(
  () => spareParts.value.filter((p) => p.quantityOnHand <= p.reorderPoint).length,
)

const completionRate = computed(() => {
  if (totalRequests.value === 0) return 0
  return Math.round((completedCount.value / totalRequests.value) * 100)
})

const topCategories = computed(() => {
  const map = new Map<string, number>()
  for (const item of requests.value) {
    const key = item.category?.trim() || 'other'
    map.set(key, (map.get(key) ?? 0) + 1)
  }

  return [...map.entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, 5)
    .map(([category, count]) => ({ category, count }))
})

const latestRequests = computed(() =>
  [...requests.value]
    .sort((a, b) => new Date(b.createdAt ?? 0).getTime() - new Date(a.createdAt ?? 0).getTime())
    .slice(0, 8),
)

const maxCategoryCount = computed(() =>
  topCategories.value.length ? Math.max(...topCategories.value.map((x) => x.count)) : 1,
)

const formatDateTime = (value: string | null | undefined): string => {
  if (!value) return '-'
  return new Date(value).toLocaleString('th-TH', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const formatDisplayName = (value: string | null | undefined): string => {
  const raw = (value || '').trim()
  if (!raw) return '-'
  if (!raw.includes('@')) return raw

  const localPart = raw.split('@')[0] || raw
  return localPart.replace(/[._-]+/g, ' ')
}

const statusTag = (status: string): { severity: 'secondary' | 'info' | 'success' | 'danger'; label: string } => {
  if (status === 'new') return { severity: 'secondary', label: 'ใหม่' }
  if (status === 'assigned') return { severity: 'info', label: 'มอบหมายแล้ว' }
  if (status === 'in_progress') return { severity: 'info', label: 'กำลังดำเนินการ' }
  if (status === 'need_supervisor_review') return { severity: 'danger', label: 'รอหัวหน้าพิจารณา' }
  if (status === 'returned_to_technician') return { severity: 'info', label: 'ส่งกลับให้ช่าง' }
  if (status === 'waiting_department_external_procurement') return { severity: 'danger', label: 'รอหน่วยงาน/กอง จัดจ้างช่างภายนอก' }
  if (status === 'waiting_central_external_procurement') return { severity: 'danger', label: 'รอธุรการส่วนกลาง จัดจ้างช่างภายนอก' }
  if (status === 'external_scheduled') return { severity: 'info', label: 'นัดช่างภายนอกแล้ว' }
  if (status === 'external_in_progress') return { severity: 'info', label: 'ช่างภายนอกกำลังซ่อม' }
  if (status === 'resolved') return { severity: 'success', label: 'ซ่อมเสร็จ' }
  if (status === 'closed') return { severity: 'success', label: 'ปิดงาน' }
  return { severity: 'secondary', label: status }
}

const categoryLabel = (category: string): string => {
  if (category === 'electrical') return 'งานระบบไฟฟ้า'
  if (category === 'plumbing') return 'งานระบบประปา'
  if (category === 'building') return 'งานโครงสร้าง/อาคาร'
  if (category === 'hvac') return 'งานระบบปรับอากาศ'
  if (category === 'furniture') return 'งานเฟอร์นิเจอร์/ครุภัณฑ์'
  if (category === 'other') return 'อื่น ๆ'
  return category
}

const loadDashboard = async () => {
  loading.value = true
  errorMessage.value = ''
  try {
    const [serviceRes, partRes, issueRes] = await Promise.all([
      api.get('/ServiceRequest', { params: { take: 500 } }),
      api.get('/SparePart'),
      api.get('/SparePart/issue-requests'),
    ])

    requests.value = (serviceRes.data.items ?? serviceRes.data ?? []) as ServiceRequest[]
    spareParts.value = (partRes.data ?? []) as SparePart[]
    issueRequests.value = (issueRes.data ?? []) as IssueRequest[]
  } catch {
    errorMessage.value = 'โหลดข้อมูล Dashboard ระบบแจ้งซ่อมไม่สำเร็จ'
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await loadDashboard()
})
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h1 class="text-2xl font-bold text-gray-900">Dashboard ระบบแจ้งซ่อมงานอาคาร</h1>
        <p class="text-sm text-gray-500 mt-1">สรุปภาพรวมงานซ่อม คลังอะไหล่ และคำขอเบิกที่รอจัดการ</p>
      </div>
      <Button label="รีเฟรชข้อมูล" icon="pi pi-refresh" outlined :loading="loading" @click="loadDashboard" />
    </div>

    <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>

    <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-6 gap-4">
      <div class="rounded-xl border border-gray-200 bg-white p-4">
        <p class="text-xs text-gray-500">ใบแจ้งซ่อมทั้งหมด</p>
        <p class="text-2xl font-bold text-gray-900 mt-1">{{ totalRequests }}</p>
      </div>
      <div class="rounded-xl border border-blue-200 bg-blue-50 p-4">
        <p class="text-xs text-blue-700">งานที่ยังไม่ปิด</p>
        <p class="text-2xl font-bold text-blue-800 mt-1">{{ openRequests }}</p>
      </div>
      <div class="rounded-xl border border-emerald-200 bg-emerald-50 p-4">
        <p class="text-xs text-emerald-700">งานซ่อมเสร็จ/ปิดงาน</p>
        <p class="text-2xl font-bold text-emerald-800 mt-1">{{ completedCount }}</p>
      </div>
      <div class="rounded-xl border border-orange-200 bg-orange-50 p-4">
        <p class="text-xs text-orange-700">คิวช่างภายนอก</p>
        <p class="text-2xl font-bold text-orange-800 mt-1">{{ externalQueueCount }}</p>
      </div>
      <div class="rounded-xl border border-yellow-200 bg-yellow-50 p-4">
        <p class="text-xs text-yellow-700">อะไหล่ต่ำกว่าจุดสั่งซื้อ</p>
        <p class="text-2xl font-bold text-yellow-800 mt-1">{{ lowStockCount }}</p>
      </div>
      <div class="rounded-xl border border-purple-200 bg-purple-50 p-4">
        <p class="text-xs text-purple-700">คำขอเบิกรออนุมัติ</p>
        <p class="text-2xl font-bold text-purple-800 mt-1">{{ pendingIssueCount }}</p>
      </div>
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
      <Card class="xl:col-span-1">
        <template #title>
          <div class="flex items-center justify-between">
            <span>อัตราปิดงาน</span>
            <span class="text-sm font-semibold text-gray-600">{{ completionRate }}%</span>
          </div>
        </template>
        <template #content>
          <div class="h-2 rounded-full bg-gray-100 overflow-hidden">
            <div
              class="h-full bg-emerald-500 transition-all duration-300"
              :style="{ width: `${completionRate}%` }"
            />
          </div>
          <p class="text-xs text-gray-500 mt-3">คำนวณจากจำนวนงานที่สถานะเป็น ซ่อมเสร็จ หรือ ปิดงาน</p>
        </template>
      </Card>

      <Card class="xl:col-span-2">
        <template #title>หมวดงานที่พบมาก</template>
        <template #content>
          <div v-if="topCategories.length" class="space-y-3">
            <div v-for="item in topCategories" :key="item.category">
              <div class="flex items-center justify-between text-sm mb-1">
                <span class="text-gray-700">{{ categoryLabel(item.category) }}</span>
                <span class="font-semibold text-gray-900">{{ item.count }} งาน</span>
              </div>
              <div class="h-2 rounded-full bg-gray-100 overflow-hidden">
                <div
                  class="h-full bg-indigo-500 transition-all duration-300"
                  :style="{ width: `${Math.max(8, (item.count / maxCategoryCount) * 100)}%` }"
                />
              </div>
            </div>
          </div>
          <p v-else class="text-sm text-gray-500">ยังไม่มีข้อมูลหมวดงาน</p>
        </template>
      </Card>
    </div>

    <Card>
      <template #title>งานแจ้งซ่อมล่าสุด</template>
      <template #content>
        <DataTable :value="latestRequests" :loading="loading" size="small" striped-rows>
          <Column field="workOrderNo" header="เลขใบงาน" style="width: 10rem">
            <template #body="{ data }">{{ data.workOrderNo || '-' }}</template>
          </Column>
          <Column field="title" header="หัวข้อ" />
          <Column field="category" header="หมวดงาน" style="width: 11rem">
            <template #body="{ data }">{{ categoryLabel(data.category) }}</template>
          </Column>
          <Column field="requesterName" header="ผู้แจ้ง" style="width: 10rem">
            <template #body="{ data }">{{ formatDisplayName(data.requesterName) }}</template>
          </Column>
          <Column field="status" header="สถานะ" style="width: 12rem">
            <template #body="{ data }">
              <Tag :severity="statusTag(data.status).severity" :value="statusTag(data.status).label" />
            </template>
          </Column>
          <Column field="createdAt" header="วันที่แจ้ง" style="width: 12rem">
            <template #body="{ data }">{{ formatDateTime(data.createdAt) }}</template>
          </Column>
        </DataTable>
      </template>
    </Card>
  </div>
</template>