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
interface SarabanStat {
  id: string
  departmentId: string | null
  bookType: string
  bookName: string
  recordMonth: string
  receiverName: string
  receivedCount: number
  internalPaperCount: number
  internalDigitalCount: number
  externalPaperCount: number
  externalDigitalCount: number
  forwardedCount: number
  recordedBy: string
  createdAt: string
}

interface Department { id: string; name: string }

// ─── State ────────────────────────────────────────────────────────────────────
const authStore = useAuthStore()
const { isSuperAdmin, isOfficer, isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('saraban')
// officer และ superadmin สามารถเพิ่ม/แก้ไข/ลบได้
const canEdit = computed(() => isSuperAdmin.value || isOfficer.value || isAdmin)
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const departments = ref<Department[]>([])
const historyRecords = ref<SarabanStat[]>([])
const isLoadingHistory = ref(true)
const isLoadingMore = ref(false)
const hasMore = ref(true)
const skip = ref(0)
const take = 20

// --- History Filters ---
const filterDeptId = ref('')
const filterBookType = ref('')
const filterYear = ref<number | null>(null)

// ─── Form (Add new) ───────────────────────────────────────────────────────────
const formData = ref({
  departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
  bookType: '',
  bookName: '',
  recordMonth: null as Date | null,
  receiverName: '',
  receivedCount: null as number | null,
  internalPaperCount: null as number | null,
  internalDigitalCount: null as number | null,
  externalPaperCount: null as number | null,
  externalDigitalCount: null as number | null,
  forwardedCount: null as number | null,
})

const isSubmitting = ref(false)
const successMessage = ref('')
const errorMessage = ref('')

const bookTypeOptions = [
  { label: 'ทะเบียนหนังสือรับ', value: 'received' },
  { label: 'ทะเบียนหนังสือส่ง', value: 'sent' },
  { label: 'ทะเบียนหนังสือเวียน', value: 'circular' },
  { label: 'ทะเบียนบันทึกข้อความ', value: 'memo' },
  { label: 'อื่นๆ', value: 'other' },
]

const getBookTypeLabel = (val: string) => bookTypeOptions.find(o => o.value === val)?.label || val

// ─── Submit ───────────────────────────────────────────────────────────────────
const submitForm = async (): Promise<void> => {
  successMessage.value = ''; errorMessage.value = ''

  if (!formData.value.recordMonth) { errorMessage.value = 'กรุณาเลือกเดือน/ปี'; return }
  if (!formData.value.bookType) { errorMessage.value = 'กรุณาเลือกประเภทเล่มทะเบียน'; return }
  if (!formData.value.bookName.trim()) { errorMessage.value = 'กรุณากรอกชื่อเล่มทะเบียน'; return }
  if (!formData.value.receiverName.trim()) { errorMessage.value = 'กรุณากรอกรายชื่อผู้รับ'; return }

  const saveDeptId = isSuperAdmin.value ? formData.value.departmentId : currentUserDepartment.value
  if (!saveDeptId) { errorMessage.value = 'กรุณาเลือกหน่วยงาน'; return }

  isSubmitting.value = true
  try {
    const payload = {
      departmentId: saveDeptId,
      bookType: formData.value.bookType,
      bookName: formData.value.bookName,
      recordMonth: new Date(Date.UTC(
        formData.value.recordMonth.getFullYear(),
        formData.value.recordMonth.getMonth(), 1
      )).toISOString(),
      receiverName: formData.value.receiverName,
      receivedCount: formData.value.receivedCount ?? 0,
      internalPaperCount: formData.value.internalPaperCount ?? 0,
      internalDigitalCount: formData.value.internalDigitalCount ?? 0,
      externalPaperCount: formData.value.externalPaperCount ?? 0,
      externalDigitalCount: formData.value.externalDigitalCount ?? 0,
      forwardedCount: formData.value.forwardedCount ?? 0,
      recordedBy: authStore.user?.uid || '',
    }

    await api.post('/SarabanStat', payload)
    successMessage.value = 'บันทึกสถิติสารบรรณสำเร็จ'
    formData.value = {
      departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
      bookType: '', bookName: '', recordMonth: null,
      receiverName: '',
      receivedCount: null, internalPaperCount: null, internalDigitalCount: null,
      externalPaperCount: null, externalDigitalCount: null, forwardedCount: null,
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
const selectedRecord = ref<SarabanStat | null>(null)
const isUpdating = ref(false)
const deleteConfirmVisible = ref(false)
const recordToDelete = ref<SarabanStat | null>(null)

const editForm = ref({
  departmentId: '',
  bookType: '', bookName: '',
  recordMonth: null as Date | null,
  receiverName: '',
  receivedCount: 0,
  internalPaperCount: 0, internalDigitalCount: 0,
  externalPaperCount: 0, externalDigitalCount: 0,
  forwardedCount: 0,
})

const openDetail = (record: SarabanStat) => { selectedRecord.value = record; detailVisible.value = true }
const openEdit = (record: SarabanStat) => {
  selectedRecord.value = record
  editForm.value = {
    departmentId: record.departmentId || '',
    bookType: record.bookType,
    bookName: record.bookName,
    recordMonth: record.recordMonth ? new Date(record.recordMonth) : null,
    receiverName: record.receiverName,
    receivedCount: record.receivedCount,
    internalPaperCount: record.internalPaperCount,
    internalDigitalCount: record.internalDigitalCount,
    externalPaperCount: record.externalPaperCount,
    externalDigitalCount: record.externalDigitalCount,
    forwardedCount: record.forwardedCount,
  }
  editVisible.value = true
}

const saveEdit = async (): Promise<void> => {
  if (!selectedRecord.value) return
  if (!editForm.value.bookName.trim()) {
    toast.error('กรุณากรอกชื่อเล่มทะเบียน'); return
  }
  isUpdating.value = true
  try {
    const payload = {
      departmentId: editForm.value.departmentId || null,
      bookType: editForm.value.bookType,
      bookName: editForm.value.bookName,
      recordMonth: editForm.value.recordMonth ? new Date(Date.UTC(
        editForm.value.recordMonth.getFullYear(),
        editForm.value.recordMonth.getMonth(), 1
      )).toISOString() : null,
      receiverName: editForm.value.receiverName,
      receivedCount: editForm.value.receivedCount,
      internalPaperCount: editForm.value.internalPaperCount,
      internalDigitalCount: editForm.value.internalDigitalCount,
      externalPaperCount: editForm.value.externalPaperCount,
      externalDigitalCount: editForm.value.externalDigitalCount,
      forwardedCount: editForm.value.forwardedCount,
      recordedBy: authStore.user?.uid || '',
    }
    await api.put(`/SarabanStat/${selectedRecord.value.id}`, payload)
    const index = historyRecords.value.findIndex(r => r.id === selectedRecord.value!.id)
    if (index !== -1) {
      historyRecords.value[index] = {
        ...historyRecords.value[index]!,
        ...payload,
        recordMonth: payload.recordMonth || '',
        departmentId: payload.departmentId || null,
      }
    }
    editVisible.value = false
    toast.success('แก้ไขข้อมูลสำเร็จ')
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่')
  } finally {
    isUpdating.value = false
  }
}

const confirmDelete = (record: SarabanStat) => {
  recordToDelete.value = record; deleteConfirmVisible.value = true
}
const deleteRecord = async (): Promise<void> => {
  if (!recordToDelete.value) return
  try {
    await api.delete(`/SarabanStat/${recordToDelete.value.id}`)
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
    if (filterBookType.value) params.bookType = filterBookType.value
    if (filterYear.value) params.year = filterYear.value

    const response = await api.get('/SarabanStat', { params })
    const newRecords: SarabanStat[] = response.data.items || []

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

watch([filterDeptId, filterBookType, filterYear], handleFilterChange)

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
const getDeptName = (id: string | null) => id ? (departments.value.find((x) => x.id === id)?.name || id) : '-'
const formatMonthYear = (dateStr: string | null | undefined) => {
  if (!dateStr) return '-'
  const utcStr = /Z|[+-]\d{2}:\d{2}$/.test(dateStr) ? dateStr : dateStr + 'Z'
  return new Date(utcStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'long' })
}
const formatDateTime = (dateStr: string | null | undefined) =>
  dateStr ? new Date(dateStr).toLocaleString('th-TH', { dateStyle: 'medium', timeStyle: 'short' }) : '-'

const yearOptions = computed(() => {
  return [
    { label: 'ทุกปี', value: null },
    ...Array.from({ length: 5 }, (_, i) => {
      const y = new Date().getFullYear() - i
      return { label: `${y + 543}`, value: y }
    })
  ]
})
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10">
    <div class="mb-6 flex flex-col sm:flex-row sm:justify-between sm:items-end gap-4">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          <i class="pi pi-folder-open text-purple-600 mr-2"></i>บันทึกสถิติงานสารบรรณ
        </h2>
        <p class="text-gray-500 mt-1">บันทึกสถิติเอกสารรับ-ลงรับ-ส่งต่อ รายเดือน</p>
      </div>
    </div>

    <Tabs value="0" lazy>
      <TabList>
        <Tab value="0"><i class="pi pi-file-edit mr-2"></i>บันทึกสถิติ</Tab>
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

                <!-- ส่วนที่ 1: ข้อมูลเล่มทะเบียน -->
                <div>
                  <p class="text-sm font-bold text-purple-700 mb-3 flex items-center gap-2">
                    <i class="pi pi-book"></i>ข้อมูลเล่มทะเบียน
                  </p>
                  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 p-4 bg-purple-50 rounded-xl border border-purple-100">
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">
                        <i class="pi pi-calendar mr-1 text-purple-500"></i>เดือน/ปี
                        <span class="text-red-500">*</span>
                      </label>
                      <DatePicker v-model="formData.recordMonth" view="month" dateFormat="mm/yy"
                        class="w-full" showIcon placeholder="เลือกเดือน/ปี" />
                    </div>
                    <div v-if="isSuperAdmin" class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">
                        <i class="pi pi-building mr-1 text-purple-500"></i>หน่วยงาน
                        <span class="text-red-500">*</span>
                      </label>
                      <Select v-model="formData.departmentId" :options="departments" optionLabel="name"
                        optionValue="id" placeholder="-- เลือกหน่วยงาน --" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">
                        <i class="pi pi-tag mr-1 text-purple-500"></i>ประเภทเล่มทะเบียน
                        <span class="text-red-500">*</span>
                      </label>
                      <Select v-model="formData.bookType" :options="bookTypeOptions" optionLabel="label"
                        optionValue="value" placeholder="-- เลือกประเภท --" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">
                        <i class="pi pi-book mr-1 text-purple-500"></i>ชื่อเล่มทะเบียน
                        <span class="text-red-500">*</span>
                      </label>
                      <InputText v-model="formData.bookName" placeholder="เช่น ทะเบียนหนังสือรับ 2568" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2 lg:col-span-4">
                      <label class="font-semibold text-sm text-gray-700">
                        <i class="pi pi-user mr-1 text-purple-500"></i>รายชื่อผู้รับ
                        <span class="text-red-500">*</span>
                      </label>
                      <InputText v-model="formData.receiverName" placeholder="ชื่อผู้รับเอกสาร" class="w-full" />
                    </div>
                  </div>
                </div>

                <!-- ส่วนที่ 2: สถิติจำนวนเอกสาร -->
                <div>
                  <p class="text-sm font-bold text-blue-700 mb-3 flex items-center gap-2">
                    <i class="pi pi-chart-bar"></i>สถิติจำนวนเอกสาร (ฉบับ)
                  </p>
                  <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 p-4 bg-blue-50 rounded-xl border border-blue-100">
                    <!-- รับเข้าทั้งหมด -->
                    <div class="flex flex-col gap-2 col-span-2 md:col-span-1 lg:col-span-1">
                      <label class="font-semibold text-sm text-gray-700 text-center">
                        <i class="pi pi-inbox text-purple-500 block text-lg mb-1"></i>
                        จำนวนเอกสารรับเข้า
                      </label>
                      <InputNumber v-model="formData.receivedCount" :min="0" class="w-full"
                        inputClass="text-center font-bold text-purple-700" placeholder="0" />
                    </div>

                    <!-- ลงรับภายใน กระดาษ -->
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700 text-center">
                        <i class="pi pi-file text-orange-400 block text-lg mb-1"></i>
                        ภายใน (กระดาษ)
                      </label>
                      <InputNumber v-model="formData.internalPaperCount" :min="0" class="w-full"
                        inputClass="text-center font-bold text-orange-600" placeholder="0" />
                    </div>

                    <!-- ลงรับภายใน ดิจิทัล -->
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700 text-center">
                        <i class="pi pi-desktop text-blue-400 block text-lg mb-1"></i>
                        ภายใน (ดิจิทัล)
                      </label>
                      <InputNumber v-model="formData.internalDigitalCount" :min="0" class="w-full"
                        inputClass="text-center font-bold text-blue-600" placeholder="0" />
                    </div>

                    <!-- ลงรับภายนอก กระดาษ -->
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700 text-center">
                        <i class="pi pi-file-export text-amber-500 block text-lg mb-1"></i>
                        ภายนอก (กระดาษ)
                      </label>
                      <InputNumber v-model="formData.externalPaperCount" :min="0" class="w-full"
                        inputClass="text-center font-bold text-amber-600" placeholder="0" />
                    </div>

                    <!-- ลงรับภายนอก ดิจิทัล -->
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700 text-center">
                        <i class="pi pi-cloud text-teal-500 block text-lg mb-1"></i>
                        ภายนอก (ดิจิทัล)
                      </label>
                      <InputNumber v-model="formData.externalDigitalCount" :min="0" class="w-full"
                        inputClass="text-center font-bold text-teal-600" placeholder="0" />
                    </div>

                    <!-- ส่งต่อ -->
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700 text-center">
                        <i class="pi pi-send text-emerald-500 block text-lg mb-1"></i>
                        จำนวนส่งต่อ
                      </label>
                      <InputNumber v-model="formData.forwardedCount" :min="0" class="w-full"
                        inputClass="text-center font-bold text-emerald-600" placeholder="0" />
                    </div>
                  </div>
                </div>

                <div class="flex justify-end border-t border-gray-100 pt-4">
                  <Button type="submit" label="บันทึกสถิติสารบรรณ" icon="pi pi-save" severity="help"
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
                <div class="flex flex-col gap-1 min-w-[180px]">
                  <label class="text-xs font-semibold text-gray-500">ประเภทเล่มทะเบียน</label>
                  <Select v-model="filterBookType" :options="[{label:'ทุกประเภท',value:''},...bookTypeOptions]"
                    optionLabel="label" optionValue="value" class="w-full" />
                </div>
                <div class="flex flex-col gap-1 min-w-[130px]">
                  <label class="text-xs font-semibold text-gray-500">ปี</label>
                  <Select v-model="filterYear" :options="yearOptions" optionLabel="label" optionValue="value" class="w-full" />
                </div>
              </div>

              <DataTable :value="historyRecords" :loading="isLoadingHistory" paginator :rows="10" stripedRows
                responsiveLayout="scroll" emptyMessage="ยังไม่มีข้อมูล">

                <Column v-if="isAdmin" header="หน่วยงาน" style="min-width: 120px">
                  <template #body="{ data }">
                    <div class="font-bold text-gray-700 text-sm">{{ getDeptName(data.departmentId) }}</div>
                    <div class="text-xs text-gray-400"><i class="pi pi-user mr-1"></i>{{ data.recordedBy }}</div>
                  </template>
                </Column>

                <Column header="เดือน/ปี" style="min-width: 110px">
                  <template #body="{ data }">
                    <div class="font-semibold text-gray-800 text-sm">{{ formatMonthYear(data.recordMonth) }}</div>
                  </template>
                </Column>

                <Column header="ประเภท / ชื่อเล่ม" style="min-width: 180px">
                  <template #body="{ data }">
                    <div class="font-semibold text-gray-800 text-sm">{{ data.bookName }}</div>
                    <div class="text-xs text-purple-500">{{ getBookTypeLabel(data.bookType) }}</div>
                  </template>
                </Column>

                <Column header="ผู้รับ" style="min-width: 130px">
                  <template #body="{ data }">
                    <span class="text-sm text-gray-700">{{ data.receiverName || '-' }}</span>
                  </template>
                </Column>

                <Column header="รับเข้า" style="width: 80px; text-align:center">
                  <template #body="{ data }">
                    <span class="font-bold text-purple-700">{{ data.receivedCount.toLocaleString() }}</span>
                  </template>
                </Column>

                <Column header="ภายใน กระดาษ" style="width: 100px; text-align:center">
                  <template #body="{ data }">
                    <span class="text-orange-600 font-semibold">{{ data.internalPaperCount.toLocaleString() }}</span>
                  </template>
                </Column>

                <Column header="ภายใน ดิจิทัล" style="width: 100px; text-align:center">
                  <template #body="{ data }">
                    <span class="text-blue-600 font-semibold">{{ data.internalDigitalCount.toLocaleString() }}</span>
                  </template>
                </Column>

                <Column header="ภายนอก กระดาษ" style="width: 110px; text-align:center">
                  <template #body="{ data }">
                    <span class="text-amber-600 font-semibold">{{ data.externalPaperCount.toLocaleString() }}</span>
                  </template>
                </Column>

                <Column header="ภายนอก ดิจิทัล" style="width: 110px; text-align:center">
                  <template #body="{ data }">
                    <span class="text-teal-600 font-semibold">{{ data.externalDigitalCount.toLocaleString() }}</span>
                  </template>
                </Column>

                <Column header="ส่งต่อ" style="width: 80px; text-align:center">
                  <template #body="{ data }">
                    <span class="text-emerald-600 font-semibold">{{ data.forwardedCount.toLocaleString() }}</span>
                  </template>
                </Column>

                <Column header="จัดการ" style="width: 110px">
                  <template #body="{ data }">
                    <div class="flex gap-1">
                      <Button icon="pi pi-eye" text rounded severity="info" size="small" v-tooltip.top="'ดูรายละเอียด'"
                        @click="openDetail(data)" />
                      <Button v-if="canEdit" icon="pi pi-pencil" text rounded severity="secondary" size="small"
                        v-tooltip.top="'แก้ไข'" @click="openEdit(data)" />
                      <Button v-if="canEdit" icon="pi pi-trash" text rounded severity="danger" size="small"
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
    <Dialog v-model:visible="detailVisible" modal header="รายละเอียดสถิติสารบรรณ" :style="{ width: '520px' }"
      :draggable="false">
      <div v-if="selectedRecord" class="flex flex-col gap-4">
        <div class="flex items-center gap-3 p-4 bg-purple-50 rounded-xl">
          <div class="w-12 h-12 bg-purple-100 rounded-xl flex items-center justify-center">
            <i class="pi pi-book text-purple-600 text-xl"></i>
          </div>
          <div>
            <p class="font-bold text-gray-800 text-lg">{{ selectedRecord.bookName }}</p>
            <p class="text-sm text-gray-500">{{ getBookTypeLabel(selectedRecord.bookType) }} — {{ formatMonthYear(selectedRecord.recordMonth) }}</p>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3 text-sm">
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">หน่วยงาน</p>
            <p class="font-semibold text-gray-800">{{ getDeptName(selectedRecord.departmentId) }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">รายชื่อผู้รับ</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.receiverName || '-' }}</p>
          </div>
        </div>

        <!-- สถิติตัวเลข -->
        <div class="grid grid-cols-3 gap-2">
          <div class="bg-purple-50 rounded-lg p-3 text-center">
            <p class="text-xs text-purple-400 mb-1">รับเข้าทั้งหมด</p>
            <p class="font-black text-2xl text-purple-700">{{ selectedRecord.receivedCount.toLocaleString() }}</p>
          </div>
          <div class="bg-orange-50 rounded-lg p-3 text-center">
            <p class="text-xs text-orange-400 mb-1">ภายใน (กระดาษ)</p>
            <p class="font-black text-2xl text-orange-600">{{ selectedRecord.internalPaperCount.toLocaleString() }}</p>
          </div>
          <div class="bg-blue-50 rounded-lg p-3 text-center">
            <p class="text-xs text-blue-400 mb-1">ภายใน (ดิจิทัล)</p>
            <p class="font-black text-2xl text-blue-600">{{ selectedRecord.internalDigitalCount.toLocaleString() }}</p>
          </div>
          <div class="bg-amber-50 rounded-lg p-3 text-center">
            <p class="text-xs text-amber-400 mb-1">ภายนอก (กระดาษ)</p>
            <p class="font-black text-2xl text-amber-600">{{ selectedRecord.externalPaperCount.toLocaleString() }}</p>
          </div>
          <div class="bg-teal-50 rounded-lg p-3 text-center">
            <p class="text-xs text-teal-400 mb-1">ภายนอก (ดิจิทัล)</p>
            <p class="font-black text-2xl text-teal-600">{{ selectedRecord.externalDigitalCount.toLocaleString() }}</p>
          </div>
          <div class="bg-emerald-50 rounded-lg p-3 text-center">
            <p class="text-xs text-emerald-400 mb-1">ส่งต่อ</p>
            <p class="font-black text-2xl text-emerald-600">{{ selectedRecord.forwardedCount.toLocaleString() }}</p>
          </div>
        </div>

        <div class="border-t pt-3 text-xs text-gray-400 flex justify-between">
          <span><i class="pi pi-user mr-1"></i>{{ selectedRecord.recordedBy }}</span>
          <span>{{ formatDateTime(selectedRecord.createdAt) }}</span>
        </div>
      </div>
      <template #footer>
        <Button label="ปิด" severity="secondary" text @click="detailVisible = false" />
        <Button v-if="canEdit && selectedRecord" label="แก้ไข" icon="pi pi-pencil" severity="help"
          @click="detailVisible = false; openEdit(selectedRecord!)" />
      </template>
    </Dialog>

    <!-- Edit Dialog -->
    <Dialog v-model:visible="editVisible" modal header="แก้ไขสถิติสารบรรณ" :style="{ width: '600px' }"
      :draggable="false">
      <div class="flex flex-col gap-4 mt-2">
        <div class="grid grid-cols-2 gap-4">
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">เดือน/ปี <span class="text-red-500">*</span></label>
            <DatePicker v-model="editForm.recordMonth" view="month" dateFormat="mm/yy" class="w-full" showIcon />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ประเภทเล่มทะเบียน</label>
            <Select v-model="editForm.bookType" :options="bookTypeOptions" optionLabel="label" optionValue="value" class="w-full" />
          </div>
          <div class="flex flex-col gap-2 col-span-2">
            <label class="text-sm font-semibold text-gray-700">ชื่อเล่มทะเบียน <span class="text-red-500">*</span></label>
            <InputText v-model="editForm.bookName" class="w-full" />
          </div>
          <div class="flex flex-col gap-2 col-span-2">
            <label class="text-sm font-semibold text-gray-700">รายชื่อผู้รับ</label>
            <InputText v-model="editForm.receiverName" class="w-full" />
          </div>
        </div>

        <p class="text-xs font-bold text-blue-700 -mb-2">สถิติจำนวนเอกสาร (ฉบับ)</p>
        <div class="grid grid-cols-3 gap-3">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-gray-500">รับเข้าทั้งหมด</label>
            <InputNumber v-model="editForm.receivedCount" :min="0" class="w-full" inputClass="text-center font-bold text-purple-700" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-gray-500">ภายใน (กระดาษ)</label>
            <InputNumber v-model="editForm.internalPaperCount" :min="0" class="w-full" inputClass="text-center font-bold text-orange-600" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-gray-500">ภายใน (ดิจิทัล)</label>
            <InputNumber v-model="editForm.internalDigitalCount" :min="0" class="w-full" inputClass="text-center font-bold text-blue-600" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-gray-500">ภายนอก (กระดาษ)</label>
            <InputNumber v-model="editForm.externalPaperCount" :min="0" class="w-full" inputClass="text-center font-bold text-amber-600" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-gray-500">ภายนอก (ดิจิทัล)</label>
            <InputNumber v-model="editForm.externalDigitalCount" :min="0" class="w-full" inputClass="text-center font-bold text-teal-600" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-semibold text-gray-500">ส่งต่อ</label>
            <InputNumber v-model="editForm.forwardedCount" :min="0" class="w-full" inputClass="text-center font-bold text-emerald-600" />
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
          ต้องการลบสถิติ
          <strong>{{ recordToDelete?.bookName }}</strong>
          ({{ formatMonthYear(recordToDelete?.recordMonth) }}) หรือไม่?
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
