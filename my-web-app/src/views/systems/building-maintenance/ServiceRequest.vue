<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import InputText from 'primevue/inputtext'
import AutoComplete from 'primevue/autocomplete'
import Select from 'primevue/select'
import Button from 'primevue/button'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'

// ─── Types ────────────────────────────────────────────────────────────────────
interface ServiceRequest {
  id: string
  workOrderNo?: string
  title: string
  description: string
  category: string
  priority: string
  status: string
  buildingName?: string
  locationDetail?: string
  assetNumber?: string
  isCentralAsset?: boolean
  extension: string
  requesterName: string
  requesterEmail: string
  requesterUid?: string
  requesterDepartmentCode?: string
  requesterDepartmentName?: string
  technicianUid?: string
  technicianName?: string
  supervisorUid?: string
  supervisorName?: string
  adminOfficerUid?: string
  adminOfficerName?: string
  technicianDiagnosis?: string
  technicianAction?: string
  escalationReason?: string
  supervisorCanRepairInHouse?: boolean | null
  supervisorReason?: string
  supervisorRepairPlan?: string
  supervisorExternalAdvice?: string
  externalVendorName?: string
  externalScheduledAt?: string | null
  externalCompletedAt?: string | null
  externalResult?: string
  closedByName?: string
  closedAt?: string | null
  assignedTo?: string
  note?: string
  createdAt: string | null
  updatedAt?: string | null
}

interface AssetHistoryItem {
  id: string
  workOrderNo: string
  title: string
  status: string
  priority: string
  buildingName: string
  locationDetail: string
  technicianName: string
  technicianDiagnosis: string
  technicianAction: string
  supervisorReason: string
  supervisorRepairPlan: string
  supervisorExternalAdvice: string
  externalVendorName: string
  externalScheduledAt: string | null
  externalCompletedAt: string | null
  externalResult: string
  closedAt: string | null
  createdAt: string | null
  updatedAt: string | null
}

// ─── Store ────────────────────────────────────────────────────────────────────
const router = useRouter()
const authStore = useAuthStore()
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('maintenance')

// ─── Options ──────────────────────────────────────────────────────────────────
const categoryOptions = [
  { label: 'งานระบบไฟฟ้า', value: 'electrical' },
  { label: 'งานระบบประปา', value: 'plumbing' },
  { label: 'งานโครงสร้าง/อาคาร', value: 'building' },
  { label: 'งานระบบปรับอากาศ', value: 'hvac' },
  { label: 'งานเฟอร์นิเจอร์/ครุภัณฑ์', value: 'furniture' },
  { label: 'อื่น ๆ', value: 'other' },
]
const priorityOptions = [
  { label: 'ต่ำ', value: 'low' },
  { label: 'ปานกลาง', value: 'medium' },
  { label: 'เร่งด่วน', value: 'urgent' },
  { label: 'วิกฤต', value: 'critical' },
]
const statusOptions = [
  { label: 'รอช่างอาคารประเมิน', value: 'new' },
  { label: 'มอบหมายแล้ว', value: 'assigned' },
  { label: 'กำลังดำเนินการ', value: 'in_progress' },
  { label: 'รอหัวหน้างานพิจารณา', value: 'need_supervisor_review' },
  { label: 'ส่งกลับให้ช่าง', value: 'returned_to_technician' },
  { label: 'รอหน่วยงาน/กอง จัดจ้างช่างภายนอก', value: 'waiting_department_external_procurement' },
  { label: 'รอธุรการส่วนกลาง จัดจ้างช่างภายนอก', value: 'waiting_central_external_procurement' },
  { label: 'นัดช่างภายนอกแล้ว', value: 'external_scheduled' },
  { label: 'ช่างภายนอกกำลังซ่อม', value: 'external_in_progress' },
  { label: 'ซ่อมเสร็จ', value: 'resolved' },
  { label: 'ปิดงาน', value: 'closed' },
]

// ─── State ────────────────────────────────────────────────────────────────────
const requests = ref<ServiceRequest[]>([])
const loading = ref(true)
const saving = ref(false)
const successMsg = ref('')

// Form
const form = ref({
  title: '',
  description: '',
  category: 'building',
  priority: 'medium',
  buildingName: '',
  locationDetail: '',
  assetNumber: '',
  isCentralAsset: false,
  extension: '',
})
const formVisible = ref(false)
const formError = ref('')

