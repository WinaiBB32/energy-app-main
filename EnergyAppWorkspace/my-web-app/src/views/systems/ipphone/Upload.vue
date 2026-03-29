<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'

defineOptions({ name: 'FileUpload' })
import {
  collection,
  doc,
  serverTimestamp,
  query,
  onSnapshot,
  Timestamp,
  writeBatch,
  where,
  getDocs,
  deleteDoc,
} from 'firebase/firestore'
// Firebase Removed
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'

import Card from 'primevue/card'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'

// ─── Interfaces ───────────────────────────────────────────────────────────────
interface UploadStatRecord {
  id: string
  reportMonth: Timestamp | null
  fileName: string
  uploadedBy: string
  totalRecords: number
  createdAt: Timestamp
}

interface ParsedRow {
  extension: string
  // ─ Inbound ─────────────────────────────────────────────
  answeredInbound: number      // B[1]  ตอบแล้ว
  noAnswerInbound: number      // C[2]  ไม่มีคำตอบ = ไม่รับสาย
  busyInbound: number          // D[3]  ไม่ว่าง = ไม่รับสาย
  failedInbound: number        // E[4]  ล้มเหลว = Busy
  voicemailInbound: number     // F[5]  วอยซ์เมล = Busy
  totalInbound: number         // G[6]  รวมสายเข้า
  // ─ Outbound ─────────────────────────────────────────
  answeredOutbound: number     // H[7]  ตอบแล้ว
  noAnswerOutbound: number     // I[8]  ไม่มีคำตอบ
  busyOutbound: number         // J[9]  ไม่ว่าง
  failedOutbound: number       // K[10] ล้มเหลว
  voicemailOutbound: number    // L[11] วอยซ์เมล
  totalOutbound: number        // M[12] รวมสายออก
  totalCalls: number           // computed G+M
  totalTalkDuration: string    // O[14]
}

// ─── State & Setup ────────────────────────────────────────────────────────────
const authStore = useAuthStore()
const currentUserName = computed(
  () => authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ',
)

const uploadHistory = ref<UploadStatRecord[]>([])
let unsubUploads: () => void

function statCreatedMs(r: UploadStatRecord): number {
  const c = r.createdAt
  return c && typeof c.toMillis === 'function' ? c.toMillis() : 0
}

onMounted(() => {
  unsubUploads = onSnapshot(
    collection(db, 'ipphone_monthly_stats'),
    (snap) => {
      const records: UploadStatRecord[] = []
      snap.forEach((document) =>
        records.push({ id: document.id, ...document.data() } as UploadStatRecord),
      )
      records.sort((a, b) => statCreatedMs(b) - statCreatedMs(a))
      uploadHistory.value = records
    },
  )
})

onUnmounted(() => {
  if (unsubUploads) unsubUploads()
})

