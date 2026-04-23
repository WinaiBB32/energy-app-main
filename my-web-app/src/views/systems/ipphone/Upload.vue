<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'

defineOptions({ name: 'FileUpload' })
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { logAudit } from '@/utils/auditLogger'
import { toUtcDateOnly } from '@/utils/dateUtils'

import Card from 'primevue/card'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'

// ─── Interfaces ───────────────────────────────────────────────────────────────
interface UploadStatRecord {
  id: string
  reportMonth: string | null
  fileName: string
  uploadedBy: string
  totalRecords: number
  createdAt: string
}

interface ParsedRow {
  extension: string
  answeredInbound: number
  noAnswerInbound: number
  busyInbound: number
  failedInbound: number
  voicemailInbound: number
  totalInbound: number
  answeredOutbound: number
  noAnswerOutbound: number
  busyOutbound: number
  failedOutbound: number
  voicemailOutbound: number
  totalOutbound: number
  totalCalls: number
  totalTalkDuration: string
}

interface CallLogRecord {
  id: string
  statId: string
  extension: string
  reportMonth: string
  answeredInbound: number
  noAnswerInbound: number
  busyInbound: number
  failedInbound: number
  voicemailInbound: number
  totalInbound: number
  answeredOutbound: number
  noAnswerOutbound: number
  busyOutbound: number
  failedOutbound: number
  voicemailOutbound: number
  totalOutbound: number
  totalCalls: number
  totalTalkDuration: string
  createdAt: string
}

// ─── State & Setup ────────────────────────────────────────────────────────────
const authStore = useAuthStore()
const toast = useAppToast()
const currentUserName = computed(
  () => authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ',
)

const uploadHistory = ref<UploadStatRecord[]>([])

async function loadHistory() {
  try {
    const res = await api.get('/IPPhoneMonthStat', { params: { take: '100' } })
    uploadHistory.value = res.data.items ?? res.data
  } catch {
    // ignore
  }
}

onMounted(() => {
  loadHistory()
})

// ─── CSV Template Download ──────────────────────────────────────────────────
const downloadTemplate = (): void => {
  const header = [
    'หมายเลขภายใน',
    'ตอบแล้ว(สายเข้า)',
    'ไม่มีคำตอบ(สายเข้า)',
    'ไปรษา(สายเข้า)',
    'สั่งเลย(สายเข้า)',
    'วอยซ์เมล(สายเข้า)',
    'รวม(สายเข้า)',
    'ตอบแล้ว(สายออก)',
    'ไม่มีคำตอบ(สายออก)',
    'ไปรษา(สายออก)',
    'สั่งเลย(สายออก)',
    'วอยซ์เมล(สายออก)',
    'รวม(สายออก)',
    'รวม',
    'ระยะเวลาพูดคุยทั้งหมด',
  ].join(',')

  const sampleRows = [
    '70101,1,0,0,0,0,1,0,0,0,0,0,0,1,00:01:37',
    '70102,32,2,0,0,0,34,2,0,0,0,0,2,36,01:07:05',
    '70103,1,5,0,0,0,6,0,0,0,0,0,0,6,00:01:17',
    '79905,45,5,0,0,0,50,30,5,0,0,0,35,85,01:23:45',
    '79906,10,0,0,0,0,10,60,10,0,0,0,70,80,00:55:00',
    '70131,80,10,0,0,0,90,20,5,0,0,0,25,115,02:10:30',
  ].join('\n')

  const bom = '﻿'
  const csv = bom + header + '\n' + sampleRows
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'ipphone_export_template.csv'
  a.click()
  URL.revokeObjectURL(url)
}

// ─── CSV Parser ───────────────────────────────────────────────────────────────
function parseCSVRow(row: string): string[] {
  const result: string[] = []
  let current = ''
  let inQuotes = false
  for (let i = 0; i < row.length; i++) {
    const char = row[i]
    if (char === '"') {
      if (inQuotes && row[i + 1] === '"') {
        current += '"'
        i++
      } else {
        inQuotes = !inQuotes
      }
    } else if (char === ',' && !inQuotes) {
      result.push(current.trim())
      current = ''
    } else {
      current += char
    }
  }
  result.push(current.trim())
  return result
}