// Edit / Delete
const editVisible = ref(false)
const deleteVisible = ref(false)
const selectedRequest = ref<ServiceRequest | null>(null)
const editForm = ref({
  status: 'new',
  technicianUid: '',
  technicianName: '',
  note: '',
  diagnosis: '',
  action: '',
  escalationReason: '',
  canRepairInHouse: true,
  supervisorReason: '',
  supervisorPlan: '',
  supervisorAdvice: '',
  vendorName: '',
  externalResult: '',
})
const requestToDelete = ref<ServiceRequest | null>(null)

const assetHistoryVisible = ref(false)
const assetHistoryLoading = ref(false)
const assetHistoryAssetNo = ref('')
const assetHistoryItems = ref<AssetHistoryItem[]>([])
const assetSuggestions = ref<string[]>([])

// Filter
const filterStatus = ref<string | null>(null)
const filterSearch = ref('')
const filterAssetNumber = ref('')

// ─── Data Fetching ────────────────────────────────────────────────────────────
async function loadRequests() {
  try {
    const res = await api.get('/ServiceRequest', { params: { take: '500' } })
    requests.value = (res.data.items ?? res.data) as ServiceRequest[]
  } catch {
    // ignore
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadRequests()
})

const roleText = computed(() => (authStore.user?.role ?? '').toLowerCase())
const canAssignTechnician = computed(() => ['superadmin', 'supervisor', 'adminbuilding'].includes(roleText.value))
const canTechnicianAction = computed(() => ['superadmin', 'technician'].includes(roleText.value))
const canSupervisorAction = computed(() => ['superadmin', 'supervisor'].includes(roleText.value))
const canAdminBuildingAction = computed(() => ['superadmin', 'adminbuilding'].includes(roleText.value))

// ─── Computed ─────────────────────────────────────────────────────────────────
const filteredRequests = computed(() => {
  let list = requests.value
  if (filterStatus.value) list = list.filter((r) => r.status === filterStatus.value)
  if (filterSearch.value.trim()) {
    const q = filterSearch.value.trim().toLowerCase()
    list = list.filter(
      (r) =>
        r.title.toLowerCase().includes(q) ||
        (r.assetNumber ?? '').toLowerCase().includes(q) ||
        (r.workOrderNo ?? '').toLowerCase().includes(q) ||
        r.requesterName.toLowerCase().includes(q),
    )
  }
  if (filterAssetNumber.value.trim()) {
    const asset = filterAssetNumber.value.trim().toLowerCase()
    list = list.filter((r) => (r.assetNumber ?? '').toLowerCase().includes(asset))
  }
  return list
})

const statCounts = computed(() => ({
  pending: requests.value.filter((r) => ['new', 'assigned'].includes(r.status)).length,
  in_progress: requests.value.filter((r) => r.status === 'in_progress').length,
  done: requests.value.filter((r) => ['resolved', 'closed'].includes(r.status)).length,
}))

const historicalAssetNumbers = computed(() => {
  const unique = new Set(
    requests.value
      .map((r) => (r.assetNumber ?? '').trim())
      .filter((asset) => asset.length > 0),
  )
  return Array.from(unique).sort((a, b) => a.localeCompare(b))
})

