<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

defineOptions({ name: 'SarabanSystem' })

const toast = useAppToast()

import Card from 'primevue/card'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import Select from 'primevue/select'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import Dialog from 'primevue/dialog'

// ─── Interfaces ───────────────────────────────────────────────────────────────
interface FetchedSarabanRecord {
  id: string
  docType: string
  docNumber: string
  subject: string
  fromOrganization: string
  toOrganization: string
  receivedDate: string | null
  dueDate: string | null
  priority: string
  status: string
  assignedTo: string
  note: string
  recordedBy: string
  departmentId: string
  createdAt: string
}

interface Department { id: string; name: string }

// ─── State ────────────────────────────────────────────────────────────────────
const authStore = useAuthStore()
const { isSuperAdmin, isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('saraban')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const departments = ref<Department[]>([])
const historyRecords = ref<FetchedSarabanRecord[]>([])
const isLoadingHistory = ref(true)
const isLoadingMore = ref(false)
const hasMore = ref(true)
const skip = ref(0)
const take = 20

// --- History Filters ---
const filterDateFrom = ref<Date | null>(null)
const filterDateTo = ref<Date | null>(null)
const filterDeptId = ref('')
const filterStatus = ref('')
const filterDocType = ref('')

// ─── Form (Add new) ───────────────────────────────────────────────────────────
const formData = ref({
  departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
  docType: '',
  docNumber: '',
  subject: '',
  fromOrganization: '',
  toOrganization: '',
  receivedDate: null as Date | null,
  dueDate: null as Date | null,
  priority: 'normal',
  status: 'pending',
  assignedTo: '',
  note: '',
})

const isSubmitting = ref(false)
const successMessage = ref('')
const errorMessage = ref('')

const docTypeOptions = [
  { label: 'หนังสือรับ', value: 'received' },
  { label: 'หนังสือส่ง', value: 'sent' },
  { label: 'หนังสือเวียน', value: 'circular' },
  { label: 'บันทึกข้อความ', value: 'memo' },
]

const priorityOptions = [
  { label: 'ปกติ', value: 'normal' },
  { label: 'ด่วน', value: 'urgent' },
  { label: 'ด่วนมาก', value: 'very_urgent' },
  { label: 'ด่วนที่สุด', value: 'most_urgent' },
]

const statusOptions = [
  { label: 'รอดำเนินการ', value: 'pending' },
  { label: 'กำลังดำเนินการ', value: 'in_progress' },
  { label: 'เสร็จสิ้น', value: 'completed' },
]

// ─── Submit ───────────────────────────────────────────────────────────────────
const submitForm = async (): Promise<void> => {
  successMessage.value = ''; errorMessage.value = ''
  if (!formData.value.receivedDate) { errorMessage.value = 'กรุณาเลือกวันที่รับหนังสือ'; return }
  const saveDeptId = isSuperAdmin.value ? formData.value.departmentId : currentUserDepartment.value
  if (!saveDeptId) { errorMessage.value = 'กรุณาเลือกหน่วยงาน'; return }
  if (!formData.value.subject.trim()) { errorMessage.value = 'กรุณากรอกเรื่อง'; return }

  isSubmitting.value = true
  try {
    const payload = {
      departmentId: saveDeptId,
      docType: formData.value.docType,
      docNumber: formData.value.docNumber,
      subject: formData.value.subject,
      fromOrganization: formData.value.fromOrganization,
      toOrganization: formData.value.toOrganization,
      receivedDate: formData.value.receivedDate.toISOString(),
      dueDate: formData.value.dueDate ? formData.value.dueDate.toISOString() : null,
      priority: formData.value.priority,
      status: formData.value.status,
      assignedTo: formData.value.assignedTo,
      note: formData.value.note,
      recordedBy: authStore.user?.uid || '',
    }

    await api.post('/SarabanRecord', payload)
    successMessage.value = 'บันทึกข้อมูลสารบรรณสำเร็จ'
    formData.value = {
      departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
      docType: '', docNumber: '', subject: '',
      fromOrganization: '', toOrganization: '',
      receivedDate: null, dueDate: null,
      priority: 'normal', status: 'pending',
      assignedTo: '', note: '',
    }
    handleFilterChange()
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? `เกิดข้อผิดพลาด: ${e.message}` : 'เกิดข้อผิดพลาด'
  } finally {
    isSubmitting.value = false
  }
}

// ─── Detail / Edit / Delete dialogs ──────────────────────────────────────────
const detailVisible = ref(false)
const editVisible = ref(false)
const selectedRecord = ref<FetchedSarabanRecord | null>(null)
const isUpdating = ref(false)
const deleteConfirmVisible = ref(false)
const recordToDelete = ref<FetchedSarabanRecord | null>(null)

const editForm = ref({
  departmentId: '', docType: '', docNumber: '', subject: '',
  fromOrganization: '', toOrganization: '',
  receivedDate: null as Date | null, dueDate: null as Date | null,
  priority: 'normal', status: 'pending', assignedTo: '', note: '',
})

const openDetail = (record: FetchedSarabanRecord) => { selectedRecord.value = record; detailVisible.value = true }
const openEdit = (record: FetchedSarabanRecord) => {
  selectedRecord.value = record
  editForm.value = {
    departmentId: record.departmentId,
    docType: record.docType,
    docNumber: record.docNumber,
    subject: record.subject,
    fromOrganization: record.fromOrganization,
    toOrganization: record.toOrganization,
    receivedDate: record.receivedDate ? new Date(record.receivedDate) : null,
    dueDate: record.dueDate ? new Date(record.dueDate) : null,
    priority: record.priority,
    status: record.status,
    assignedTo: record.assignedTo,
    note: record.note,
  }
  editVisible.value = true
}

const saveEdit = async (): Promise<void> => {
  if (!selectedRecord.value) return
  if (!editForm.value.subject.trim()) {
    toast.error('กรุณากรอกเรื่อง'); return
  }
  isUpdating.value = true
  try {
    const payload = {
      departmentId: editForm.value.departmentId,
      docType: editForm.value.docType,
      docNumber: editForm.value.docNumber,
      subject: editForm.value.subject,
      fromOrganization: editForm.value.fromOrganization,
      toOrganization: editForm.value.toOrganization,
      receivedDate: editForm.value.receivedDate ? editForm.value.receivedDate.toISOString() : null,
      dueDate: editForm.value.dueDate ? editForm.value.dueDate.toISOString() : null,
      priority: editForm.value.priority,
      status: editForm.value.status,
      assignedTo: editForm.value.assignedTo,
      note: editForm.value.note,
    }
    await api.put(`/SarabanRecord/${selectedRecord.value.id}`, payload)
    const index = historyRecords.value.findIndex(r => r.id === selectedRecord.value!.id)
    if (index !== -1) {
      historyRecords.value[index] = { ...historyRecords.value[index], ...payload, receivedDate: payload.receivedDate || null, dueDate: payload.dueDate || null }
    }
    editVisible.value = false
    toast.success('แก้ไขข้อมูลสำเร็จ')
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่')
  } finally {
    isUpdating.value = false
  }
}

const confirmDelete = (record: FetchedSarabanRecord) => {
  recordToDelete.value = record; deleteConfirmVisible.value = true
}
const deleteRecord = async (): Promise<void> => {
  if (!recordToDelete.value) return
  try {
    await api.delete(`/SarabanRecord/${recordToDelete.value.id}`)
    historyRecords.value = historyRecords.value.filter(r => r.id !== recordToDelete.value!.id)
    toast.success('ลบข้อมูลสำเร็จ')
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่')
  } finally {
    deleteConfirmVisible.value = false
    recordToDelete.value = null
  }
}

// ─── Fetch Data ───────────────────────────────────────────────────────────────
const fetchHistory = async (loadMore = false): Promise<void> => {
  if (!hasMore.value && loadMore) return
  if (loadMore) isLoadingMore.value = true
  else isLoadingHistory.value = true

  try {
    const params: Record<string, unknown> = {
      skip: loadMore ? skip.value : 0,
      take,
    }

    const saveDeptId = isAdmin
      ? (filterDeptId.value || undefined)
      : currentUserDepartment.value

    if (saveDeptId) params.departmentId = saveDeptId
    if (filterStatus.value) params.status = filterStatus.value
    if (filterDocType.value) params.docType = filterDocType.value

    const response = await api.get('/SarabanRecord', { params })
    const newRecords: FetchedSarabanRecord[] = response.data

    if (loadMore) historyRecords.value.push(...newRecords)
    else historyRecords.value = newRecords

    hasMore.value = newRecords.length >= take
    if (loadMore) skip.value += newRecords.length
    else skip.value = newRecords.length
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูลสารบรรณได้')
    hasMore.value = false
  } finally {
    if (loadMore) isLoadingMore.value = false
    else isLoadingHistory.value = false
  }
}

const handleFilterChange = () => {
  skip.value = 0
  hasMore.value = true
  historyRecords.value = []
  fetchHistory(false)
}

watch([filterDateFrom, filterDateTo, filterDeptId, filterStatus, filterDocType], handleFilterChange)

onMounted(async () => {
  handleFilterChange()
  try {
    const deptRes = await api.get('/Department')
    departments.value = deptRes.data
  } catch (e: unknown) {
    toast.fromError(e, 'ไม่สามารถโหลดข้อมูลตั้งต้นได้')
  }
})

// ─── Helpers ──────────────────────────────────────────────────────────────────
const getDeptName = (id: string) => departments.value.find((x) => x.id === id)?.name || id
const formatThaiDate = (dateStr: string | null | undefined) =>
  dateStr ? new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'long', day: 'numeric' }) : '-'