// ─── Upload Logic ─────────────────────────────────────────────────────────────
const uploadMonth = ref<Date | null>(null)
const selectedFile = ref<File | null>(null)
const isUploading = ref<boolean>(false)
const uploadSuccess = ref<string>('')
const uploadError = ref<string>('')

const previewData = ref<ParsedRow[]>([])
const showPreview = ref(false)
const parseErrors = ref<string[]>([])

const handleFileSelect = (event: Event): void => {
  const target = event.target as HTMLInputElement
  uploadSuccess.value = ''
  uploadError.value = ''
  previewData.value = []
  showPreview.value = false
  parseErrors.value = []
  if (target.files && target.files.length > 0) {
    selectedFile.value = target.files[0] ?? null
    if (selectedFile.value) parseFilePreview(selectedFile.value)
  }
}

const parseFilePreview = (file: File): void => {
  const reader = new FileReader()
  reader.onload = (e: ProgressEvent<FileReader>) => {
    try {
      const buffer = e.target?.result as ArrayBuffer
      let text: string
      try {
        text = new TextDecoder('utf-8', { fatal: true }).decode(buffer)
      } catch {
        text = new TextDecoder('windows-874').decode(buffer)
      }

      const rows = text
        .split('\n')
        .map((row) => row.trim())
        .filter((row) => row.length > 0)

      if (rows.length < 2) {
        uploadError.value = 'ไฟล์ว่างเปล่า หรือไม่มีข้อมูล'
        return
      }

      const errors: string[] = []
      const parsed: ParsedRow[] = []

      for (let i = 1; i < rows.length; i++) {
        const row = rows[i]
        if (!row) continue
        const cols = parseCSVRow(row)

        if (cols.length < 14) continue

        const rawExt = cols[0] ?? ''
        const ext = rawExt.includes('-')
          ? (rawExt.split('-')[0] ?? '').trim()
          : rawExt.trim()

        const answeredInbound   = Number(cols[1])  || 0
        const noAnswerInbound   = Number(cols[2])  || 0
        const busyInbound       = Number(cols[3])  || 0
        const failedInbound     = Number(cols[4])  || 0
        const voicemailInbound  = Number(cols[5])  || 0
        const totalInbound      = Number(cols[6])  || 0
        const answeredOutbound  = Number(cols[7])  || 0
        const noAnswerOutbound  = Number(cols[8])  || 0
        const busyOutbound      = Number(cols[9])  || 0
        const failedOutbound    = Number(cols[10]) || 0
        const voicemailOutbound = Number(cols[11]) || 0
        const totalOutbound     = Number(cols[12]) || 0
        const totalCalls        = Number(cols[13]) || 0
        const totalTalkDuration = (cols[14] ?? '00:00:00').trim()

        if (!ext) {
          errors.push(`แถว ${i + 1}: ไม่มีหมายเลขเบอร์โทร (extension)`)
          continue
        }

        parsed.push({
          extension: ext,
          answeredInbound,
          noAnswerInbound,
          busyInbound,
          failedInbound,
          voicemailInbound,
          totalInbound,
          answeredOutbound,
          noAnswerOutbound,
          busyOutbound,
          failedOutbound,
          voicemailOutbound,
          totalOutbound,
          totalCalls,
          totalTalkDuration,
        })
      }

      previewData.value = parsed
      parseErrors.value = errors
      showPreview.value = true
    } catch (err: unknown) {
      uploadError.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการอ่านไฟล์'
    }
  }
  reader.readAsArrayBuffer(file)
}

