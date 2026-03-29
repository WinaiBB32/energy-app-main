<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'

import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'
import api from '@/services/api'

import Card from 'primevue/card'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Dialog from 'primevue/dialog'
import Tag from 'primevue/tag'
import Checkbox from 'primevue/checkbox'
import ToggleSwitch from 'primevue/toggleswitch'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'

// ─── 1. Interfaces ────────────────────────────────────────────────────────────
export interface IPPhoneDirectory {
  ownerName: string
  location: string
  ipPhoneNumber: string
  analogNumber: string
  deviceCode: string
  departmentId: string
  workgroup: string
  description: string
  keywords: string
  /** ข้อมูลเก่าอาจไม่มีฟิลด์ — ถือเป็นเผยแพร่ได้ (ลงตัวกับพฤติกรรม import เดิม) */
  isPublished?: boolean
}

export interface FetchedIPPhoneDirectory extends IPPhoneDirectory {
  id: string
  createdAt?: string
}

export interface Department {
  id: string
  name: string
}

interface DirImportRow {
  ownerName: string
  ipPhoneNumber: string
  departmentId: string
  workgroup: string
  location: string
  analogNumber: string
  deviceCode: string
  keywords: string
  description: string
  isPublished: boolean
}

// ─── 2. State & Setup ─────────────────────────────────────────────────────────
const authStore = useAuthStore()
const { isSuperAdmin, isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('ipphone')
const currentUserName = computed(
  () => authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ',
)
const toast = useAppToast()

/** รายการที่ไม่มี isPublished ถือว่าเผยแพร่ได้ */
function isDirPublished(item: IPPhoneDirectory): boolean {
  return item.isPublished !== false
}

const departments = ref<Department[]>([])
const directories = ref<FetchedIPPhoneDirectory[]>([])

const isLoading = ref<boolean>(true)

// ─── 3. Fetch Data ────────────────────────────────────────────────────────────
const fetchDepartments = async (): Promise<void> => {
  try {
    const res = await api.get('/Department')
    departments.value = res.data
  } catch (e) {
    toast.fromError(e, 'ไม่สามารถโหลดรายชื่อหน่วยงานได้')
  }
}

const fetchDirectories = async (): Promise<void> => {
  isLoading.value = true
  try {
    const params: Record<string, string> = {}
    if (searchQuery.value) params.keyword = searchQuery.value
    const res = await api.get('/IPPhoneDirectory', { params })
    directories.value = res.data.items || []
  } catch (e) {
    toast.fromError(e, 'ไม่สามารถโหลดสมุดโทรศัพท์ได้')
  } finally {
    isLoading.value = false
  }
}

onMounted(async () => {
  await fetchDepartments()
  await fetchDirectories()
})

// ─── 4. Directory CRUD Operations ─────────────────────────────────────────────
const dialogVisible = ref<boolean>(false)
const isSaving = ref<boolean>(false)
const isEditMode = ref<boolean>(false)
const successMsg = ref<string>('')
const errorMsg = ref<string>('')
const searchQuery = ref<string>('')
const editId = ref<string>('')
const filterPublished = ref<boolean | null>(null) // null=ทั้งหมด, true=เผยแพร่, false=ห้ามเผยแพร่

const formDir = ref<IPPhoneDirectory>({
  ownerName: '',
  location: '',
  ipPhoneNumber: '',
  analogNumber: '',
  deviceCode: '',
  departmentId: '',
  workgroup: '',
  description: '',
  keywords: '',
  isPublished: true,
})

const openNewDialog = (): void => {
  successMsg.value = ''
  errorMsg.value = ''
  isEditMode.value = false
  editId.value = ''
  formDir.value = {
    ownerName: '',
    location: '',
    ipPhoneNumber: '',
    analogNumber: '',
    deviceCode: '',
    departmentId: '',
    workgroup: '',
    description: '',
    keywords: '',
    isPublished: true,
  }
  dialogVisible.value = true
}

const openEditDialog = (data: FetchedIPPhoneDirectory): void => {
  successMsg.value = ''
  errorMsg.value = ''
  isEditMode.value = true
  editId.value = data.id
  formDir.value = {
    ownerName: data.ownerName,
    location: data.location,
    ipPhoneNumber: data.ipPhoneNumber,
    analogNumber: data.analogNumber,
    deviceCode: data.deviceCode,
    departmentId: data.departmentId,
    workgroup: data.workgroup,
    description: data.description,
    keywords: data.keywords,
    isPublished: data.isPublished ?? true,
  }
  dialogVisible.value = true
}

const saveDirectory = async (): Promise<void> => {
  if (!formDir.value.ownerName || !formDir.value.ipPhoneNumber || !formDir.value.departmentId) {
    errorMsg.value = 'กรุณากรอกข้อมูลบังคับ (*) ให้ครบถ้วน'
    return
  }

  isSaving.value = true
  try {
    if (isEditMode.value && editId.value) {
      await api.put(`/IPPhoneDirectory/${editId.value}`, { ...formDir.value })
      successMsg.value = 'อัปเดตข้อมูลสำเร็จ'
    } else {
      await api.post('/IPPhoneDirectory', { ...formDir.value })
      successMsg.value = 'เพิ่มหมายเลขใหม่สำเร็จ'
    }
    await fetchDirectories()
    setTimeout(() => {
      dialogVisible.value = false
    }, 1000)
  } catch (e: unknown) {
    toast.fromError(e, 'ไม่สามารถบันทึกข้อมูลได้')
    errorMsg.value = e instanceof Error ? e.message : 'เกิดข้อผิดพลาด'
  } finally {
    isSaving.value = false
  }
}

const deleteDirectory = async (id: string, name: string): Promise<void> => {
  if (confirm(`คุณแน่ใจหรือไม่ว่าต้องการลบเบอร์ของ "${name}" ?`)) {
    try {
      await api.delete(`/IPPhoneDirectory/${id}`)
      await fetchDirectories()
    } catch (e) {
      toast.fromError(e, 'ไม่สามารถลบข้อมูลได้')
    }
  }
}

const computedDirectories = computed<FetchedIPPhoneDirectory[]>(() => directories.value)

const togglePublished = async (item: FetchedIPPhoneDirectory): Promise<void> => {
  try {
    await api.put(`/IPPhoneDirectory/${item.id}`, { ...item, isPublished: !isDirPublished(item) })
    await fetchDirectories()
  } catch (e) {
    toast.fromError(e, 'ไม่สามารถเปลี่ยนสถานะได้')
  }
}

const filteredDirectories = computed<FetchedIPPhoneDirectory[]>(() => {
  let list = computedDirectories.value
  if (filterPublished.value !== null) {
    list = list.filter((item) => isDirPublished(item) === filterPublished.value)
  }
  if (!searchQuery.value) return list
  const q = searchQuery.value.toLowerCase()
  return list.filter(
    (item) =>
      (item.ownerName || '').toLowerCase().includes(q) ||
      (item.ipPhoneNumber || '').toLowerCase().includes(q) ||
      (item.keywords || '').toLowerCase().includes(q) ||
      (item.workgroup || '').toLowerCase().includes(q) ||
      (item.location || '').toLowerCase().includes(q) ||
      (item.description || '').toLowerCase().includes(q) ||
      getDeptName(item.departmentId).toLowerCase().includes(q),
  )
})

const publishedCount = computed(() => computedDirectories.value.filter((d) => isDirPublished(d)).length)
const hiddenCount = computed(() => computedDirectories.value.filter((d) => !isDirPublished(d)).length)

// ─── Custom CSV Row Parser (Safe for TS Strict Mode) ──────────────────────────
const parseCSVRow = (str: string): string[] => {
  const arr: string[] = []
  let quote = false
  let col = ''
  for (let i = 0; i < str.length; i++) {
    const cc = str[i] as string
    const nc = i + 1 < str.length ? (str[i + 1] as string) : ''
    if (cc === '"' && quote && nc === '"') {
      col += '"'
      i++
      continue
    }
    if (cc === '"') {
      quote = !quote
      continue
    }
    if (cc === ',' && !quote) {
      arr.push(col.trim())
      col = ''
      continue
    }
    col += cc
  }
  arr.push(col.trim())
  return arr
}

// ─── 4.1. Import Directory CSV Logic ──────────────────────────────────────────
const importDirDialogVisible = ref<boolean>(false)
const importDirFile = ref<File | null>(null)
const isImportingDir = ref<boolean>(false)
const importDirSuccess = ref<string>('')
const importDirError = ref<string>('')
const importDirPreviewRows = ref<DirImportRow[]>([])

const handleImportDirFileSelect = (event: Event): void => {
  const target = event.target as HTMLInputElement
  importDirError.value = ''
  importDirSuccess.value = ''
  importDirPreviewRows.value = []
  if (!target.files || target.files.length === 0) {
    importDirFile.value = null
    return
  }
  importDirFile.value = target.files[0] || null
  if (!importDirFile.value) return

  const reader = new FileReader()
  reader.onload = (e: ProgressEvent<FileReader>) => {
    try {
      const text = e.target?.result
      if (typeof text !== 'string') throw new Error('ไม่สามารถอ่านไฟล์ได้')
      const rows = text
        .split('\n')
        .map((r) => r.trim())
        .filter((r) => r.length > 0)
      if (rows.length < 2) throw new Error('ไฟล์ไม่มีข้อมูล')
      const parsed: DirImportRow[] = []
      for (let i = 1; i < rows.length; i++) {
        const row = rows[i]
        if (!row) continue
        const cols = parseCSVRow(row)
        if (cols.length < 2) continue
        const ownerName = cols[0] || ''
        const ipPhoneNumber = cols[1] || ''
        if (!ownerName || !ipPhoneNumber) continue
        const rawDept = cols[2] || ''
        const matchedDept = departments.value.find((d) => d.name === rawDept)
        const departmentId = matchedDept ? matchedDept.id : rawDept
        parsed.push({
          ownerName,
          ipPhoneNumber,
          departmentId,
          workgroup: cols[3] || '',
          location: cols[4] || '',
          analogNumber: cols[5] || '',
          deviceCode: cols[6] || '',
          keywords: cols[7] || '',
          description: cols[8] || '',
          isPublished: true,
        })
      }
      if (parsed.length === 0) throw new Error('ไม่พบข้อมูลที่สามารถนำเข้าได้ในไฟล์')
      importDirPreviewRows.value = parsed
    } catch (err: unknown) {
      importDirError.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการอ่านไฟล์'
    }
  }
  reader.readAsText(importDirFile.value)
}

const setAllDirPublished = (value: boolean): void => {
  importDirPreviewRows.value = importDirPreviewRows.value.map((r) => ({ ...r, isPublished: value }))
}

const handleImportDirectory = async (): Promise<void> => {
  importDirSuccess.value = ''
  importDirError.value = ''
  if (importDirPreviewRows.value.length === 0) {
    importDirError.value = 'กรุณาเลือกไฟล์ CSV และตรวจสอบข้อมูลก่อนนำเข้า'
    return
  }

  isImportingDir.value = true
  try {
    for (const row of importDirPreviewRows.value) {
      await api.post('/IPPhoneDirectory', { ...row })
    }
    importDirSuccess.value = `นำเข้าสมุดโทรศัพท์สำเร็จจำนวน ${importDirPreviewRows.value.length} รายการ`
    importDirFile.value = null
    importDirPreviewRows.value = []
    const fileInput = document.getElementById('importDirFile') as HTMLInputElement
    if (fileInput) fileInput.value = ''
    await fetchDirectories()
    setTimeout(() => {
      importDirDialogVisible.value = false
    }, 2000)
  } catch (err: unknown) {
    importDirError.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการบันทึกข้อมูล'
  } finally {
    isImportingDir.value = false
  }
}

// ─── 5. Excel (CSV) Upload Logic (Monthly Stats) ──────────────────────────────
const uploadMonth = ref<Date | null>(null)
const selectedFile = ref<File | null>(null)
const isUploading = ref<boolean>(false)
const uploadSuccess = ref<string>('')
const uploadError = ref<string>('')

const handleFileSelect = (event: Event): void => {
  const target = event.target as HTMLInputElement
  if (target.files && target.files.length > 0) {
    selectedFile.value = target.files[0] || null
  }
}

// ─── ฟังก์ชันสำหรับดาวน์โหลดไฟล์ Template CSV ───
const downloadCSVTemplate = (): void => {
  const csvHeaders =
    'Extension,Answered(Inbound),No Answered(Inbound),Busy(Inbound),Failed(Inbound),Voicemail(Inbound),Total(Inbound),Answered(Outbound),No Answered(Outbound),Busy(Outbound),Failed(Outbound),Voicemail(Outbound),Total(Outbound),Total,Total Talk Duration\n'
  const csvSampleData =
    '70101-70101,1,1,0,0,0,2,0,1,0,0,0,1,3,00:01:37\n70102-70102,32,2,0,0,0,34,2,0,0,0,0,2,36,01:07:05\n'

  const blob = new Blob(['\uFEFF' + csvHeaders + csvSampleData], {
    type: 'text/csv;charset=utf-8;',
  })
  const url = URL.createObjectURL(blob)

  const link = document.createElement('a')
  link.href = url
  link.setAttribute('download', 'Template_IPPhone_Stat.csv')
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
}

// ─── ฟังก์ชันสำหรับดาวน์โหลดไฟล์ Template สมุดโทรศัพท์ ───
const downloadDirCSVTemplate = (): void => {
  const csvHeaders =
    'ชื่อผู้ครอบครอง,เบอร์ IP-Phone,หน่วยงาน,กลุ่มงาน,สถานที่,เบอร์ Analog,รหัสเครื่อง,Keywords,รายละเอียด\n'
  const csvSampleData =
    'สมชาย ใจดี,70101,กองช่าง,IT Support,อาคาร A ชั้น 2,02-123-4567,MAC-1234,ด่วน/ซ่อม,ติดต่อเรื่องอินเทอร์เน็ต\nสมหญิง รักดี,70102,สำนักปลัด,การเงิน,อาคาร B ชั้น 1,-,-,บัญชี/เบิกจ่าย,-\n'

  const blob = new Blob(['\uFEFF' + csvHeaders + csvSampleData], {
    type: 'text/csv;charset=utf-8;',
  })
  const url = URL.createObjectURL(blob)

  const link = document.createElement('a')
  link.href = url
  link.setAttribute('download', 'Template_IPPhone_Directory.csv')
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
}

const handleUploadExcel = async (): Promise<void> => {
  uploadSuccess.value = ''
  uploadError.value = ''

  if (!uploadMonth.value) {
    uploadError.value = 'กรุณาเลือกรอบเดือนของสถิติ'
    return
  }
  if (!selectedFile.value) {
    uploadError.value = 'กรุณาเลือกไฟล์ CSV ที่ต้องการอัปโหลด'
    return
  }

  isUploading.value = true
  const reader = new FileReader()

  reader.onload = async (e: ProgressEvent<FileReader>) => {
    try {
      const text = e.target?.result
      if (typeof text !== 'string') throw new Error('ไม่สามารถอ่านไฟล์ได้')

      const rows = text
        .split('\n')
        .map((row) => row.trim())
        .filter((row) => row.length > 0)
      if (rows.length < 2) throw new Error('ไฟล์ว่างเปล่า หรือไม่มีข้อมูลสถิติ')

      const parsedData = []

      for (let i = 1; i < rows.length; i++) {
        const row = rows[i]
        if (!row) continue
        const cols = parseCSVRow(row)
        if (cols.length < 15) continue

        const rawExt = cols[0] || ''
        const ext = rawExt.includes('-') ? (rawExt.split('-')[0] || '').trim() : rawExt.trim()

        parsedData.push({
          reportMonth: uploadMonth.value,
          extension: ext,
          answeredInbound: Number(cols[1] || 0),
          noAnswerInbound: Number(cols[2] || 0),
          busyInbound: Number(cols[3] || 0),
          failedInbound: Number(cols[4] || 0),
          voicemailInbound: Number(cols[5] || 0),
          totalInbound: Number(cols[6] || 0),
          answeredOutbound: Number(cols[7] || 0),
          noAnswerOutbound: Number(cols[8] || 0),
          busyOutbound: Number(cols[9] || 0),
          failedOutbound: Number(cols[10] || 0),
          voicemailOutbound: Number(cols[11] || 0),
          totalOutbound: Number(cols[12] || 0),
          totalCalls: Number(cols[6] || 0) + Number(cols[12] || 0),
          totalTalkDuration: cols[14] || '00:00:00',
        })
      }

      if (parsedData.length === 0) {
        throw new Error('ไม่พบข้อมูลรูปแบบที่ถูกต้องในไฟล์ (กรุณาเช็คโครงสร้าง CSV)')
      }

      uploadSuccess.value = `นำเข้าข้อมูลสำเร็จ! บันทึกสถิติทั้งหมด ${parsedData.length} หมายเลขเข้าสู่ระบบแล้ว`
      uploadMonth.value = null
      selectedFile.value = null

      const fileInput = document.getElementById('excelFile') as HTMLInputElement
      if (fileInput) fileInput.value = ''
    } catch (err: unknown) {
      uploadError.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการอ่านไฟล์'
    } finally {
      isUploading.value = false
    }
  }

  reader.readAsText(selectedFile.value)
}

// ─── Helpers ──────────────────────────────────────────────────────────────────
const getDeptName = (id: string): string => departments.value.find((x) => x.id === id)?.name || id
const formatThaiMonth = (dateStr: string | null | undefined): string => {
  if (!dateStr) return '-'
  const d = new Date(dateStr)
  return d.toLocaleDateString('th-TH', { year: 'numeric', month: 'long' })
}
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10">
    <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
      <div>
        <h2 class="text-3xl font-bold text-gray-800">
          <i class="pi pi-desktop text-teal-600 mr-2"></i>ระบบจัดการ IP-Phone
        </h2>
        <p class="text-gray-500 mt-1">
          สมุดโทรศัพท์องค์กร (Directory) และนำเข้าสถิติการโทร (.csv) ประจำเดือน
        </p>
      </div>
    </div>

    <Tabs value="0" lazy>
      <TabList>
        <Tab value="0"><i class="pi pi-address-book mr-2"></i>สมุดโทรศัพท์ (Directory)</Tab>
        <Tab value="1" v-if="isAdmin"><i class="pi pi-cloud-upload mr-2"></i>นำเข้าสถิติ (CSV)</Tab>
      </TabList>

      <TabPanels>
        <TabPanel value="0">
          <Card class="shadow-sm border-none mt-2">
            <template #content>
              <div class="flex flex-col md:flex-row justify-between items-center gap-4 mb-4">
                <IconField class="w-full md:w-96">
                  <InputIcon class="pi pi-search" />
                  <InputText v-model="searchQuery" placeholder="ค้นหาชื่อ, เบอร์, หน่วยงาน, กลุ่มงาน, Keyword..."
                    class="w-full" @keyup.enter="fetchDirectories" />
                </IconField>

                <div class="flex flex-wrap gap-2 w-full md:w-auto items-center">
                  <!-- Published filter buttons -->
                  <div class="flex gap-1 border border-gray-200 rounded-lg p-0.5 bg-gray-50">
                    <Button label="ทั้งหมด" size="small" :severity="filterPublished === null ? 'info' : 'secondary'"
                      :text="filterPublished !== null" @click="filterPublished = null" />
                    <Button size="small" :severity="filterPublished === true ? 'success' : 'secondary'"
                      :text="filterPublished !== true" @click="filterPublished = true">
                      <i class="pi pi-eye mr-1 text-xs"></i>เผยแพร่ได้
                      <span class="ml-1 text-xs opacity-70">({{ publishedCount }})</span>
                    </Button>
                    <Button size="small" :severity="filterPublished === false ? 'danger' : 'secondary'"
                      :outlined="filterPublished === false" :text="filterPublished !== false"
                      @click="filterPublished = false">
                      <i class="pi pi-eye-slash mr-1 text-xs"></i>ห้ามเผยแพร่
                      <span class="ml-1 text-xs opacity-70">({{ hiddenCount }})</span>
                    </Button>
                  </div>
                  <template v-if="isAdmin">
                    <Button label="นำเข้า CSV" icon="pi pi-upload" severity="secondary" outlined
                      @click="importDirDialogVisible = true" />
                    <Button label="เพิ่มหมายเลขใหม่" icon="pi pi-plus" severity="help" @click="openNewDialog" />
                  </template>
                </div>
              </div>

              <DataTable :value="filteredDirectories" :loading="isLoading" paginator :rows="10" stripedRows
                responsiveLayout="scroll" emptyMessage="ไม่พบข้อมูลหมายเลขโทรศัพท์">
                <Column header="ผู้ครอบครอง / สถานที่">
                  <template #body="sp">
                    <div class="font-bold text-gray-800">{{ sp.data.ownerName }}</div>
                    <div class="text-xs text-gray-500">
                      <i class="pi pi-map-marker mr-1"></i>{{ sp.data.location || '-' }}
                    </div>
                  </template>
                </Column>

                <Column header="เบอร์ติดต่อ">
                  <template #body="sp">
                    <div class="font-bold text-teal-600 text-lg tracking-wider">
                      {{ sp.data.ipPhoneNumber }}
                    </div>
                    <div v-if="sp.data.analogNumber" class="text-xs text-gray-500 mt-1">
                      Analog: {{ sp.data.analogNumber }}
                    </div>
                  </template>
                </Column>

                <Column header="หน่วยงาน / กลุ่มงาน">
                  <template #body="sp">
                    <div class="text-sm font-semibold text-gray-700">
                      {{ getDeptName(sp.data.departmentId) }}
                    </div>
                    <div class="text-xs text-gray-500">{{ sp.data.workgroup || '-' }}</div>
                  </template>
                </Column>

                <Column header="Keywords (บทบาท)">
                  <template #body="sp">
                    <div class="flex flex-wrap gap-1">
                      <Tag v-if="sp.data.keywords" :value="sp.data.keywords" severity="secondary" rounded
                        class="text-[10px]" />
                      <span v-else class="text-xs text-gray-400">-</span>
                    </div>
                  </template>
                </Column>

                <Column header="สถานะ" alignFrozen="right" style="width: 130px">
                  <template #body="sp">
                    <!-- admin/superadmin: toggle switch -->
                    <div v-if="isAdmin" class="flex items-center gap-2">
                      <ToggleSwitch
                        :modelValue="isDirPublished(sp.data)"
                        v-tooltip.top="isDirPublished(sp.data) ? 'คลิกเพื่อตั้งเป็นห้ามเผยแพร่' : 'คลิกเพื่อเผยแพร่'"
                        @update:modelValue="togglePublished(sp.data)"
                      />
                      <span class="text-xs" :class="isDirPublished(sp.data) ? 'text-green-600' : 'text-red-500'">
                        {{ isDirPublished(sp.data) ? 'เผยแพร่ได้' : 'ห้ามเผยแพร่' }}
                      </span>
                    </div>
                    <!-- user: badge only -->
                    <div v-else>
                      <Tag
                        :value="isDirPublished(sp.data) ? 'เผยแพร่ได้' : 'ห้ามเผยแพร่'"
                        :severity="isDirPublished(sp.data) ? 'success' : 'danger'"
                        :icon="isDirPublished(sp.data) ? 'pi pi-check-circle' : 'pi pi-lock'"
                        rounded
                      />
                    </div>
                  </template>
                </Column>

                <Column header="จัดการ" alignFrozen="right" style="width: 10rem">
                  <template #body="sp">
                    <div class="flex gap-1">
                      <Button icon="pi pi-eye" severity="info" text rounded v-tooltip.top="'ดูประวัติ/สนทนา'"
                        @click="$router.push(`/ipphone/directory/${sp.data.id}`)" />

                      <template v-if="isAdmin">
                        <Button icon="pi pi-pencil" severity="secondary" text rounded v-tooltip.top="'แก้ไข'"
                          @click="openEditDialog(sp.data)" />
                        <Button icon="pi pi-trash" severity="danger" text rounded v-tooltip.top="'ลบ'"
                          @click="deleteDirectory(sp.data.id, sp.data.ownerName)" />
                      </template>
                    </div>
                  </template>
                </Column>
              </DataTable>
            </template>
          </Card>
        </TabPanel>

        <TabPanel value="1" v-if="isAdmin">
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mt-2">
            <Card class="shadow-sm border-none">
              <template #title>
                <div class="text-lg font-bold text-gray-800">
                  <i class="pi pi-file-excel text-teal-600 mr-2"></i>นำเข้าสถิติจากระบบ IP-Phone
                </div>
              </template>
              <template #content>
                <Message v-if="uploadSuccess" severity="success" :closable="true">{{
                  uploadSuccess
                  }}</Message>
                <Message v-if="uploadError" severity="error" :closable="true">{{
                  uploadError
                  }}</Message>

                <div class="flex flex-col gap-5 mt-2">
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">สถิติประจำเดือน <span
                        class="text-red-500">*</span></label>
                    <DatePicker v-model="uploadMonth" view="month" dateFormat="MM yy" placeholder="-- เลือกรอบเดือน --"
                      class="w-full" showIcon />
                  </div>

                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">ไฟล์สถิติ (.csv) <span
                        class="text-red-500">*</span></label>
                    <div
                      class="border-2 border-dashed border-gray-300 rounded-xl p-6 text-center hover:bg-gray-50 transition-colors">
                      <i class="pi pi-cloud-upload text-4xl text-gray-400 mb-3"></i>
                      <input type="file" id="excelFile" accept=".csv" @change="handleFileSelect"
                        class="block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-teal-50 file:text-teal-700 hover:file:bg-teal-100 cursor-pointer" />
                    </div>
                    <p class="text-xs text-gray-400 mt-1">
                      <i class="pi pi-info-circle"></i> รองรับเฉพาะไฟล์นามสกุล .csv ที่โหลดมาจากระบบ
                      IP-Phone ส่วนกลาง
                    </p>

                    <div class="mt-2">
                      <Button label="ดาวน์โหลดไฟล์ตัวอย่าง (CSV Template)" icon="pi pi-download" severity="secondary"
                        outlined size="small" @click="downloadCSVTemplate" />
                    </div>
                  </div>

                  <Button label="เริ่มอ่านข้อมูลและบันทึก" icon="pi pi-cog" severity="success"
                    class="w-full mt-2 py-3 font-bold" :loading="isUploading" :disabled="!selectedFile || !uploadMonth"
                    @click="handleUploadExcel" />
                </div>
              </template>
            </Card>
          </div>
        </TabPanel>
      </TabPanels>
    </Tabs>

    <Dialog v-model:visible="importDirDialogVisible" modal header="นำเข้าสมุดโทรศัพท์ (CSV)"
      :style="{ width: 'min(95vw, 1100px)' }" :draggable="false">
      <Message v-if="importDirSuccess" severity="success" :closable="false" class="mb-3">{{
        importDirSuccess
        }}</Message>
      <Message v-if="importDirError" severity="error" :closable="false" class="mb-3">{{
        importDirError
        }}</Message>

      <div class="flex flex-col gap-3">
        <p class="text-sm text-gray-600">
          กรุณาเตรียมไฟล์ CSV โดยให้ข้อมูลอยู่ในคอลัมน์ตามลำดับดังนี้:
        </p>
        <div class="bg-gray-100 p-3 rounded text-xs font-mono text-gray-600 overflow-x-auto whitespace-nowrap">
          ชื่อผู้ครอบครอง, เบอร์ IP-Phone, หน่วยงาน, กลุ่มงาน, สถานที่, เบอร์ Analog, รหัสเครื่อง,
          Keywords, รายละเอียด
        </div>
        <p class="text-xs text-red-500">
          * คอลัมน์ที่ 1 (ชื่อ) และ คอลัมน์ที่ 2 (เบอร์) ห้ามเว้นว่าง
        </p>
        <div class="flex items-center gap-3">
          <Button label="ดาวน์โหลดไฟล์ตัวอย่าง (CSV Template)" icon="pi pi-download" severity="secondary" outlined
            size="small" @click="downloadDirCSVTemplate" />
        </div>

        <div
          class="border-2 border-dashed border-gray-300 rounded-xl p-5 text-center hover:bg-gray-50 transition-colors">
          <i class="pi pi-file-excel text-3xl text-gray-400 mb-2"></i>
          <input type="file" id="importDirFile" accept=".csv" @change="handleImportDirFileSelect"
            class="block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-purple-50 file:text-purple-700 hover:file:bg-purple-100 cursor-pointer" />
        </div>

        <!-- Preview Table -->
        <div v-if="importDirPreviewRows.length > 0">
          <div class="flex items-center justify-between mb-2 flex-wrap gap-2">
            <div class="font-semibold text-gray-700">
              <i class="pi pi-table mr-1 text-purple-600"></i>
              ตรวจสอบข้อมูลก่อนนำเข้า
              <Tag :value="`${importDirPreviewRows.length} รายการ`" severity="info" rounded class="ml-2 text-xs" />
            </div>
            <div class="flex gap-2">
              <Button label="เผยแพร่ทั้งหมด" icon="pi pi-eye" severity="success" size="small" outlined
                @click="setAllDirPublished(true)" />
              <Button label="ซ่อนทั้งหมด" icon="pi pi-eye-slash" severity="secondary" size="small" outlined
                @click="setAllDirPublished(false)" />
            </div>
          </div>
          <DataTable :value="importDirPreviewRows" :rows="10" paginator stripedRows
            class="text-sm" scrollable scrollHeight="340px"
            emptyMessage="ไม่มีข้อมูล">
            <Column header="ชื่อผู้ครอบครอง" style="min-width: 140px">
              <template #body="sp">
                <span class="font-semibold text-gray-800">{{ sp.data.ownerName }}</span>
              </template>
            </Column>
            <Column header="เบอร์ IP-Phone" style="min-width: 110px">
              <template #body="sp">
                <span class="font-bold text-teal-600 tracking-wider">{{ sp.data.ipPhoneNumber }}</span>
              </template>
            </Column>
            <Column header="หน่วยงาน" style="min-width: 130px">
              <template #body="sp">
                {{ getDeptName(sp.data.departmentId) }}
              </template>
            </Column>
            <Column field="workgroup" header="กลุ่มงาน" style="min-width: 110px" />
            <Column field="location" header="สถานที่" style="min-width: 120px" />
            <Column header="เผยแพร่" style="width: 90px; text-align: center">
              <template #body="sp">
                <div class="flex justify-center">
                  <Checkbox v-model="sp.data.isPublished" :binary="true" />
                </div>
              </template>
            </Column>
          </DataTable>
          <p class="text-xs text-gray-400 mt-1">
            <i class="pi pi-info-circle mr-1"></i>
            ติ๊กช่อง "เผยแพร่" เพื่อให้ผู้ใช้ทั่วไปมองเห็นหมายเลขนั้น
          </p>
        </div>
      </div>

      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="importDirDialogVisible = false" />
        <Button label="นำเข้าข้อมูล" icon="pi pi-upload" severity="help" :loading="isImportingDir"
          :disabled="importDirPreviewRows.length === 0" @click="handleImportDirectory" />
      </template>
    </Dialog>

    <Dialog v-model:visible="dialogVisible" modal :header="isEditMode ? 'แก้ไขข้อมูลหมายเลข' : 'เพิ่มหมายเลข IP-Phone'"
      :style="{ width: '680px' }" :draggable="false">
      <Message v-if="successMsg" severity="success" :closable="false" class="mb-3">{{
        successMsg
        }}</Message>
      <Message v-if="errorMsg" severity="error" :closable="false" class="mb-3">{{
        errorMsg
        }}</Message>

      <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-2">
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">ชื่อผู้ครอบครอง / ประจำจุด <span
              class="text-red-500">*</span></label>
          <InputText v-model="formDir.ownerName" placeholder="ระบุชื่อเจ้าหน้าที่หรือตำแหน่ง" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">เบอร์โทร IP-Phone <span
              class="text-red-500">*</span></label>
          <InputText v-model="formDir.ipPhoneNumber" placeholder="เช่น 70101" class="w-full font-bold text-teal-600" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">หน่วยงาน <span class="text-red-500">*</span></label>
          <Select v-model="formDir.departmentId" :options="departments" optionLabel="name" optionValue="id"
            placeholder="เลือกหน่วยงาน" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">กลุ่มงาน / แผนก</label>
          <InputText v-model="formDir.workgroup" placeholder="เช่น IT Support" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">สถานที่ติดตั้ง</label>
          <InputText v-model="formDir.location" placeholder="เช่น อาคาร A ชั้น 2" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">เบอร์โทร Analog (ถ้ามี)</label>
          <InputText v-model="formDir.analogNumber" placeholder="เช่น 02-123-4567 ต่อ 11" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">รหัสประจำเครื่อง (MAC/Serial)</label>
          <InputText v-model="formDir.deviceCode" placeholder="ระบุรหัสหลังเครื่อง" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">Keywords สำหรับค้นหา</label>
          <InputText v-model="formDir.keywords" placeholder="เช่น แจ้งซ่อม, ด่วน, รปภ" class="w-full" />
        </div>

        <div class="flex flex-col gap-2 md:col-span-2">
          <label class="text-sm font-semibold text-gray-700">สถานะการเผยแพร่</label>
          <div class="flex items-center gap-3 p-3 bg-gray-50 rounded-lg border border-gray-200">
            <ToggleSwitch v-model="formDir.isPublished" />
            <span :class="formDir.isPublished ? 'text-green-600 font-semibold' : 'text-gray-400'">
              <i :class="formDir.isPublished ? 'pi pi-eye' : 'pi pi-eye-slash'" class="mr-1"></i>
              {{ formDir.isPublished ? 'เผยแพร่ — ผู้ใช้ทั่วไปมองเห็น' : 'ซ่อน — เฉพาะ Admin เท่านั้น' }}
            </span>
          </div>
        </div>

        <div class="flex flex-col gap-2 md:col-span-2">
          <label class="text-sm font-semibold text-gray-700">รายละเอียดเพิ่มเติม</label>
          <Textarea v-model="formDir.description" rows="2" class="w-full" />
        </div>
      </div>
      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="dialogVisible = false" />
        <Button :label="isEditMode ? 'บันทึกการแก้ไข' : 'เพิ่มหมายเลข'" icon="pi pi-check" severity="help"
          :loading="isSaving" @click="saveDirectory" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
:deep(.p-datatable-header-cell) {
  background-color: #f8fafc !important;
  color: #475569 !important;
  font-weight: 700 !important;
}

:deep(.p-tablist-tab-list) {
  background-color: transparent !important;
}
</style>