// ─── Helpers ──────────────────────────────────────────────────────────────────
function formatDate(dateStr: string | null | undefined): string {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString('th-TH', {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

function categoryLabel(val: string) {
  return categoryOptions.find((c) => c.value === val)?.label ?? val
}

function priorityTag(val: string): { severity: 'info' | 'warn' | 'danger'; label: string } {
  if (val === 'low') return { severity: 'info', label: 'ต่ำ' }
  if (val === 'medium') return { severity: 'info', label: 'ปานกลาง' }
  if (val === 'urgent') return { severity: 'warn', label: 'เร่งด่วน' }
  if (val === 'critical') return { severity: 'danger', label: 'วิกฤต' }
  return { severity: 'info', label: val }
}

function statusTag(val: string): { severity: 'secondary' | 'info' | 'success' | 'danger'; label: string } {
  if (val === 'new') return { severity: 'secondary', label: 'รอช่างอาคารประเมิน' }
  if (val === 'assigned') return { severity: 'info', label: 'มอบหมายแล้ว' }
  if (val === 'in_progress') return { severity: 'info', label: 'กำลังดำเนินการ' }
  if (val === 'need_supervisor_review') return { severity: 'danger', label: 'รอหัวหน้าพิจารณา' }
  if (val === 'returned_to_technician') return { severity: 'info', label: 'ส่งกลับให้ช่าง' }
  if (val === 'waiting_department_external_procurement') return { severity: 'danger', label: 'รอหน่วยงาน/กอง จัดจ้างช่างภายนอก' }
  if (val === 'waiting_central_external_procurement') return { severity: 'danger', label: 'รอธุรการส่วนกลาง จัดจ้างช่างภายนอก' }
  if (val === 'external_scheduled') return { severity: 'info', label: 'นัดช่างภายนอกแล้ว' }
  if (val === 'external_in_progress') return { severity: 'info', label: 'ช่างภายนอกกำลังซ่อม' }
  if (val === 'resolved') return { severity: 'success', label: 'ซ่อมเสร็จ' }
  if (val === 'closed') return { severity: 'success', label: 'ปิดงาน' }
  return { severity: 'secondary', label: val }
}

function formatDisplayName(value: string | null | undefined): string {
  const raw = (value || '').trim()
  if (!raw) return '-'
  if (!raw.includes('@')) return raw

  const localPart = raw.split('@')[0] || raw
  return localPart.replace(/[._-]+/g, ' ')
}

// ─── Actions ──────────────────────────────────────────────────────────────────
function openForm() {
  form.value = {
    title: '',
    description: '',
    category: 'building',
    priority: 'medium',
    buildingName: '',
    locationDetail: '',
    assetNumber: '',
    isCentralAsset: false,
    extension: '',
  }
  formError.value = ''
  assetSuggestions.value = []
  formVisible.value = true
}

function completeAssetNumber(event: { query: string }) {
  const query = event.query.trim().toLowerCase()
  if (!query) {
    assetSuggestions.value = historicalAssetNumbers.value.slice(0, 12)
    return
  }
  assetSuggestions.value = historicalAssetNumbers.value
    .filter((asset) => asset.toLowerCase().includes(query))
    .slice(0, 12)
}

async function submitRequest() {
  if (!form.value.title.trim()) { formError.value = 'กรุณาระบุหัวข้อ'; return }
  if (!form.value.description.trim()) { formError.value = 'กรุณาระบุรายละเอียด'; return }
  if (!form.value.locationDetail.trim()) { formError.value = 'กรุณาระบุตำแหน่งที่พบปัญหา'; return }
  saving.value = true
  try {
    const requesterDepartmentCode =
      (authStore.user?.departmentId ?? authStore.userProfile?.departmentId ?? '').toString()

    await api.post('/ServiceRequest', {
      ...form.value,
      status: 'new',
      requesterName: authStore.user?.displayName ?? authStore.user?.email ?? '',
      requesterEmail: authStore.user?.email ?? '',
      requesterUid: authStore.user?.uid ?? authStore.user?.id ?? '',
      requesterDepartmentCode,
      requesterDepartmentName: '',
    })
    formVisible.value = false
    successMsg.value = 'ส่งคำร้องเรียบร้อยแล้ว'
    setTimeout(() => (successMsg.value = ''), 4000)
    await loadRequests()
  } catch {
    formError.value = 'เกิดข้อผิดพลาด กรุณาลองใหม่'
  } finally {
    saving.value = false
  }
}

function openDetail(r: ServiceRequest) {
  router.push(`/maintenance/service/${r.id}`)
}

function openEdit(r: ServiceRequest) {
  selectedRequest.value = r
  editForm.value = {
    status: r.status,
    technicianUid: r.technicianUid ?? r.assignedTo ?? '',
    technicianName: r.technicianName ?? '',
    note: r.note ?? '',
    diagnosis: r.technicianDiagnosis ?? '',
    action: r.technicianAction ?? '',
    escalationReason: r.escalationReason ?? '',
    canRepairInHouse: r.supervisorCanRepairInHouse ?? true,
    supervisorReason: r.supervisorReason ?? '',
    supervisorPlan: r.supervisorRepairPlan ?? '',
    supervisorAdvice: r.supervisorExternalAdvice ?? '',
    vendorName: r.externalVendorName ?? '',
    externalResult: r.externalResult ?? '',
  }
  editVisible.value = true
}

async function saveEdit() {
  if (!selectedRequest.value) return
  saving.value = true
  try {
    const requestId = selectedRequest.value.id

    if (canAssignTechnician.value && editForm.value.technicianUid.trim()) {
      await api.put(`/ServiceRequest/${requestId}/assign-technician`, {
        technicianUid: editForm.value.technicianUid,
        technicianName: editForm.value.technicianName,
      })
    }

    if (canTechnicianAction.value && editForm.value.status === 'resolved') {
      await api.put(`/ServiceRequest/${requestId}/technician-complete`, {
        diagnosis: editForm.value.diagnosis,
        action: editForm.value.action,
        note: editForm.value.note,
      })
    } else if (canTechnicianAction.value && editForm.value.status === 'need_supervisor_review') {
      await api.put(`/ServiceRequest/${requestId}/technician-escalate`, {
        escalationReason: editForm.value.escalationReason,
        diagnosis: editForm.value.diagnosis,
      })
    } else if (canSupervisorAction.value && ['returned_to_technician', 'waiting_department_external_procurement', 'waiting_central_external_procurement'].includes(editForm.value.status)) {
      await api.put(`/ServiceRequest/${requestId}/supervisor-review`, {
        canRepairInHouse: editForm.value.canRepairInHouse,
        reason: editForm.value.supervisorReason,
        repairPlan: editForm.value.supervisorPlan,
        externalAdvice: editForm.value.supervisorAdvice,
      })
    } else if (canAdminBuildingAction.value && ['external_scheduled', 'external_in_progress', 'closed', 'resolved'].includes(editForm.value.status)) {
      await api.put(`/ServiceRequest/${requestId}/external-progress`, {
        vendorName: editForm.value.vendorName,
        scheduledAt: editForm.value.status === 'external_scheduled' ? new Date().toISOString() : null,
        completedAt: ['closed', 'resolved'].includes(editForm.value.status) ? new Date().toISOString() : null,
        result: editForm.value.externalResult,
        closeAfterComplete: editForm.value.status === 'closed',
      })
    } else {
      await api.put(`/ServiceRequest/${requestId}`, {
        ...selectedRequest.value,
        status: editForm.value.status,
        technicianUid: editForm.value.technicianUid,
        technicianName: editForm.value.technicianName,
        note: editForm.value.note,
      })
    }

    logAudit(
      { uid: authStore.user?.uid ?? '', displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.userProfile?.role ?? 'user' },
      'UPDATE', 'ServiceRequest', `อัปเดตสถานะ [${selectedRequest.value.id}] → ${editForm.value.status}`,
    )
    editVisible.value = false
    successMsg.value = 'อัปเดตสถานะเรียบร้อยแล้ว'
    setTimeout(() => (successMsg.value = ''), 4000)
    await loadRequests()
  } catch {
    alert('เกิดข้อผิดพลาด')
  } finally {
    saving.value = false
  }
}

function confirmDelete(r: ServiceRequest) {
  requestToDelete.value = r
  deleteVisible.value = true
}

async function deleteRequest() {
  if (!requestToDelete.value) return
  try {
    const req = requestToDelete.value
    await api.delete(`/ServiceRequest/${req.id}`)
    logAudit(
      { uid: authStore.user?.uid ?? '', displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.userProfile?.role ?? 'user' },
      'DELETE', 'ServiceRequest', `ลบคำร้อง: ${req.title} (${req.requesterEmail})`,
    )
    deleteVisible.value = false
    successMsg.value = 'ลบคำร้องเรียบร้อยแล้ว'
    setTimeout(() => (successMsg.value = ''), 4000)
    await loadRequests()
  } catch {
    alert('เกิดข้อผิดพลาด')
  }
}

async function openAssetHistory(assetNumber?: string) {
  const normalized = (assetNumber ?? '').trim()
  if (!normalized) return

  assetHistoryAssetNo.value = normalized
  assetHistoryVisible.value = true
  assetHistoryLoading.value = true
  try {
    const res = await api.get(`/ServiceRequest/asset/${encodeURIComponent(normalized)}/history`)
    assetHistoryItems.value = res.data.items ?? []
  } catch {
    assetHistoryItems.value = []
  } finally {
    assetHistoryLoading.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900">ระบบแจ้งซ่อมงานอาคารออนไลน์</h1>
        <p class="text-sm text-gray-400 mt-1">แจ้งซ่อม ติดตามงาน และดูประวัติการซ่อมตามเลขสินทรัพย์</p>
      </div>
      <Button label="แจ้งซ่อม" icon="pi pi-plus" @click="openForm" />
    </div>

    <!-- Success -->
    <Message v-if="successMsg" severity="success" :closable="false">{{ successMsg }}</Message>

    <!-- Stat cards -->
    <div class="grid grid-cols-3 gap-4">
      <div
        class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3 cursor-pointer hover:border-yellow-300 transition-colors"
        :class="filterStatus === 'new' ? 'border-yellow-400 bg-yellow-50' : ''"
        @click="filterStatus = filterStatus === 'new' ? null : 'new'">
        <div class="w-10 h-10 bg-yellow-100 rounded-lg flex items-center justify-center">
          <i class="pi pi-clock text-yellow-600"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-gray-900">{{ statCounts.pending }}</p>
          <p class="text-xs text-gray-400">งานใหม่/รอมอบหมาย</p>
        </div>
      </div>
      <div
        class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3 cursor-pointer hover:border-blue-300 transition-colors"
        :class="filterStatus === 'in_progress' ? 'border-blue-400 bg-blue-50' : ''"
        @click="filterStatus = filterStatus === 'in_progress' ? null : 'in_progress'">
        <div class="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
          <i class="pi pi-sync text-blue-600"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-gray-900">{{ statCounts.in_progress }}</p>
          <p class="text-xs text-gray-400">กำลังดำเนินการ</p>
        </div>
      </div>
      <div
        class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3 cursor-pointer hover:border-green-300 transition-colors"
        :class="filterStatus === 'resolved' ? 'border-green-400 bg-green-50' : ''"
        @click="filterStatus = filterStatus === 'resolved' ? null : 'resolved'">
        <div class="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
          <i class="pi pi-check-circle text-green-600"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-gray-900">{{ statCounts.done }}</p>
          <p class="text-xs text-gray-400">ซ่อมเสร็จ/ปิดงาน</p>
        </div>
      </div>
    </div>

    <!-- Table -->
    <Card>
      <template #content>
        <!-- Filters -->
        <div class="flex gap-3 mb-4">
          <InputText v-model="filterSearch" placeholder="ค้นหาเลขใบงาน, หัวข้อ, ผู้แจ้ง..." class="flex-1" />
          <InputText v-model="filterAssetNumber" placeholder="กรองเลขสินทรัพย์" class="w-52" />
          <Select v-model="filterStatus" :options="[{ label: 'ทุกสถานะ', value: null }, ...statusOptions]"
            option-label="label" option-value="value" placeholder="กรองสถานะ" class="w-48" />
        </div>

        <DataTable :value="filteredRequests" :loading="loading" paginator :rows="15" size="small" striped-rows
          empty-message="ไม่มีคำร้อง">
          <Column field="workOrderNo" header="เลขใบงาน" style="width: 10rem">
            <template #body="{ data }">
              <span class="font-mono text-xs">{{ data.workOrderNo || '-' }}</span>
            </template>
          </Column>
          <Column field="createdAt" header="วันที่" style="width: 9rem">
            <template #body="{ data }">
              <span class="text-xs text-gray-500">{{ formatDate(data.createdAt) }}</span>
            </template>
          </Column>
          <Column field="title" header="หัวข้อ">
            <template #body="{ data }">
              <span class="font-medium text-gray-800">{{ data.title }}</span>
              <p class="text-xs text-gray-400 truncate max-w-xs">{{ data.description }}</p>
            </template>
          </Column>
          <Column field="category" header="ประเภท" style="width: 10rem">
            <template #body="{ data }">
              <span class="text-sm text-gray-600">{{ categoryLabel(data.category) }}</span>
            </template>
          </Column>
          <Column field="assetNumber" header="เลขสินทรัพย์" style="width: 9rem">
            <template #body="{ data }">
              <Button v-if="data.assetNumber" :label="data.assetNumber" link size="small" class="p-0!"
                @click="openAssetHistory(data.assetNumber)" />
              <span v-else class="font-mono text-sm">-</span>
            </template>
          </Column>
          <Column field="buildingName" header="อาคาร/จุด" style="width: 12rem">
            <template #body="{ data }">
              <div class="text-xs text-gray-600">{{ data.buildingName || '-' }}</div>
              <div class="text-xs text-gray-400 truncate max-w-44">{{ data.locationDetail || '-' }}</div>
            </template>
          </Column>
          <Column field="priority" header="ความสำคัญ" style="width: 7rem">
            <template #body="{ data }">
              <Tag :severity="priorityTag(data.priority).severity" :value="priorityTag(data.priority).label" />
            </template>
          </Column>
          <Column field="status" header="สถานะ" style="width: 9rem">
            <template #body="{ data }">
              <Tag :severity="statusTag(data.status).severity" :value="statusTag(data.status).label" />
            </template>
          </Column>
          <Column field="requesterName" header="ผู้แจ้ง" style="width: 9rem">
            <template #body="{ data }">
              <span class="text-sm text-gray-600">{{ formatDisplayName(data.requesterName) }}</span>
            </template>
          </Column>
          <Column header="จัดการ" style="width: 8rem">
            <template #body="{ data }">
              <div class="flex gap-1">
                <Button icon="pi pi-comments" text rounded size="small" severity="secondary" @click="openDetail(data)"
                  v-tooltip="'แชท / ดูรายละเอียด'" />
                <Button v-if="isAdmin" icon="pi pi-pencil" text rounded size="small" severity="info"
                  @click="openEdit(data)" v-tooltip="'อัปเดตสถานะ'" />
                <Button v-if="isAdmin" icon="pi pi-trash" text rounded size="small" severity="danger"
                  @click="confirmDelete(data)" v-tooltip="'ลบ'" />
              </div>
            </template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <!-- ── New Request Dialog ── -->
    <Dialog v-model:visible="formVisible" header="แจ้งซ่อมงานอาคาร" modal :style="{ width: '42rem' }">
      <div class="space-y-4">
        <Message v-if="formError" severity="error" :closable="false">{{ formError }}</Message>

        <div class="grid grid-cols-2 gap-4">
          <div class="col-span-2 flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">หัวข้อ <span class="text-red-500">*</span></label>
            <InputText v-model="form.title" placeholder="ระบุหัวข้อปัญหา" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">ประเภท</label>
            <Select v-model="form.category" :options="categoryOptions" option-label="label" option-value="value" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">ความสำคัญ</label>
            <Select v-model="form.priority" :options="priorityOptions" option-label="label" option-value="value" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">อาคาร</label>
            <InputText v-model="form.buildingName" placeholder="เช่น อาคาร A" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">ตำแหน่งที่พบปัญหา <span
                class="text-red-500">*</span></label>
            <InputText v-model="form.locationDetail" placeholder="เช่น ชั้น 2 ห้องประชุม" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">เลขสินทรัพย์</label>
            <AutoComplete v-model="form.assetNumber" :suggestions="assetSuggestions" dropdown
              placeholder="พิมพ์เพื่อค้นหาเลขสินทรัพย์เดิม..." @complete="completeAssetNumber" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">ประเภททรัพย์สิน</label>
            <Select v-model="form.isCentralAsset"
              :options="[{ label: 'ทรัพย์สินของกอง/หน่วยงาน', value: false }, { label: 'ทรัพย์สินส่วนกลาง', value: true }]"
              option-label="label" option-value="value" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">เบอร์ติดต่อกลับ (ถ้ามี)</label>
            <InputText v-model="form.extension" placeholder="เช่น 1234" />
          </div>
          <div class="col-span-2 flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">รายละเอียด <span class="text-red-500">*</span></label>
            <Textarea v-model="form.description" rows="4" placeholder="อธิบายปัญหาหรือสิ่งที่ต้องการ..."
              class="w-full" />
          </div>
        </div>
      </div>
      <template #footer>
        <Button label="ยกเลิก" text @click="formVisible = false" />
        <Button label="ส่งใบแจ้งซ่อม" icon="pi pi-send" :loading="saving" @click="submitRequest" />
      </template>
    </Dialog>

    <!-- ── Edit Status Dialog (admin) ── -->
    <Dialog v-model:visible="editVisible" header="อัปเดต Workflow งานซ่อม" modal :style="{ width: '38rem' }">
      <div class="space-y-4">
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">สถานะ</label>
          <Select v-model="editForm.status" :options="statusOptions" option-label="label" option-value="value" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">รหัสช่าง</label>
          <InputText v-model="editForm.technicianUid" placeholder="เช่น tech-001" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">ชื่อช่าง</label>
          <InputText v-model="editForm.technicianName" placeholder="ชื่อผู้รับผิดชอบ" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">ผลวิเคราะห์/สาเหตุ</label>
          <Textarea v-model="editForm.diagnosis" rows="2" class="w-full" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">วิธีซ่อม/การแก้ไข</label>
          <Textarea v-model="editForm.action" rows="2" class="w-full" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">เหตุผลส่งหัวหน้างาน</label>
          <Textarea v-model="editForm.escalationReason" rows="2" class="w-full" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">หัวหน้าประเมินว่าซ่อมภายในได้หรือไม่</label>
          <Select v-model="editForm.canRepairInHouse"
            :options="[{ label: 'ซ่อมได้', value: true }, { label: 'ซ่อมไม่ได้', value: false }]" option-label="label"
            option-value="value" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">เหตุผลหัวหน้างาน</label>
          <Textarea v-model="editForm.supervisorReason" rows="2" class="w-full" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">แผนซ่อมที่ส่งกลับช่าง</label>
          <Textarea v-model="editForm.supervisorPlan" rows="2" class="w-full" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">คำแนะนำกรณีซ่อมภายในไม่ได้</label>
          <Textarea v-model="editForm.supervisorAdvice" rows="2" class="w-full" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">บริษัทภายนอก</label>
          <InputText v-model="editForm.vendorName" placeholder="ชื่อบริษัท" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">ผลการซ่อมภายนอก</label>
          <Textarea v-model="editForm.externalResult" rows="2" class="w-full" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">หมายเหตุ</label>
          <Textarea v-model="editForm.note" rows="3" placeholder="บันทึกเพิ่มเติม..." class="w-full" />
        </div>
      </div>
      <template #footer>
        <Button label="ยกเลิก" text @click="editVisible = false" />
        <Button label="บันทึก" icon="pi pi-check" :loading="saving" @click="saveEdit" />
      </template>
    </Dialog>

    <!-- ── Delete Confirm Dialog ── -->
    <Dialog v-model:visible="deleteVisible" header="ยืนยันการลบ" modal :style="{ width: '28rem' }">
      <div class="flex items-start gap-3">
        <div class="w-10 h-10 bg-red-100 rounded-full flex items-center justify-center shrink-0">
          <i class="pi pi-exclamation-triangle text-red-600"></i>
        </div>
        <div>
          <p class="text-gray-800 font-medium">ต้องการลบคำร้องนี้ใช่หรือไม่?</p>
          <p class="text-sm text-gray-500 mt-1">{{ requestToDelete?.title }}</p>
          <p class="text-xs text-red-500 mt-2">การกระทำนี้ไม่สามารถย้อนกลับได้</p>
        </div>
      </div>
      <template #footer>
        <Button label="ยกเลิก" text @click="deleteVisible = false" />
        <Button label="ลบ" icon="pi pi-trash" severity="danger" @click="deleteRequest" />
      </template>
    </Dialog>

    <!-- ── Asset History Dialog ── -->
    <Dialog v-model:visible="assetHistoryVisible" header="ประวัติการซ่อมตามเลขสินทรัพย์" modal
      :style="{ width: '70rem' }">
      <div class="mb-3 text-sm text-gray-600">
        เลขสินทรัพย์: <span class="font-mono font-semibold">{{ assetHistoryAssetNo }}</span>
      </div>
      <DataTable :value="assetHistoryItems" :loading="assetHistoryLoading" paginator :rows="10" size="small"
        empty-message="ไม่พบประวัติการซ่อม">
        <Column field="workOrderNo" header="เลขใบงาน" style="width: 10rem" />
        <Column field="createdAt" header="วันที่แจ้ง" style="width: 10rem">
          <template #body="{ data }">{{ formatDate(data.createdAt) }}</template>
        </Column>
        <Column field="title" header="หัวข้อ" />
        <Column field="status" header="สถานะ" style="width: 12rem">
          <template #body="{ data }">
            <Tag :severity="statusTag(data.status).severity" :value="statusTag(data.status).label" />
          </template>
        </Column>
        <Column field="technicianAction" header="ผลซ่อม" />
      </DataTable>
      <template #footer>
        <Button label="ปิด" text @click="assetHistoryVisible = false" />
      </template>
    </Dialog>
  </div>
</template>