const handleUploadExcel = async (): Promise<void> => {
  uploadSuccess.value = ''
  uploadError.value = ''

  if (!uploadMonth.value) {
    uploadError.value = 'กรุณาเลือกรอบเดือนของสถิติ'
    return
  }
  if (previewData.value.length === 0) {
    uploadError.value = 'ไม่มีข้อมูลที่ถูกต้องสำหรับนำเข้า'
    return
  }

  const selectedDate = uploadMonth.value
  isUploading.value = true

  try {
    const dupRes = await api.get('/IPPhoneMonthStat/check-duplicate', {
      params: { year: selectedDate.getFullYear(), month: selectedDate.getMonth() + 1 },
    })
    if (dupRes.data.exists) {
      uploadError.value = `ข้อมูลสถิติ${selectedDate.toLocaleDateString('th-TH', { year: 'numeric', month: 'long' })} ถูกนำเข้าไปแล้ว กรุณาตรวจสอบประวัติการนำเข้า`
      return
    }

    const parsedData = previewData.value
    await api.post('/IPPhoneMonthStat', {
      reportMonth: toUtcDateOnly(selectedDate),
      fileName: selectedFile.value?.name ?? 'ไม่ระบุ',
      uploadedBy: currentUserName.value,
      totalRecords: parsedData.length,
      logs: parsedData,
    })

    logAudit(
      {
        uid: authStore.user?.uid ?? '',
        displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '',
        email: authStore.user?.email ?? '',
        role: authStore.userProfile?.role ?? 'user',
      },
      'UPLOAD',
      'IPPhoneUpload',
      `นำเข้าสถิติ ${parsedData.length} รายการ ไฟล์: ${selectedFile.value?.name ?? ''}`,
    )
    uploadSuccess.value = `นำเข้าข้อมูลสำเร็จ! บันทึกสถิติทั้งหมด ${parsedData.length} หมายเลขเข้าสู่ระบบแล้ว`
    uploadMonth.value = null
    selectedFile.value = null
    previewData.value = []
    showPreview.value = false

    const fileInput = document.getElementById('excelFile') as HTMLInputElement
    if (fileInput) fileInput.value = ''
    await loadHistory()
  } catch (err: unknown) {
    uploadError.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการบันทึกข้อมูล'
  } finally {
    isUploading.value = false
  }
}

// ─── Delete Upload ─────────────────────────────────────────────────────────────
const confirmDeleteId = ref<string | null>(null)
const isDeleting = ref(false)
const showDeleteDialog = computed({
  get: () => !!confirmDeleteId.value,
  set: (v: boolean) => { if (!v) confirmDeleteId.value = null },
})

const handleDeleteUpload = async (): Promise<void> => {
  if (!confirmDeleteId.value) return
  isDeleting.value = true
  try {
    await api.delete(`/IPPhoneMonthStat/${confirmDeleteId.value}`)
    uploadSuccess.value = 'ลบข้อมูลการนำเข้าและ log ที่เกี่ยวข้องเรียบร้อยแล้ว'
    await loadHistory()
  } catch (err) {
    uploadError.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการลบ'
  } finally {
    isDeleting.value = false
    confirmDeleteId.value = null
  }
}

// ─── View / Edit Logs ─────────────────────────────────────────────────────────
const viewingStat = ref<UploadStatRecord | null>(null)
const statLogs = ref<CallLogRecord[]>([])
const isLoadingLogs = ref(false)
const viewDialogVisible = computed({
  get: () => !!viewingStat.value,
  set: (v: boolean) => { if (!v) { viewingStat.value = null; statLogs.value = [] } },
})

const openViewDialog = async (stat: UploadStatRecord) => {
  viewingStat.value = stat
  isLoadingLogs.value = true
  statLogs.value = []
  try {
    const res = await api.get(`/IPPhoneMonthStat/${stat.id}/logs`)
    statLogs.value = res.data
  } catch (e) {
    toast.fromError(e, 'โหลดข้อมูล log ไม่สำเร็จ')
  } finally {
    isLoadingLogs.value = false
  }
}

// ─── Edit individual log ───────────────────────────────────────────────────────
interface EditForm {
  answeredInbound: number
  noAnswerInbound: number
  busyInbound: number
  failedInbound: number
  voicemailInbound: number
  totalInbound: number
  answeredOutbound: number
  noAnswerOutbound: number
  busyOutbound: number
  failedOutbound: number
  voicemailOutbound: number
  totalOutbound: number
  totalCalls: number
  totalTalkDuration: string
}

