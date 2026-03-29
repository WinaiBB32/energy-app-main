<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import {
  collection,
  query,
  orderBy,
  limit,
  onSnapshot,
  Timestamp,
} from 'firebase/firestore'
// Firebase Removed
import type { AuditAction } from '@/utils/auditLogger'
import { useAppToast } from '@/composables/useAppToast'

import Card from 'primevue/card'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Select from 'primevue/select'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'

// ─── Interface ────────────────────────────────────────────────────────────────
interface AuditLog {
  id: string
  uid: string
  displayName: string
  email: string
  role: string
  action: AuditAction
  module: string
  detail: string
  ipAddress: string
  browser: string
  createdAt: Timestamp | null
}

// ─── State ────────────────────────────────────────────────────────────────────
const toast = useAppToast()
const logs = ref<AuditLog[]>([])
const isLoading = ref(true)
let unsubLogs: () => void

const searchQuery = ref('')
const selectedAction = ref<AuditAction | null>(null)
const selectedDate = ref<Date | null>(null)

// ─── Fetch ────────────────────────────────────────────────────────────────────
onMounted(() => {
  unsubLogs = onSnapshot(
    query(collection(db, 'audit_logs'), orderBy('createdAt', 'desc'), limit(1000)),
    (snap) => {
      logs.value = snap.docs.map((d) => ({ id: d.id, ...d.data() } as AuditLog))
      isLoading.value = false
    },
    (err) => {
      toast.fromError(err, 'ไม่สามารถโหลดข้อมูล Audit Log ได้')
      isLoading.value = false
    },
  )
})

onUnmounted(() => {
  if (unsubLogs) unsubLogs()
})

// ─── Filter ───────────────────────────────────────────────────────────────────
const filteredLogs = computed(() => {
  let list = logs.value

  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(
      (l) =>
        l.displayName.toLowerCase().includes(q) ||
        l.email.toLowerCase().includes(q) ||
        l.ipAddress.includes(q) ||
        l.detail.toLowerCase().includes(q),
    )
  }
  if (selectedAction.value) {
    list = list.filter((l) => l.action === selectedAction.value)
  }
  if (selectedDate.value) {
    const d = selectedDate.value
    list = list.filter((l) => {
      if (!l.createdAt) return false
      const logDate = l.createdAt.toDate()
      return (
        logDate.getFullYear() === d.getFullYear() &&
        logDate.getMonth() === d.getMonth() &&
        logDate.getDate() === d.getDate()
      )
    })
  }
  return list
})

// ─── Stats ────────────────────────────────────────────────────────────────────
const todayLogs = computed(() => {
  const today = new Date()
  return logs.value.filter((l) => {
    if (!l.createdAt) return false
    const d = l.createdAt.toDate()
    return (
      d.getFullYear() === today.getFullYear() &&
      d.getMonth() === today.getMonth() &&
      d.getDate() === today.getDate()
    )
  })
})

const uniqueUsersToday = computed(() => new Set(todayLogs.value.map((l) => l.uid)).size)

const dangerActionsToday = computed(
  () => todayLogs.value.filter((l) => l.action === 'DELETE' || l.action === 'SUSPEND_USER').length,
)

// ─── Helpers ──────────────────────────────────────────────────────────────────
const actionOptions = [
  { label: 'ทั้งหมด', value: null },
  { label: 'LOGIN', value: 'LOGIN' },
  { label: 'LOGOUT', value: 'LOGOUT' },
  { label: 'CREATE', value: 'CREATE' },
  { label: 'UPDATE', value: 'UPDATE' },
  { label: 'DELETE', value: 'DELETE' },
  { label: 'UPLOAD', value: 'UPLOAD' },
  { label: 'APPROVE_USER', value: 'APPROVE_USER' },
  { label: 'REJECT_USER', value: 'REJECT_USER' },
  { label: 'SUSPEND_USER', value: 'SUSPEND_USER' },
  { label: 'VIEW_ADMIN', value: 'VIEW_ADMIN' },
]

const actionSeverity = (action: AuditAction) => {
  const map: Record<AuditAction, string> = {
    LOGIN: 'success',
    LOGOUT: 'secondary',
    CREATE: 'info',
    UPDATE: 'warn',
    DELETE: 'danger',
    UPLOAD: 'help',
    APPROVE_USER: 'success',
    REJECT_USER: 'danger',
    SUSPEND_USER: 'danger',
    VIEW_ADMIN: 'secondary',
  }
  return map[action] ?? 'secondary'
}

const roleSeverity = (role: string) => {
  if (role === 'superadmin') return 'danger'
  if (role === 'admin') return 'warn'
  return 'info'
}