const formatDateTime = (dateStr: string | null | undefined) =>
  dateStr ? new Date(dateStr).toLocaleString('th-TH', { dateStyle: 'medium', timeStyle: 'short' }) : '-'

const getDocTypeLabel = (val: string) => docTypeOptions.find(o => o.value === val)?.label || val
const getPriorityLabel = (val: string) => priorityOptions.find(o => o.value === val)?.label || val
const getStatusLabel = (val: string) => statusOptions.find(o => o.value === val)?.label || val
const getStatusSeverity = (val: string) => {
  if (val === 'completed') return 'text-green-600'
  if (val === 'in_progress') return 'text-blue-600'
  return 'text-orange-500'
}
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10">
    <div class="mb-6 flex flex-col sm:flex-row sm:justify-between sm:items-end gap-4">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          <i class="pi pi-folder-open text-purple-600 mr-2"></i>บันทึกสถิติงานสารบรรณ
        </h2>
        <p class="text-gray-500 mt-1">บันทึกและติดตามเอกสารรับ-ส่ง</p>
      </div>
    </div>

    <Tabs value="0" lazy>
      <TabList>
        <Tab value="0"><i class="pi pi-file-edit mr-2"></i>บันทึกข้อมูล</Tab>
        <Tab value="1">
          <i class="pi pi-history mr-2"></i>ประวัติย้อนหลัง
          <span v-if="historyRecords.length > 0"
            class="ml-2 bg-purple-100 text-purple-700 px-2 py-0.5 rounded-full text-xs">
            {{ historyRecords.length }}
          </span>
        </Tab>
      </TabList>

      <TabPanels>
        <!-- Tab 0: Add Record -->
        <TabPanel value="0">
          <Card class="shadow-sm border-none mt-2">
            <template #content>
              <form @submit.prevent="submitForm" class="flex flex-col gap-6">
                <Message v-if="successMessage" severity="success" :closable="true">{{ successMessage }}</Message>
                <Message v-if="errorMessage" severity="error" :closable="true">{{ errorMessage }}</Message>

                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 p-4 bg-gray-50 rounded-xl border border-gray-200">
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">
                      <i class="pi pi-calendar mr-1 text-purple-500"></i>วันที่รับหนังสือ
                      <span class="text-red-500">*</span>
                    </label>
                    <DatePicker v-model="formData.receivedDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">
                      <i class="pi pi-building mr-1 text-purple-500"></i>หน่วยงาน
                      <span class="text-red-500">*</span>
                    </label>
                    <Select v-if="isSuperAdmin" v-model="formData.departmentId" :options="departments" optionLabel="name"
                      optionValue="id" placeholder="-- เลือกหน่วยงาน --" class="w-full" />
                    <InputText v-else :value="getDeptName(currentUserDepartment)" disabled
                      class="w-full bg-gray-100 font-bold" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">
                      <i class="pi pi-tag mr-1 text-purple-500"></i>ประเภทเอกสาร
                    </label>
                    <Select v-model="formData.docType" :options="docTypeOptions" optionLabel="label" optionValue="value"
                      placeholder="-- เลือกประเภท --" class="w-full" showClear />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">เลขที่หนังสือ</label>
                    <InputText v-model="formData.docNumber" placeholder="เช่น นร 0001/2567" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">ความเร่งด่วน</label>
                    <Select v-model="formData.priority" :options="priorityOptions" optionLabel="label" optionValue="value" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">สถานะ</label>
                    <Select v-model="formData.status" :options="statusOptions" optionLabel="label" optionValue="value" class="w-full" />
                  </div>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">เรื่อง <span class="text-red-500">*</span></label>
                    <InputText v-model="formData.subject" placeholder="เรื่องของหนังสือ" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">ผู้รับผิดชอบ</label>
                    <InputText v-model="formData.assignedTo" placeholder="ชื่อผู้รับผิดชอบ" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">จาก (หน่วยงาน/บุคคล)</label>
                    <InputText v-model="formData.fromOrganization" placeholder="หน่วยงานต้นทาง" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">ถึง (หน่วยงาน/บุคคล)</label>
                    <InputText v-model="formData.toOrganization" placeholder="หน่วยงานปลายทาง" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">วันที่ครบกำหนด</label>
                    <DatePicker v-model="formData.dueDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
                  </div>
                </div>

                <div class="flex flex-col gap-2">
                  <label class="font-semibold text-sm text-gray-700">หมายเหตุ</label>
                  <InputText v-model="formData.note" placeholder="รายละเอียดเพิ่มเติม" class="w-full" />
                </div>

                <div class="flex justify-end border-t border-gray-100 pt-4">
                  <Button type="submit" label="บันทึกข้อมูลสารบรรณ" icon="pi pi-save" severity="help"
                    :loading="isSubmitting" class="px-8 py-3 text-base" />
                </div>
              </form>
            </template>
          </Card>
        </TabPanel>

        <!-- Tab 1: History -->
        <TabPanel value="1">
          <Card class="shadow-sm border-none mt-2 overflow-hidden">
            <template #content>
              <!-- Filters -->
              <div class="flex flex-wrap gap-3 mb-4 p-3 bg-gray-50 rounded-xl border border-gray-100">
                <div v-if="isAdmin" class="flex flex-col gap-1 min-w-[180px]">
                  <label class="text-xs font-semibold text-gray-500">หน่วยงาน</label>
                  <Select v-model="filterDeptId" :options="[{id:'',name:'ทุกหน่วยงาน'},...departments]"
                    optionLabel="name" optionValue="id" class="w-full" />
                </div>
                <div class="flex flex-col gap-1 min-w-[160px]">
                  <label class="text-xs font-semibold text-gray-500">ประเภทเอกสาร</label>
                  <Select v-model="filterDocType" :options="[{label:'ทุกประเภท',value:''},...docTypeOptions]"
                    optionLabel="label" optionValue="value" class="w-full" />
                </div>
                <div class="flex flex-col gap-1 min-w-[160px]">
                  <label class="text-xs font-semibold text-gray-500">สถานะ</label>
                  <Select v-model="filterStatus" :options="[{label:'ทุกสถานะ',value:''},...statusOptions]"
                    optionLabel="label" optionValue="value" class="w-full" />
                </div>
              </div>

              <DataTable :value="historyRecords" :loading="isLoadingHistory" paginator :rows="10" stripedRows
                responsiveLayout="scroll" emptyMessage="ยังไม่มีข้อมูล">

                <Column v-if="isAdmin" header="หน่วยงาน" style="min-width: 130px">
                  <template #body="{ data }">
                    <div class="font-bold text-gray-700 text-sm">{{ getDeptName(data.departmentId) }}</div>
                    <div class="text-xs text-gray-400"><i class="pi pi-user mr-1"></i>{{ data.recordedBy }}</div>
                  </template>
                </Column>

                <Column header="วันที่รับ" style="min-width: 130px">
                  <template #body="{ data }">
                    <div class="font-semibold text-gray-800 text-sm">{{ formatThaiDate(data.receivedDate) }}</div>
                  </template>
                </Column>

                <Column header="ประเภท" style="min-width: 120px">
                  <template #body="{ data }">
                    <div class="text-sm text-gray-600">{{ getDocTypeLabel(data.docType) || '-' }}</div>
                  </template>
                </Column>

                <Column header="เรื่อง" style="min-width: 200px">
                  <template #body="{ data }">
                    <div class="font-semibold text-gray-800">{{ data.subject || '-' }}</div>
                    <div class="text-xs text-gray-400">{{ data.docNumber || '' }}</div>
                  </template>
                </Column>

                <Column header="สถานะ" style="width: 120px">
                  <template #body="{ data }">
                    <span :class="['font-semibold text-sm', getStatusSeverity(data.status)]">
                      {{ getStatusLabel(data.status) }}
                    </span>
                  </template>
                </Column>

                <Column header="จัดการ" style="width: 110px">
                  <template #body="{ data }">
                    <div class="flex gap-1">
                      <Button icon="pi pi-eye" text rounded severity="info" size="small" v-tooltip.top="'ดูรายละเอียด'"
                        @click="openDetail(data)" />
                      <Button v-if="isAdmin" icon="pi pi-pencil" text rounded severity="secondary" size="small"
                        v-tooltip.top="'แก้ไข'" @click="openEdit(data)" />
                      <Button v-if="isAdmin" icon="pi pi-trash" text rounded severity="danger" size="small"
                        v-tooltip.top="'ลบ'" @click="confirmDelete(data)" />
                    </div>
                  </template>
                </Column>
              </DataTable>
              <div class="flex justify-center mt-4">
                <Button v-if="hasMore" label="โหลดเพิ่มเติม" icon="pi pi-chevron-down" severity="secondary" outlined
                  :loading="isLoadingMore" @click="fetchHistory(true)" />
                <p v-else-if="historyRecords.length > 0" class="text-xs text-gray-400 py-2">
                  แสดงทั้งหมด {{ historyRecords.length }} รายการ
                </p>
              </div>
            </template>
          </Card>
        </TabPanel>
      </TabPanels>
    </Tabs>

    <!-- Detail Dialog -->
    <Dialog v-model:visible="detailVisible" modal header="รายละเอียดข้อมูลสารบรรณ" :style="{ width: '480px' }"
      :draggable="false">
      <div v-if="selectedRecord" class="flex flex-col gap-4">
        <div class="flex items-center gap-3 p-4 bg-purple-50 rounded-xl">
          <div class="w-12 h-12 bg-purple-100 rounded-xl flex items-center justify-center">
            <i class="pi pi-file text-purple-600 text-xl"></i>
          </div>
          <div>
            <p class="font-bold text-gray-800 text-lg">{{ selectedRecord.subject || '-' }}</p>
            <p class="text-sm text-gray-500">{{ formatThaiDate(selectedRecord.receivedDate) }}</p>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3 text-sm">
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">หน่วยงาน</p>
            <p class="font-semibold text-gray-800">{{ getDeptName(selectedRecord.departmentId) }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">ประเภทเอกสาร</p>
            <p class="font-semibold text-gray-800">{{ getDocTypeLabel(selectedRecord.docType) || '-' }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">เลขที่หนังสือ</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.docNumber || '-' }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">ความเร่งด่วน</p>
            <p class="font-semibold text-gray-800">{{ getPriorityLabel(selectedRecord.priority) }}</p>
          </div>
          <div class="bg-purple-50 rounded-lg p-3">
            <p class="text-xs text-purple-400 mb-1">สถานะ</p>
            <p :class="['font-bold text-lg', getStatusSeverity(selectedRecord.status)]">{{ getStatusLabel(selectedRecord.status) }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">ผู้รับผิดชอบ</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.assignedTo || '-' }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">จาก</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.fromOrganization || '-' }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">ถึง</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.toOrganization || '-' }}</p>
          </div>
          <div v-if="selectedRecord.dueDate" class="bg-orange-50 rounded-lg p-3 col-span-2">
            <p class="text-xs text-orange-400 mb-1">วันครบกำหนด</p>
            <p class="font-bold text-orange-700">{{ formatThaiDate(selectedRecord.dueDate) }}</p>
          </div>
          <div v-if="selectedRecord.note" class="bg-gray-50 rounded-lg p-3 col-span-2">
            <p class="text-xs text-gray-400 mb-1">หมายเหตุ</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.note }}</p>
          </div>
        </div>

        <div class="border-t pt-3 text-xs text-gray-400 flex justify-between">
          <span><i class="pi pi-user mr-1"></i>{{ selectedRecord.recordedBy }}</span>
          <span>{{ formatDateTime(selectedRecord.createdAt) }}</span>
        </div>
      </div>
      <template #footer>
        <Button label="ปิด" severity="secondary" text @click="detailVisible = false" />
        <Button v-if="isAdmin && selectedRecord" label="แก้ไข" icon="pi pi-pencil" severity="help"
          @click="detailVisible = false; openEdit(selectedRecord!)" />
      </template>
    </Dialog>

    <!-- Edit Dialog -->
    <Dialog v-model:visible="editVisible" modal header="แก้ไขข้อมูลสารบรรณ" :style="{ width: '560px' }"
      :draggable="false">
      <div class="flex flex-col gap-4 mt-2">
        <div class="grid grid-cols-2 gap-4">
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">วันที่รับหนังสือ <span class="text-red-500">*</span></label>
            <DatePicker v-model="editForm.receivedDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">หน่วยงาน</label>
            <Select v-if="isSuperAdmin" v-model="editForm.departmentId" :options="departments" optionLabel="name"
              optionValue="id" class="w-full" />
            <InputText v-else :value="getDeptName(editForm.departmentId)" disabled class="w-full bg-gray-50" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ประเภทเอกสาร</label>
            <Select v-model="editForm.docType" :options="docTypeOptions" optionLabel="label" optionValue="value" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">เลขที่หนังสือ</label>
            <InputText v-model="editForm.docNumber" class="w-full" />
          </div>
          <div class="flex flex-col gap-2 col-span-2">
            <label class="text-sm font-semibold text-gray-700">เรื่อง <span class="text-red-500">*</span></label>
            <InputText v-model="editForm.subject" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">จาก</label>
            <InputText v-model="editForm.fromOrganization" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ถึง</label>
            <InputText v-model="editForm.toOrganization" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ความเร่งด่วน</label>
            <Select v-model="editForm.priority" :options="priorityOptions" optionLabel="label" optionValue="value" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">สถานะ</label>
            <Select v-model="editForm.status" :options="statusOptions" optionLabel="label" optionValue="value" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ผู้รับผิดชอบ</label>
            <InputText v-model="editForm.assignedTo" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">วันครบกำหนด</label>
            <DatePicker v-model="editForm.dueDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
          </div>
          <div class="flex flex-col gap-2 col-span-2">
            <label class="text-sm font-semibold text-gray-700">หมายเหตุ</label>
            <InputText v-model="editForm.note" class="w-full" />
          </div>
        </div>
      </div>
      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="editVisible = false" />
        <Button label="บันทึกการแก้ไข" icon="pi pi-check" severity="help" :loading="isUpdating" @click="saveEdit" />
      </template>
    </Dialog>

    <!-- Delete Confirm Dialog -->
    <Dialog v-model:visible="deleteConfirmVisible" modal header="ยืนยันการลบ" :style="{ width: '380px' }"
      :draggable="false">
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 bg-red-100 rounded-full flex items-center justify-center shrink-0">
          <i class="pi pi-exclamation-triangle text-red-500"></i>
        </div>
        <p class="text-gray-700">
          ต้องการลบข้อมูล
          <strong>{{ recordToDelete?.subject }}</strong>
          หรือไม่?
        </p>
      </div>
      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="deleteConfirmVisible = false" />
        <Button label="ลบข้อมูล" icon="pi pi-trash" severity="danger" @click="deleteRecord" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
:deep(.p-inputnumber-input) {
  width: 100%;
}

:deep(.p-datatable-header-cell) {
  background-color: #f8fafc !important;
  color: #475569 !important;
  font-weight: 700 !important;
}

:deep(.p-tablist-tab-list) {
  background-color: transparent !important;
}
</style>