const editingLog = ref<CallLogRecord | null>(null)
const editForm = ref<EditForm>({
  answeredInbound: 0, noAnswerInbound: 0, busyInbound: 0, failedInbound: 0, voicemailInbound: 0,
  totalInbound: 0,
  answeredOutbound: 0, noAnswerOutbound: 0, busyOutbound: 0, failedOutbound: 0, voicemailOutbound: 0,
  totalOutbound: 0,
  totalCalls: 0,
  totalTalkDuration: '',
})
const isSavingLog = ref(false)
const editDialogVisible = computed({
  get: () => !!editingLog.value,
  set: (v: boolean) => { if (!v) editingLog.value = null },
})

const openEditLog = (log: CallLogRecord) => {
  editingLog.value = log
  editForm.value = {
    answeredInbound: log.answeredInbound,
    noAnswerInbound: log.noAnswerInbound,
    busyInbound: log.busyInbound,
    failedInbound: log.failedInbound,
    voicemailInbound: log.voicemailInbound,
    totalInbound: log.totalInbound,
    answeredOutbound: log.answeredOutbound,
    noAnswerOutbound: log.noAnswerOutbound,
    busyOutbound: log.busyOutbound,
    failedOutbound: log.failedOutbound,
    voicemailOutbound: log.voicemailOutbound,
    totalOutbound: log.totalOutbound,
    totalCalls: log.totalCalls,
    totalTalkDuration: log.totalTalkDuration,
  }
}

// Auto-compute totalCalls when totalInbound/totalOutbound change
watch(
  () => [editForm.value.totalInbound, editForm.value.totalOutbound] as [number, number],
  ([inb, out]) => { editForm.value.totalCalls = inb + out },
)

const saveEditLog = async () => {
  if (!editingLog.value) return
  isSavingLog.value = true
  try {
    const res = await api.put(`/IPPhoneMonthStat/logs/${editingLog.value.id}`, editForm.value)
    const idx = statLogs.value.findIndex((l) => l.id === editingLog.value!.id)
    if (idx !== -1) statLogs.value[idx] = { ...statLogs.value[idx], ...res.data }
    toast.success('บันทึกการแก้ไขสำเร็จ')
    editingLog.value = null
  } catch (e) {
    toast.fromError(e, 'บันทึกไม่สำเร็จ')
  } finally {
    isSavingLog.value = false
  }
}

// ─── Helpers ───────────────────────────────────────────────────────────────────
const formatThaiMonth = (dateStr: string | null | undefined): string =>
  dateStr ? new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'long' }) : '-'