// ─── CSV Template Download ──────────────────────────────────────────────────
const downloadTemplate = (): void => {
  // ตรงกับรูปแบบ export จากระบบ IP-Phone จริง (15 column A-O)
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

  const bom = '\uFEFF'
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

// Preview state
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

        // raw export จากระบบ IP-Phone มี 15 column (A-O)
        if (cols.length < 14) continue

        // A: หมายเลขภายใน (อาจมี "-" ตามหลัง)
        const rawExt = cols[0] ?? ''
        const ext = rawExt.includes('-')
          ? (rawExt.split('-')[0] ?? '').trim()
          : rawExt.trim()

        //  B[1]  ตอบแล้ว(สายเข้า)
        //  C[2]  ไม่มีคำตอบ(สายเข้า) = ไม่รับสาย
        //  D[3]  ไม่ว่าง(ขาเข้า) = ไม่รับสาย
        //  E[4]  ล้มเหลว(ขาเข้า) = Busy
        //  F[5]  วอยซ์เมล(ขาเข้า) = Busy
        //  G[6]  รวม(สายเข้า)
        //  H[7]  ตอบแล้ว(สายออก)
        //  I[8]  ไม่มีคำตอบ(สายออก)
        //  J[9]  ไม่ว่าง(ขาออก)
        //  K[10] ล้มเหลว(ขาออก)
        //  L[11] วอยซ์เมล(ขาออก)
        //  M[12] รวม(สายออก)
        //  N[13] รวมทั้งหมด
        //  O[14] ระยะเวลาพูดคุย
        const answeredInbound   = Number(cols[1])  || 0
        const noAnswerInbound   = Number(cols[2])  || 0  // ไม่รับสาย
        const busyInbound       = Number(cols[3])  || 0  // ไม่รับสาย
        const failedInbound     = Number(cols[4])  || 0  // Busy
        const voicemailInbound  = Number(cols[5])  || 0  // Busy
        const totalInbound      = Number(cols[6])  || 0
        const answeredOutbound  = Number(cols[7])  || 0
        const noAnswerOutbound  = Number(cols[8])  || 0
        const busyOutbound      = Number(cols[9])  || 0
        const failedOutbound    = Number(cols[10]) || 0
        const voicemailOutbound = Number(cols[11]) || 0
        const totalOutbound     = Number(cols[12]) || 0
        const totalCallsRaw     = Number(cols[13]) || 0
        const totalTalkDuration = (cols[14] ?? '00:00:00').trim()

        if (!ext) {
          errors.push(`แถว ${i + 1}: ไม่มีหมายเลขเบอร์โทร (extension)`)
          continue
        }

        const computedTotal = totalInbound + totalOutbound
        if (totalCallsRaw !== computedTotal && computedTotal > 0) {
          errors.push(
            `แถว ${i + 1} (${ext}): คอลัมน์ N (${totalCallsRaw}) ไม่ตรงกับ G+M (${computedTotal}) — จะใช้ค่าคำนวณ G+M แทน`,
          )
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
          totalCalls: computedTotal > 0 ? computedTotal : totalCallsRaw,
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

  // ตรวจสอบข้อมูลซ้ำ
  const selectedDate = uploadMonth.value
  const startOfMonth = new Date(selectedDate.getFullYear(), selectedDate.getMonth(), 1)
  const startOfNextMonth = new Date(selectedDate.getFullYear(), selectedDate.getMonth() + 1, 1)
  const dupSnap = await getDocs(
    query(
      collection(db, 'ipphone_monthly_stats'),
      where('reportMonth', '>=', startOfMonth),
      where('reportMonth', '<', startOfNextMonth),
    ),
  )
  if (!dupSnap.empty) {
    uploadError.value = `ข้อมูลสถิติ${selectedDate.toLocaleDateString('th-TH', { year: 'numeric', month: 'long' })} ถูกนำเข้าไปแล้ว กรุณาตรวจสอบประวัติการนำเข้า`
    return
  }

  isUploading.value = true

  try {
    const parsedData = previewData.value

    // Batch write แบ่ง chunk ละ 499
    const BATCH_SIZE = 499
    const statRef = doc(collection(db, 'ipphone_monthly_stats'))

    const firstBatch = writeBatch(db)
    firstBatch.set(statRef, {
      reportMonth: uploadMonth.value,
      fileName: selectedFile.value?.name ?? 'ไม่ระบุ',
      uploadedBy: currentUserName.value,
      totalRecords: parsedData.length,
      createdAt: serverTimestamp(),
    })
    parsedData.slice(0, BATCH_SIZE).forEach((item) => {
      const logRef = doc(collection(db, 'ipphone_call_logs'))
      firstBatch.set(logRef, { 
        ...item, 
        reportMonth: uploadMonth.value,
        statId: statRef.id, 
        createdAt: serverTimestamp() 
      })
    })
    await firstBatch.commit()

    for (let i = BATCH_SIZE; i < parsedData.length; i += BATCH_SIZE) {
      const batch = writeBatch(db)
      parsedData.slice(i, i + BATCH_SIZE).forEach((item) => {
        const logRef = doc(collection(db, 'ipphone_call_logs'))
        batch.set(logRef, { 
          ...item, 
          reportMonth: uploadMonth.value,
          statId: statRef.id, 
          createdAt: serverTimestamp() 
        })
      })
      await batch.commit()
    }

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
    // ลบ call_logs ที่มี statId ตรงกัน
    const logsSnap = await getDocs(
      query(collection(db, 'ipphone_call_logs'), where('statId', '==', confirmDeleteId.value)),
    )
    const BATCH_SIZE = 499
    for (let i = 0; i < logsSnap.docs.length; i += BATCH_SIZE) {
      const batch = writeBatch(db)
      logsSnap.docs.slice(i, i + BATCH_SIZE).forEach((d) => batch.delete(d.ref))
      await batch.commit()
    }
    // ลบ stat record
    await deleteDoc(doc(db, 'ipphone_monthly_stats', confirmDeleteId.value))
    uploadSuccess.value = 'ลบข้อมูลการนำเข้าและ log ที่เกี่ยวข้องเรียบร้อยแล้ว'
  } catch (err) {
    uploadError.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการลบ'
  } finally {
    isDeleting.value = false
    confirmDeleteId.value = null
  }
}

// ─── Helpers ───────────────────────────────────────────────────────────────────
const formatThaiMonth = (ts: Timestamp | null | undefined): string =>
  ts ? ts.toDate().toLocaleDateString('th-TH', { year: 'numeric', month: 'long' }) : '-'

const answerRate = (row: ParsedRow): string => {
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
                { col:'A', name:'หมายเลขภายใน',           used:true  },
                { col:'B', name:'ตอบแล้ว(สายเข้า)',        used:true  },
                { col:'C', name:'ไม่มีคำตอบ(สายเข้า)',     used:false },
                { col:'D', name:'ไปรษา(สายเข้า)',           used:false },
                { col:'E', name:'สั่งเลย(สายเข้า)',         used:false },
                { col:'F', name:'วอยซ์เมล(สายเข้า)',       used:false },
                { col:'G', name:'รวม(สายเข้า)',             used:true  },
                { col:'H', name:'ตอบแล้ว(สายออก)',         used:true  },
                { col:'I', name:'ไม่มีคำตอบ(สายออก)',      used:false },
                { col:'J', name:'ไปรษา(สายออก)',            used:false },
                { col:'K', name:'สั่งเลย(สายออก)',          used:false },
                { col:'L', name:'วอยซ์เมล(สายออก)',        used:false },
                { col:'M', name:'รวม(สายออก)',              used:true  },
                { col:'N', name:'รวม (ทั้งหมด)',            used:true  },
                { col:'O', name:'ระยะเวลาพูดคุยทั้งหมด',  used:true  },
              ]" :key="col.col" :class="col.used ? 'bg-emerald-50 hover:bg-emerald-100' : 'bg-gray-50'">
                <td class="border border-gray-200 px-3 py-1.5 font-mono font-bold text-center" :class="col.used ? 'text-teal-700' : 'text-gray-300'">{{ col.col }}</td>
                <td class="border border-gray-200 px-3 py-1.5" :class="col.used ? 'text-gray-800 font-semibold' : 'text-gray-400'">{{ col.name }}</td>
                <td class="border border-gray-200 px-3 py-1.5 text-center">
                  <span v-if="col.used" class="text-emerald-600 font-bold text-xs bg-emerald-100 px-2 py-0.5 rounded-full">✓ ใช้</span>
                  <span v-else class="text-gray-300 text-xs">-</span>
                </td>
                <td class="border border-gray-200 px-3 py-1.5 font-mono text-xs">
                  <span v-if="col.col==='A'" class="text-blue-700">extension</span>
                  <span v-else-if="col.col==='B'" class="text-blue-700">answeredInbound</span>
                  <span v-else-if="col.col==='G'" class="text-blue-700">totalInbound</span>
                  <span v-else-if="col.col==='H'" class="text-blue-700">answeredOutbound</span>
                  <span v-else-if="col.col==='M'" class="text-blue-700">totalOutbound</span>
                  <span v-else-if="col.col==='N'" class="text-amber-600">totalCalls (คำนวณ G+M แทน)</span>
                  <span v-else-if="col.col==='O'" class="text-blue-700">totalTalkDuration</span>
                  <span v-else class="text-gray-300">ไม่บันทึก</span>
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
        <p class="text-xs text-amber-600 mt-2 font-medium">
          <i class="pi pi-exclamation-triangle mr-1"></i>
          ระบบจะใช้ <strong>G+M (รวมสายเข้า + รวมสายออก)</strong> เป็นยอดรวม ไม่ใช้คอลัมน์ N โดยตรง
          เพื่อป้องกันการนับซ้ำ
        </p>
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

            <!-- Preview summary -->
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
            <Column header="ลบ">
              <template #body="sp">
                <Button
                  icon="pi pi-trash"
                  severity="danger"
                  text
                  rounded
                  size="small"
                  @click="confirmDeleteId = sp.data.id"
                  v-tooltip.top="'ลบข้อมูลชุดนี้'"
                />
              </template>
            </Column>
          </DataTable>
        </template>
      </Card>
    </div>

    <!-- Preview Table -->
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