const formatDateTime = (ts: Timestamp | null | undefined): string => {
  if (!ts) return '-'
  return ts.toDate().toLocaleString('th-TH', {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}

const clearFilters = () => {
  searchQuery.value = ''
  selectedAction.value = null
  selectedDate.value = null
}
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10">
    <!-- Header -->
    <div class="mb-6">
      <h2 class="text-3xl font-bold text-gray-900">
        <i class="pi pi-shield text-indigo-600 mr-2"></i>Audit Log
      </h2>
      <p class="text-gray-400 mt-1">ตรวจสอบกิจกรรมของผู้ใช้งานทั้งหมดในระบบแบบ Real-time</p>
    </div>

    <!-- Stats -->
    <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
      <div class="bg-white rounded-2xl border border-gray-100 p-5 flex items-center gap-4">
        <div class="w-12 h-12 bg-indigo-50 rounded-xl flex items-center justify-center">
          <i class="pi pi-list text-indigo-600 text-xl"></i>
        </div>
        <div>
          <p class="text-2xl font-black text-gray-900">{{ todayLogs.length.toLocaleString() }}</p>
          <p class="text-xs text-gray-400 mt-0.5">กิจกรรมวันนี้</p>
        </div>
      </div>

      <div class="bg-white rounded-2xl border border-gray-100 p-5 flex items-center gap-4">
        <div class="w-12 h-12 bg-emerald-50 rounded-xl flex items-center justify-center">
          <i class="pi pi-users text-emerald-600 text-xl"></i>
        </div>
        <div>
          <p class="text-2xl font-black text-gray-900">{{ uniqueUsersToday }}</p>
          <p class="text-xs text-gray-400 mt-0.5">ผู้ใช้งาน active วันนี้</p>
        </div>
      </div>

      <div class="bg-white rounded-2xl border border-gray-100 p-5 flex items-center gap-4">
        <div class="w-12 h-12 rounded-xl flex items-center justify-center"
          :class="dangerActionsToday > 0 ? 'bg-red-50' : 'bg-gray-50'">
          <i class="pi pi-exclamation-triangle text-xl"
            :class="dangerActionsToday > 0 ? 'text-red-500' : 'text-gray-300'"></i>
        </div>
        <div>
          <p class="text-2xl font-black" :class="dangerActionsToday > 0 ? 'text-red-600' : 'text-gray-900'">
            {{ dangerActionsToday }}
          </p>
          <p class="text-xs text-gray-400 mt-0.5">การกระทำความเสี่ยงสูงวันนี้</p>
        </div>
      </div>
    </div>

    <!-- Filters -->
    <Card class="shadow-sm border-none mb-4">
      <template #content>
        <div class="flex flex-wrap gap-3 items-end">
          <div class="flex-1 min-w-48">
            <label class="text-xs font-semibold text-gray-500 mb-1 block">ค้นหาชื่อ / อีเมล / IP</label>
            <IconField class="w-full">
              <InputIcon class="pi pi-search" />
              <InputText v-model="searchQuery" placeholder="ค้นหา..." class="w-full" />
            </IconField>
          </div>

          <div class="w-48">
            <label class="text-xs font-semibold text-gray-500 mb-1 block">ประเภทการกระทำ</label>
            <Select
              v-model="selectedAction"
              :options="actionOptions"
              optionLabel="label"
              optionValue="value"
              class="w-full"
              placeholder="ทั้งหมด"
            />
          </div>

          <div class="w-44">
            <label class="text-xs font-semibold text-gray-500 mb-1 block">วันที่</label>
            <DatePicker v-model="selectedDate" placeholder="เลือกวันที่" class="w-full" showIcon />
          </div>

          <Button
            icon="pi pi-times"
            label="ล้างตัวกรอง"
            severity="secondary"
            text
            @click="clearFilters"
          />

          <div class="ml-auto">
            <Tag :value="`${filteredLogs.length} รายการ`" severity="info" rounded />
          </div>
        </div>
      </template>
    </Card>

    <!-- Table -->
    <Card class="shadow-sm border-none">
      <template #content>
        <DataTable
          :value="filteredLogs"
          :loading="isLoading"
          paginator
          :rows="25"
          :rowsPerPageOptions="[25, 50, 100]"
          stripedRows
          responsiveLayout="scroll"
          emptyMessage="ไม่พบประวัติการใช้งาน"
          size="small"
        >
          <Column header="เวลา" style="min-width: 160px">
            <template #body="{ data }">
              <span class="text-xs text-gray-500 font-mono">{{ formatDateTime(data.createdAt) }}</span>
            </template>
          </Column>

          <Column header="ผู้ใช้งาน" style="min-width: 180px">
            <template #body="{ data }">
              <div class="font-semibold text-gray-800 text-sm">{{ data.displayName }}</div>
              <div class="text-xs text-gray-400">{{ data.email }}</div>
            </template>
          </Column>

          <Column header="สิทธิ์" style="width: 110px">
            <template #body="{ data }">
              <Tag :value="data.role.toUpperCase()" :severity="roleSeverity(data.role)" rounded class="text-[10px]" />
            </template>
          </Column>

          <Column header="การกระทำ" style="width: 140px">
            <template #body="{ data }">
              <Tag :value="data.action" :severity="actionSeverity(data.action)" rounded class="text-[10px]" />
            </template>
          </Column>

          <Column header="โมดูล" style="width: 130px">
            <template #body="{ data }">
              <span class="text-xs text-gray-600">{{ data.module || '-' }}</span>
            </template>
          </Column>

          <Column header="รายละเอียด" style="min-width: 160px">
            <template #body="{ data }">
              <span class="text-xs text-gray-500">{{ data.detail || '-' }}</span>
            </template>
          </Column>

          <Column header="IP Address" style="width: 140px">
            <template #body="{ data }">
              <span class="text-xs font-mono text-gray-700 bg-gray-100 px-2 py-0.5 rounded">
                {{ data.ipAddress }}
              </span>
            </template>
          </Column>

          <Column header="Browser" style="width: 90px">
            <template #body="{ data }">
              <span class="text-xs text-gray-500">{{ data.browser || '-' }}</span>
            </template>
          </Column>
        </DataTable>
      </template>
    </Card>
  </div>
</template>

<style scoped>
:deep(.p-datatable-header-cell) {
  background-color: #f8fafc !important;
  color: #64748b !important;
  font-weight: 700 !important;
  font-size: 0.75rem !important;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}
</style>