const answerRate = (row: { totalCalls: number; answeredInbound: number; answeredOutbound: number }): string => {
  const total = row.totalCalls
  if (!total) return '0%'
  const answered = row.answeredInbound + row.answeredOutbound
  return ((answered / total) * 100).toFixed(0) + '%'
}
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10">
    <div class="mb-6">
      <h2 class="text-3xl font-bold text-gray-800">
        <i class="pi pi-cloud-upload text-teal-600 mr-2"></i>นำเข้าสถิติ IP-Phone (CSV)
      </h2>
      <p class="text-gray-500 mt-1">นำเข้าข้อมูลสถิติการโทรประจำเดือนจากระบบ IP-Phone</p>
    </div>

    <!-- Format Guide -->
    <Card class="shadow-sm border-none mb-6 border-l-4 border-teal-400">
      <template #title>
        <div class="text-base font-bold text-gray-700">
          <i class="pi pi-table text-teal-500 mr-2"></i>รูปแบบไฟล์ CSV ที่รองรับ (15 คอลัมน์ ตามรายงาน IP-Phone จริง)
        </div>
      </template>
      <template #content>
        <div class="overflow-x-auto">
          <table class="text-sm w-full border-collapse">
            <thead>
              <tr class="bg-teal-50">
                <th class="border border-teal-200 px-3 py-2 text-left font-bold text-teal-800">คอลัมน์</th>
                <th class="border border-teal-200 px-3 py-2 text-left font-bold text-teal-800">ชื่อคอลัมน์ในรายงาน</th>
                <th class="border border-teal-200 px-3 py-2 text-center font-bold text-teal-800">ใช้งาน</th>
                <th class="border border-teal-200 px-3 py-2 text-left font-bold text-teal-800">เก็บเป็น Field</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="col in [
                { col:'A', name:'หมายเลขภายใน',           field:'extension'           },
                { col:'B', name:'ตอบแล้ว(สายเข้า)',        field:'answeredInbound'     },
                { col:'C', name:'ไม่มีคำตอบ(สายเข้า)',     field:'noAnswerInbound'     },
                { col:'D', name:'ไม่ว่าง(สายเข้า)',         field:'busyInbound'         },
                { col:'E', name:'ล้มเหลว(สายเข้า)',         field:'failedInbound'       },
                { col:'F', name:'วอยซ์เมล(สายเข้า)',       field:'voicemailInbound'    },
                { col:'G', name:'รวม(สายเข้า)',             field:'totalInbound'        },
                { col:'H', name:'ตอบแล้ว(สายออก)',         field:'answeredOutbound'    },
                { col:'I', name:'ไม่มีคำตอบ(สายออก)',      field:'noAnswerOutbound'    },
                { col:'J', name:'ไม่ว่าง(สายออก)',          field:'busyOutbound'        },
                { col:'K', name:'ล้มเหลว(สายออก)',          field:'failedOutbound'      },
                { col:'L', name:'วอยซ์เมล(สายออก)',        field:'voicemailOutbound'   },
                { col:'M', name:'รวม(สายออก)',              field:'totalOutbound'       },
                { col:'N', name:'รวม (ทั้งหมด)',            field:'totalCalls'          },
                { col:'O', name:'ระยะเวลาพูดคุยทั้งหมด',  field:'totalTalkDuration'   },
              ]" :key="col.col" class="bg-emerald-50 hover:bg-emerald-100">
                <td class="border border-gray-200 px-3 py-1.5 font-mono font-bold text-center text-teal-700">{{ col.col }}</td>
                <td class="border border-gray-200 px-3 py-1.5 text-gray-800 font-semibold">{{ col.name }}</td>
                <td class="border border-gray-200 px-3 py-1.5 text-center">
                  <span class="text-emerald-600 font-bold text-xs bg-emerald-100 px-2 py-0.5 rounded-full">✓ ใช้</span>
                </td>
                <td class="border border-gray-200 px-3 py-1.5 font-mono text-xs text-blue-700">
                  {{ col.field }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="mt-4 flex items-center gap-3">
          <Button
            label="ดาวน์โหลด Template ตัวอย่าง"
            icon="pi pi-download"
            severity="secondary"
            outlined
            size="small"
            @click="downloadTemplate"
          />
          <span class="text-xs text-gray-400">ไฟล์ตัวอย่าง 15 column ตรงกับรายงาน IP-Phone จริง</span>
        </div>
      </template>
    </Card>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <!-- Upload Form -->
      <Card class="shadow-sm border-none">
        <template #title>
          <div class="text-lg font-bold text-gray-800">
            <i class="pi pi-file-excel text-teal-600 mr-2"></i>นำเข้าสถิติ
          </div>
        </template>
        <template #content>
          <Message v-if="uploadSuccess" severity="success" :closable="true">{{ uploadSuccess }}</Message>
          <Message v-if="uploadError" severity="error" :closable="true">{{ uploadError }}</Message>

          <div class="flex flex-col gap-5 mt-2">
            <div class="flex flex-col gap-2">
              <label class="font-semibold text-sm text-gray-700">
                สถิติประจำเดือน <span class="text-red-500">*</span>
              </label>
              <DatePicker
                v-model="uploadMonth"
                view="month"
                dateFormat="MM yy"
                placeholder="-- เลือกรอบเดือน --"
                class="w-full"
                showIcon
              />
            </div>

            <div class="flex flex-col gap-2">
              <label class="font-semibold text-sm text-gray-700">
                ไฟล์สถิติ (.csv) <span class="text-red-500">*</span>
              </label>
              <div class="border-2 border-dashed border-gray-300 rounded-xl p-6 text-center hover:bg-gray-50 transition-colors cursor-pointer">
                <i class="pi pi-cloud-upload text-4xl text-gray-400 mb-3 block"></i>
                <input
                  type="file"
                  id="excelFile"
                  accept=".csv"
                  @change="handleFileSelect"
                  class="block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-teal-50 file:text-teal-700 hover:file:bg-teal-100"
                />
              </div>
              <p class="text-xs text-gray-400 mt-1">
                <i class="pi pi-info-circle"></i> รองรับไฟล์ .csv (UTF-8 หรือ Windows-874/TIS-620)
              </p>
            </div>

            <div v-if="showPreview" class="rounded-lg bg-teal-50 border border-teal-200 px-4 py-3">
              <p class="font-semibold text-teal-800 text-sm mb-1">
                <i class="pi pi-check-circle mr-1"></i>อ่านไฟล์สำเร็จ
              </p>
              <p class="text-sm text-teal-700">พบข้อมูลที่ถูกต้อง <strong>{{ previewData.length }}</strong> หมายเลข</p>
              <div v-if="parseErrors.length > 0" class="mt-2">
                <p class="text-xs font-bold text-amber-700">คำเตือน ({{ parseErrors.length }} รายการ):</p>
                <ul class="text-xs text-amber-600 list-disc list-inside mt-1">
                  <li v-for="(e, i) in parseErrors.slice(0, 5)" :key="i">{{ e }}</li>
                  <li v-if="parseErrors.length > 5">... และอีก {{ parseErrors.length - 5 }} รายการ</li>
                </ul>
              </div>
            </div>

            <Button
              label="บันทึกข้อมูลเข้าระบบ"
              icon="pi pi-database"
              severity="success"
              class="w-full mt-2 py-3 font-bold"
              :loading="isUploading"
              :disabled="!previewData.length || !uploadMonth"
              @click="handleUploadExcel"
            />
          </div>
        </template>
      </Card>

      <!-- Upload History -->
      <Card class="shadow-sm border-none">
        <template #title>
          <div class="text-lg font-bold text-gray-800">
            <i class="pi pi-history text-gray-500 mr-2"></i>ประวัติการนำเข้าข้อมูล
          </div>
        </template>
        <template #content>
          <DataTable
            :value="uploadHistory"
            paginator
            :rows="5"
            stripedRows
            responsiveLayout="scroll"
            emptyMessage="ยังไม่มีประวัติการนำเข้าไฟล์"
          >
            <Column header="รอบเดือน">
              <template #body="sp">
                <span class="font-bold text-teal-700">{{ formatThaiMonth(sp.data.reportMonth) }}</span>
              </template>
            </Column>
            <Column header="รายละเอียด">
              <template #body="sp">
                <div class="text-sm text-gray-800 truncate w-36" :title="sp.data.fileName">{{ sp.data.fileName }}</div>
                <div class="text-xs text-gray-500 mt-0.5">โดย: {{ sp.data.uploadedBy }}</div>
              </template>
            </Column>
            <Column header="จำนวน">
              <template #body="sp">
                <Tag :value="`${sp.data.totalRecords} เบอร์`" severity="info" rounded class="text-[10px]" />
              </template>
            </Column>
            <Column header="จัดการ">
              <template #body="sp">
                <div class="flex items-center gap-1">
                  <Button
                    icon="pi pi-list"
                    severity="secondary"
                    text
                    rounded
                    size="small"
                    @click="openViewDialog(sp.data)"
                    v-tooltip.top="'ดู / แก้ไขรายการ'"
                  />
                  <Button
                    icon="pi pi-trash"
                    severity="danger"
                    text
                    rounded
                    size="small"
                    @click="confirmDeleteId = sp.data.id"
                    v-tooltip.top="'ลบข้อมูลชุดนี้'"
                  />
                </div>
              </template>
            </Column>
          </DataTable>
        </template>
      </Card>
    </div>

    <!-- Preview Table (before upload) -->
    <Card v-if="showPreview && previewData.length > 0" class="shadow-sm border-none mt-6">
      <template #title>
        <div class="text-base font-bold text-gray-700">
          <i class="pi pi-eye text-teal-500 mr-2"></i>ตรวจสอบข้อมูลก่อนบันทึก ({{ previewData.length }} รายการ)
        </div>
      </template>
      <template #content>
        <DataTable :value="previewData" :rows="10" paginator stripedRows responsiveLayout="scroll" class="text-sm">
          <Column field="extension" header="เบอร์โทร" style="font-weight:700" />
          <Column header="สายเข้า (รับ/ทั้งหมด)">
            <template #body="{ data }">
              <span class="text-green-700 font-bold">{{ data.answeredInbound }}</span>
              <span class="text-gray-400"> / {{ data.totalInbound }}</span>
            </template>
          </Column>
          <Column header="สายออก (รับ/ทั้งหมด)">
            <template #body="{ data }">
              <span class="text-blue-700 font-bold">{{ data.answeredOutbound }}</span>
              <span class="text-gray-400"> / {{ data.totalOutbound }}</span>
            </template>
          </Column>
          <Column header="รวมสาย">
            <template #body="{ data }">
              <span class="font-bold text-gray-900">{{ data.totalCalls.toLocaleString() }}</span>
            </template>
          </Column>
          <Column header="% รับสาย">
            <template #body="{ data }">
              <Tag :value="answerRate(data)" :severity="parseFloat(answerRate(data)) >= 80 ? 'success' : parseFloat(answerRate(data)) >= 50 ? 'warn' : 'danger'" rounded />
            </template>
          </Column>
          <Column field="totalTalkDuration" header="เวลาสนทนา" />
        </DataTable>
      </template>
    </Card>

    <!-- ── View / Edit Logs Dialog ────────────────────────────────────────────── -->
    <Dialog
      v-model:visible="viewDialogVisible"
      modal
      :header="`สถิติประจำ${viewingStat ? formatThaiMonth(viewingStat.reportMonth) : ''}`"
      :style="{ width: '90vw', maxWidth: '1100px' }"
      :draggable="false"
    >
      <div v-if="isLoadingLogs" class="flex justify-center py-12 text-gray-400">
        <i class="pi pi-spin pi-spinner text-2xl mr-2"></i> กำลังโหลด...
      </div>

      <DataTable
        v-else
        :value="statLogs"
        :rows="15"
        paginator
        stripedRows
        responsiveLayout="scroll"
        class="text-sm"
        emptyMessage="ไม่มีข้อมูล"
      >
        <Column field="extension" header="เบอร์โทร" sortable>
          <template #body="{ data }">
            <span class="font-bold text-gray-800">{{ data.extension }}</span>
          </template>
        </Column>
        <Column header="สายเข้า (รับ/ทั้งหมด)" sortable sort-field="totalInbound">
          <template #body="{ data }">
            <span class="text-green-700 font-bold">{{ data.answeredInbound }}</span>
            <span class="text-gray-400 text-xs"> / {{ data.totalInbound }}</span>
          </template>
        </Column>
        <Column header="สายออก (รับ/ทั้งหมด)" sortable sort-field="totalOutbound">
          <template #body="{ data }">
            <span class="text-blue-700 font-bold">{{ data.answeredOutbound }}</span>
            <span class="text-gray-400 text-xs"> / {{ data.totalOutbound }}</span>
          </template>
        </Column>
        <Column header="รวมสาย" sortable sort-field="totalCalls">
          <template #body="{ data }">
            <span class="font-bold">{{ data.totalCalls.toLocaleString() }}</span>
          </template>
        </Column>
        <Column header="% รับสาย">
          <template #body="{ data }">
            <Tag
              :value="answerRate(data)"
              :severity="parseFloat(answerRate(data)) >= 80 ? 'success' : parseFloat(answerRate(data)) >= 50 ? 'warn' : 'danger'"
              rounded
            />
          </template>
        </Column>
        <Column field="totalTalkDuration" header="เวลาสนทนา" />
        <Column header="แก้ไข" style="width: 70px">
          <template #body="{ data }">
            <Button
              icon="pi pi-pencil"
              severity="secondary"
              text
              rounded
              size="small"
              @click="openEditLog(data)"
              v-tooltip.top="'แก้ไขข้อมูลเบอร์นี้'"
            />
          </template>
        </Column>
      </DataTable>
    </Dialog>

    <!-- ── Edit Log Dialog ─────────────────────────────────────────────────────── -->
    <Dialog
      v-model:visible="editDialogVisible"
      modal
      :header="`แก้ไขสถิติ — เบอร์ ${editingLog?.extension}`"
      :style="{ width: '560px' }"
      :draggable="false"
    >
      <div class="flex flex-col gap-5 py-2">
        <!-- สายเข้า -->
        <div>
          <p class="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">
            <i class="pi pi-arrow-down text-green-600 mr-1"></i>สายเข้า (Inbound)
          </p>
          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">ตอบแล้ว</label>
              <InputNumber v-model="editForm.answeredInbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">ไม่มีคำตอบ</label>
              <InputNumber v-model="editForm.noAnswerInbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">ไม่ว่าง (Busy)</label>
              <InputNumber v-model="editForm.busyInbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">ล้มเหลว</label>
              <InputNumber v-model="editForm.failedInbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">วอยซ์เมล</label>
              <InputNumber v-model="editForm.voicemailInbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs font-bold text-green-700">รวมสายเข้า</label>
              <InputNumber v-model="editForm.totalInbound" :min="0" showButtons fluid class="font-bold" />
            </div>
          </div>
        </div>

        <hr class="border-gray-200" />

        <!-- สายออก -->
        <div>
          <p class="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">
            <i class="pi pi-arrow-up text-blue-600 mr-1"></i>สายออก (Outbound)
          </p>
          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">ตอบแล้ว</label>
              <InputNumber v-model="editForm.answeredOutbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">ไม่มีคำตอบ</label>
              <InputNumber v-model="editForm.noAnswerOutbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">ไม่ว่าง (Busy)</label>
              <InputNumber v-model="editForm.busyOutbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">ล้มเหลว</label>
              <InputNumber v-model="editForm.failedOutbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs text-gray-600">วอยซ์เมล</label>
              <InputNumber v-model="editForm.voicemailOutbound" :min="0" showButtons fluid />
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs font-bold text-blue-700">รวมสายออก</label>
              <InputNumber v-model="editForm.totalOutbound" :min="0" showButtons fluid class="font-bold" />
            </div>
          </div>
        </div>

        <hr class="border-gray-200" />

        <!-- ยอดรวม -->
        <div class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-bold text-gray-700">รวมทั้งหมด (คำนวณอัตโนมัติ)</label>
            <InputNumber
              v-model="editForm.totalCalls"
              :min="0"
              fluid
              disabled
              class="bg-gray-50"
            />
            <p class="text-[11px] text-gray-400">= รวมสายเข้า + รวมสายออก</p>
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-bold text-gray-700">เวลาสนทนารวม (HH:MM:SS)</label>
            <InputText
              v-model="editForm.totalTalkDuration"
              placeholder="00:00:00"
              fluid
            />
          </div>
        </div>
      </div>

      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="editingLog = null" />
        <Button
          label="บันทึกการแก้ไข"
          icon="pi pi-save"
          severity="success"
          :loading="isSavingLog"
          @click="saveEditLog"
        />
      </template>
    </Dialog>

    <!-- Confirm Delete Dialog -->
    <Dialog v-model:visible="showDeleteDialog" modal header="ยืนยันการลบข้อมูล" :style="{ width: '400px' }">
      <p class="text-gray-700">ต้องการลบข้อมูลการนำเข้าชุดนี้และ <strong>call logs ทั้งหมดที่เกี่ยวข้อง</strong> หรือไม่?</p>
      <p class="text-red-500 text-sm mt-2 font-semibold">⚠️ ไม่สามารถกู้คืนได้ กรุณาตรวจสอบก่อน</p>
      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="confirmDeleteId = null" />
        <Button label="ลบข้อมูล" icon="pi pi-trash" severity="danger" :loading="isDeleting" @click="handleDeleteUpload" />
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
</style>
