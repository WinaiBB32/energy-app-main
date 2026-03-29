<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import {
  collection, addDoc, updateDoc, deleteDoc, doc, writeBatch,
  serverTimestamp, query, orderBy, where, getDocs, startAfter, limit, Timestamp,
  type QueryDocumentSnapshot, type QueryConstraint,
} from 'firebase/firestore'
import { db } from '@/firebase/config'
import { toMonthKey, batchUpdateSummary } from '@/utils/monthlySummary'

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
interface SarabanBook {
  id: string
  name: string
  createdAt: Timestamp
}

interface PersonRow {
  personName: string
  receivedCount: number | null
  externalPaperCount: number | null
  externalDigitalCount: number | null
  forwardedCount: number | null
  internalCount: number | null
}

interface FetchedSarabanRecord {
  id: string
  departmentId: string
  recordDate?: Timestamp | null
  personName?: string
  receivedCount?: number
  externalPaperCount?: number
  externalDigitalCount?: number
  forwardedCount?: number
  internalCount?: number
  recordMonth?: Timestamp | null // Legacy
  receiverName?: string // Legacy
  bookName?: string
  digitalCount?: number // Legacy
  paperCount?: number // Legacy
  recordedByName: string
  recordedByUid: string
  createdAt: Timestamp
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
const lastDoc = ref<QueryDocumentSnapshot | null>(null)
const hasMore = ref(true)
const PAGE_SIZE = 20
const sarabanBooks = ref<SarabanBook[]>([])

// --- History Filters ---
const filterDateFrom = ref<Date | null>(null)
const filterDateTo = ref<Date | null>(null)
const filterDeptId = ref('')

// ─── Book Management (superadmin) ─────────────────────────────────────────────
const newBookName = ref('')
const isAddingBook = ref(false)
const bookToDelete = ref<SarabanBook | null>(null)
const deleteBookConfirmVisible = ref(false)

const addBook = async () => {
  const name = newBookName.value.trim()
  if (!name) return
  if (sarabanBooks.value.some(b => b.name === name)) {
    toast.error('มีชื่อเล่มทะเบียนนี้อยู่แล้ว')
    return
  }
  isAddingBook.value = true
  try {
    const newBookRef = await addDoc(collection(db, 'saraban_books'), { name, createdAt: serverTimestamp() })
    // Add to local list without re-fetching
    sarabanBooks.value.push({ id: newBookRef.id, name, createdAt: Timestamp.now() })
    sarabanBooks.value.sort((a, b) => a.name.localeCompare(b.name))
    newBookName.value = ''
    toast.success('เพิ่มเล่มทะเบียนสำเร็จ')
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด')
  } finally {
    isAddingBook.value = false
  }
}

const confirmDeleteBook = (book: SarabanBook) => {
  bookToDelete.value = book
  deleteBookConfirmVisible.value = true
}

const deleteBook = async () => {
  if (!bookToDelete.value) return
  try {
    await deleteDoc(doc(db, 'saraban_books', bookToDelete.value.id))
    sarabanBooks.value = sarabanBooks.value.filter(b => b.id !== bookToDelete.value?.id)
    toast.success('ลบเล่มทะเบียนสำเร็จ')
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด')
  } finally {
    deleteBookConfirmVisible.value = false
    bookToDelete.value = null
  }
}

// ─── Form (Add new) ───────────────────────────────────────────────────────────
const selectedDepartmentId = ref(isSuperAdmin.value ? '' : currentUserDepartment.value)
const selectedDate = ref<Date | null>(new Date())
const selectedBookName = ref('')
const personRows = ref<PersonRow[]>([createEmptyRow()])

function createEmptyRow(): PersonRow {
  return {
    personName: '', receivedCount: null, externalPaperCount: null,
    externalDigitalCount: null, forwardedCount: null, internalCount: null,
  }
}

const addRow = () => personRows.value.push(createEmptyRow())
const removeRow = (index: number) => {
  if (personRows.value.length > 1) personRows.value.splice(index, 1)
}

const isSubmitting = ref(false)
const successMessage = ref('')
const errorMessage = ref('')

// ─── CSV Import ───────────────────────────────────────────────────────────────
const csvFileInput = ref<HTMLInputElement | null>(null)
const triggerCSVImport = () => csvFileInput.value?.click()
const hasThai = (text: string) => /[\u0E00-\u0E7F]/.test(text)
const decodeCsvBuffer = (buffer: ArrayBuffer): string => {
  try {
    const text = new TextDecoder('utf-8', { fatal: true }).decode(buffer).replace(/^\uFEFF/, '')
    if (hasThai(text)) return text
  } catch { }
  try {
    const text = new TextDecoder('windows-874', { fatal: true }).decode(buffer)
    if (hasThai(text)) return text
  } catch { }
  return new TextDecoder('utf-8').decode(buffer).replace(/^\uFEFF/, '')
}

const handleCSVFile = async (event: Event) => {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  if (csvFileInput.value) csvFileInput.value.value = ''
  try {
    const buffer = await file.arrayBuffer()
    const text = decodeCsvBuffer(buffer)
    const lines = text.split(/\r?\n/).filter(line => line.trim())
    if (lines.length < 2) {
      toast.error('ไฟล์ CSV ไม่มีข้อมูล (ต้องมี header + ข้อมูลอย่างน้อย 1 บรรทัด)')
      return
    }
    const dataLines = lines.slice(1)
    const newRows = dataLines.map(line => {
      const cols = line.includes(';') ? line.split(';') : line.split(',')
      const name = cols[0]?.trim().replace(/^"|"$/g, '')
      if (!name) return null
      return {
        personName: name,
        receivedCount: parseInt(cols[1]?.trim() ?? '') || null,
        externalPaperCount: parseInt(cols[2]?.trim() ?? '') || null,
        externalDigitalCount: parseInt(cols[3]?.trim() ?? '') || null,
        forwardedCount: parseInt(cols[4]?.trim() ?? '') || null,
        internalCount: parseInt(cols[5]?.trim() ?? '') || null,
      }
    }).filter(r => r) as PersonRow[]

    if (newRows.length > 0) {
      personRows.value = newRows
      toast.success(`นำเข้าข้อมูล ${newRows.length} รายการสำเร็จ`)
    } else {
      toast.error('ไม่พบข้อมูลในไฟล์ CSV')
    }
  } catch {
    toast.error('ไม่สามารถอ่านไฟล์ได้ กรุณาลองใหม่')
  }
}

// ─── Submit ───────────────────────────────────────────────────────────────────
const submitForm = async (): Promise<void> => {
  successMessage.value = ''; errorMessage.value = ''
  if (!selectedDate.value) { errorMessage.value = 'กรุณาเลือกวันที่บันทึก'; return }
  const saveDeptId = isSuperAdmin.value ? selectedDepartmentId.value : currentUserDepartment.value
  if (!saveDeptId) { errorMessage.value = 'กรุณาเลือกหน่วยงาน'; return }
  const validRows = personRows.value.filter(r => r.personName.trim())
  if (validRows.length === 0) { errorMessage.value = 'กรุณากรอกรายชื่ออย่างน้อย 1 รายการ'; return }

  isSubmitting.value = true
  try {
    const recordedByName = authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ'
    const recordedByUid = authStore.user?.uid || 'unknown'

    const batch = writeBatch(db)
    let totalReceived = 0

    validRows.forEach(row => {
      const newDocRef = doc(collection(db, 'saraban_records'))
      const rCount = row.receivedCount || 0
      totalReceived += rCount

      batch.set(newDocRef, {
        departmentId: saveDeptId,
        recordDate: selectedDate.value ? Timestamp.fromDate(selectedDate.value) : null,
        bookName: selectedBookName.value.trim(),
        personName: row.personName.trim(),
        receivedCount: rCount,
        externalPaperCount: row.externalPaperCount || 0,
        externalDigitalCount: row.externalDigitalCount || 0,
        forwardedCount: row.forwardedCount || 0,
        internalCount: row.internalCount || 0,
        recordedByName, recordedByUid, createdAt: serverTimestamp(),
      })
    })

    // Aggregation
    const monthKey = toMonthKey(selectedDate.value)
    if (monthKey) {
      batchUpdateSummary(batch, monthKey, 'saraban', {
        received: totalReceived,
        count: validRows.length
      })
    }

    await batch.commit()
    successMessage.value = `บันทึกสถิติงานสารบรรณ ${validRows.length} รายการสำเร็จ`
    personRows.value = [createEmptyRow()]
    selectedBookName.value = ''
    handleFilterChange() // Refresh history
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
  departmentId: '', recordDate: null as Date | null, bookName: '', personName: '',
  receivedCount: null as number | null, externalPaperCount: null as number | null,
  externalDigitalCount: null as number | null, forwardedCount: null as number | null,
  internalCount: null as number | null,
})

const openDetail = (record: FetchedSarabanRecord) => { selectedRecord.value = record; detailVisible.value = true }
const openEdit = (record: FetchedSarabanRecord) => {
  selectedRecord.value = record
  const dateTs = record.recordDate ?? record.recordMonth
  editForm.value = {
    departmentId: record.departmentId,
    recordDate: dateTs ? dateTs.toDate() : null,
    bookName: record.bookName ?? '',
    personName: record.personName ?? record.receiverName ?? '',
    receivedCount: record.receivedCount ?? null,
    externalPaperCount: record.externalPaperCount ?? (record.paperCount ?? null),
    externalDigitalCount: record.externalDigitalCount ?? (record.digitalCount ?? null),
    forwardedCount: record.forwardedCount ?? null,
    internalCount: record.internalCount ?? null,
  }
  editVisible.value = true
}

const saveEdit = async (): Promise<void> => {
  if (!selectedRecord.value) return
  if (!editForm.value.recordDate || !editForm.value.personName.trim()) {
    toast.error('กรุณากรอกวันที่และรายชื่อ'); return
  }
  isUpdating.value = true
  try {
    const updatedData = {
      departmentId: editForm.value.departmentId,
      recordDate: editForm.value.recordDate,
      bookName: editForm.value.bookName.trim(),
      personName: editForm.value.personName.trim(),
      receivedCount: editForm.value.receivedCount || 0,
      externalPaperCount: editForm.value.externalPaperCount || 0,
      externalDigitalCount: editForm.value.externalDigitalCount || 0,
      forwardedCount: editForm.value.forwardedCount || 0,
      internalCount: editForm.value.internalCount || 0,
    }
    await updateDoc(doc(db, 'saraban_records', selectedRecord.value.id), updatedData)
    const index = historyRecords.value.findIndex(r => r.id === selectedRecord.value!.id)
    if (index !== -1 && editForm.value.recordDate) {
      const item = historyRecords.value[index]
      if (item) {
        historyRecords.value[index] = {
          ...item,
          ...updatedData,
          recordDate: Timestamp.fromDate(editForm.value.recordDate)
        }
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

const confirmDelete = (record: FetchedSarabanRecord) => {
  recordToDelete.value = record; deleteConfirmVisible.value = true
}
const deleteRecord = async (): Promise<void> => {
  if (!recordToDelete.value) return
  try {
    await deleteDoc(doc(db, 'saraban_records', recordToDelete.value.id))
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
    const sarabanRef = collection(db, 'saraban_records')
    const constraints: QueryConstraint[] = [orderBy('createdAt', 'desc')]

    // Filters
    if (!isAdmin) {
      constraints.push(where('departmentId', '==', currentUserDepartment.value))
    } else if (filterDeptId.value) {
      constraints.push(where('departmentId', '==', filterDeptId.value))
    }
    if (filterDateFrom.value) {
      const from = new Date(filterDateFrom.value); from.setHours(0, 0, 0, 0)
      constraints.push(where('recordDate', '>=', Timestamp.fromDate(from)))
    }
    if (filterDateTo.value) {
      const to = new Date(filterDateTo.value); to.setHours(23, 59, 59, 999)
      constraints.push(where('recordDate', '<=', Timestamp.fromDate(to)))
    }

    // Pagination
    if (loadMore && lastDoc.value) {
      constraints.push(startAfter(lastDoc.value))
    }
    constraints.push(limit(PAGE_SIZE))

    const snap = await getDocs(query(sarabanRef, ...constraints))
    const newRecords = snap.docs.map((d) => ({ id: d.id, ...d.data() } as FetchedSarabanRecord))

    if (loadMore) historyRecords.value.push(...newRecords)
    else historyRecords.value = newRecords

    lastDoc.value = snap.docs[snap.docs.length - 1] || null
    hasMore.value = newRecords.length === PAGE_SIZE
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูลสารบรรณได้')
    hasMore.value = false
  } finally {
    if (loadMore) isLoadingMore.value = false
    else isLoadingHistory.value = false
  }
}

const handleFilterChange = () => {
  lastDoc.value = null
  hasMore.value = true
  historyRecords.value = []
  fetchHistory(false)
}

watch([filterDateFrom, filterDateTo, filterDeptId], handleFilterChange)

onMounted(async () => {
  handleFilterChange() // Initial history fetch
  try {
    const [deptsSnap, booksSnap] = await Promise.all([
      getDocs(query(collection(db, 'departments'), orderBy('name'))),
      getDocs(query(collection(db, 'saraban_books'), orderBy('name'))),
    ])
    departments.value = deptsSnap.docs.map((d) => ({ id: d.id, name: d.data().name as string }))
    sarabanBooks.value = booksSnap.docs.map((d) => ({ id: d.id, ...d.data() } as SarabanBook))
  } catch (e: unknown) {
    toast.fromError(e, 'ไม่สามารถโหลดข้อมูลตั้งต้นได้')
  }
})

// ─── Helpers ──────────────────────────────────────────────────────────────────
const getDeptName = (id: string) => departments.value.find((x) => x.id === id)?.name || id
const formatThaiDate = (ts: Timestamp | null | undefined) =>
  ts ? ts.toDate().toLocaleDateString('th-TH', { year: 'numeric', month: 'long', day: 'numeric' }) : '-'
const formatDateTime = (ts: Timestamp | null | undefined) =>
  ts ? ts.toDate().toLocaleString('th-TH', { dateStyle: 'medium', timeStyle: 'short' }) : '-'

const getRecordDisplayDate = (record: FetchedSarabanRecord) =>
  formatThaiDate(record.recordDate ?? record.recordMonth)
const getRecordDisplayName = (record: FetchedSarabanRecord) =>
  record.personName ?? record.receiverName ?? record.bookName ?? '-'
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10">
    <div class="mb-6 flex flex-col sm:flex-row sm:justify-between sm:items-end gap-4">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          <i class="pi pi-folder-open text-purple-600 mr-2"></i>บันทึกสถิติงานสารบรรณ
        </h2>
        <p class="text-gray-500 mt-1">บันทึกจำนวนเอกสารรับ-ส่งรายบุคคล</p>
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
        <Tab v-if="isSuperAdmin" value="2">
          <i class="pi pi-book mr-2"></i>จัดการเล่มทะเบียน
          <span class="ml-2 bg-purple-100 text-purple-700 px-2 py-0.5 rounded-full text-xs">
            {{ sarabanBooks.length }}
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

                <!-- Header: Date + Department + Book Name -->
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4 p-4 bg-gray-50 rounded-xl border border-gray-200">
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">
                      <i class="pi pi-calendar mr-1 text-purple-500"></i>วันที่บันทึก
                      <span class="text-red-500">*</span>
                    </label>
                    <DatePicker v-model="selectedDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">
                      <i class="pi pi-building mr-1 text-purple-500"></i>หน่วยงาน
                      <span class="text-red-500">*</span>
                    </label>
                    <Select v-if="isSuperAdmin" v-model="selectedDepartmentId" :options="departments" optionLabel="name"
                      optionValue="id" placeholder="-- เลือกหน่วยงาน --" class="w-full" />
                    <InputText v-else :value="getDeptName(currentUserDepartment)" disabled
                      class="w-full bg-gray-100 font-bold" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">
                      <i class="pi pi-book mr-1 text-purple-500"></i>เล่มทะเบียน
                    </label>
                    <Select v-model="selectedBookName" :options="sarabanBooks" optionLabel="name" optionValue="name"
                      placeholder="-- เลือกเล่มทะเบียน --" class="w-full" showClear />
                  </div>
                </div>

                <!-- Person rows table -->
                <div class="overflow-x-auto rounded-xl border border-gray-200">
                  <table class="w-full text-sm">
                    <thead>
                      <tr class="bg-gray-50 border-b border-gray-200">
                        <th class="p-3 text-left font-semibold text-gray-700 min-w-[180px]">รายชื่อ</th>
                        <th class="p-3 text-center font-semibold text-gray-700 min-w-[110px]">
                          <div>จำนวนเอกสาร</div>
                          <div class="text-purple-600">รับเข้า</div>
                        </th>
                        <th class="p-3 text-center font-semibold text-gray-700 min-w-[130px]">
                          <div>ลงรับภายนอก</div>
                          <div class="text-orange-500">(กระดาษ)</div>
                        </th>
                        <th class="p-3 text-center font-semibold text-gray-700 min-w-[130px]">
                          <div>ลงรับภายนอก</div>
                          <div class="text-blue-600">(ดิจิทัล)</div>
                        </th>
                        <th class="p-3 text-center font-semibold text-gray-700 min-w-[110px]">
                          <div>จำนวนเอกสาร</div>
                          <div class="text-emerald-600">ส่งต่อ</div>
                        </th>
                        <th class="p-3 text-center font-semibold text-gray-700 min-w-[110px]">
                          <div>ลงรับ</div>
                          <div class="text-indigo-600">ภายใน</div>
                        </th>
                        <th class="p-3 w-12"></th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="(row, index) in personRows" :key="index"
                        :class="index % 2 === 0 ? 'bg-white' : 'bg-purple-50/20'">
                        <td class="p-2">
                          <InputText v-model="row.personName" placeholder="ชื่อ-นามสกุล" class="w-full" />
                        </td>
                        <td class="p-2">
                          <InputNumber v-model="row.receivedCount" :min="0" placeholder="0" class="w-full"
                            inputClass="text-center" />
                        </td>
                        <td class="p-2">
                          <InputNumber v-model="row.externalPaperCount" :min="0" placeholder="0" class="w-full"
                            inputClass="text-center text-orange-600" />
                        </td>
                        <td class="p-2">
                          <InputNumber v-model="row.externalDigitalCount" :min="0" placeholder="0" class="w-full"
                            inputClass="text-center text-blue-600" />
                        </td>
                        <td class="p-2">
                          <InputNumber v-model="row.forwardedCount" :min="0" placeholder="0" class="w-full"
                            inputClass="text-center text-emerald-600" />
                        </td>
                        <td class="p-2">
                          <InputNumber v-model="row.internalCount" :min="0" placeholder="0" class="w-full"
                            inputClass="text-center text-indigo-600" />
                        </td>
                        <td class="p-2 text-center">
                          <Button icon="pi pi-trash" text rounded severity="danger" size="small"
                            @click="removeRow(index)" :disabled="personRows.length === 1" v-tooltip.top="'ลบแถวนี้'" />
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>

                <!-- Bottom actions -->
                <div
                  class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3 border-t border-gray-100 pt-4">
                  <div class="flex gap-2 flex-wrap">
                    <Button type="button" label="เพิ่มรายชื่อ" icon="pi pi-plus" severity="secondary" outlined
                      @click="addRow" size="small" />
                    <Button type="button" label="นำเข้า CSV" icon="pi pi-file-import" severity="info" outlined
                      @click="triggerCSVImport" size="small" />
                    <input ref="csvFileInput" type="file" accept=".csv" class="hidden" @change="handleCSVFile" />
                  </div>
                  <Button type="submit" label="บันทึกสถิติสารบรรณ" icon="pi pi-save" severity="help"
                    :loading="isSubmitting" class="px-8 py-3 text-base" />
                </div>

                <!-- CSV format hint -->
                <div class="text-xs text-gray-400 bg-gray-50 rounded-lg p-3 border border-dashed border-gray-200">
                  <i class="pi pi-info-circle mr-1 text-gray-400"></i>
                  <strong>รูปแบบ CSV:</strong>
                  รายชื่อ, จำนวนเอกสารรับเข้า, ภายนอก(กระดาษ), ภายนอก(ดิจิทัล), ส่งต่อ, ภายใน
                  &nbsp;—&nbsp; บรรทัดแรกเป็น Header จะถูกข้ามอัตโนมัติ
                </div>
              </form>
            </template>
          </Card>
        </TabPanel>

        <!-- Tab 1: History -->
        <TabPanel value="1">
          <Card class="shadow-sm border-none mt-2 overflow-hidden">
            <template #content>
              <DataTable :value="historyRecords" :loading="isLoadingHistory" paginator :rows="10" stripedRows
                responsiveLayout="scroll" emptyMessage="ยังไม่มีข้อมูล">

                <Column v-if="isAdmin" header="หน่วยงาน" style="min-width: 130px">
                  <template #body="{ data }">
                    <div class="font-bold text-gray-700 text-sm">{{ getDeptName(data.departmentId) }}</div>
                    <div class="text-xs text-gray-400"><i class="pi pi-user mr-1"></i>{{ data.recordedByName }}</div>
                  </template>
                </Column>

                <Column header="วันที่บันทึก" style="min-width: 130px">
                  <template #body="{ data }">
                    <div class="font-semibold text-gray-800 text-sm">{{ getRecordDisplayDate(data) }}</div>
                  </template>
                </Column>

                <Column header="เล่มทะเบียน" style="min-width: 150px">
                  <template #body="{ data }">
                    <div class="text-sm text-gray-600">{{ data.bookName || '-' }}</div>
                  </template>
                </Column>

                <Column header="รายชื่อ" style="min-width: 160px">
                  <template #body="{ data }">
                    <div class="font-semibold text-gray-800">{{ getRecordDisplayName(data) }}</div>
                  </template>
                </Column>

                <Column header="รับเข้า" style="width: 90px">
                  <template #body="{ data }">
                    <div class="text-center font-semibold text-purple-700">
                      {{ data.receivedCount ?? '-' }}
                    </div>
                  </template>
                </Column>

                <Column header="ภายนอก(กระดาษ)" style="width: 120px">
                  <template #body="{ data }">
                    <div class="text-center text-orange-600 font-semibold">
                      {{ data.externalPaperCount ?? (data.paperCount ?? '-') }}
                    </div>
                  </template>
                </Column>

                <Column header="ภายนอก(ดิจิทัล)" style="width: 120px">
                  <template #body="{ data }">
                    <div class="text-center text-blue-600 font-semibold">
                      {{ data.externalDigitalCount ?? (data.digitalCount ?? '-') }}
                    </div>
                  </template>
                </Column>

                <Column header="ส่งต่อ" style="width: 80px">
                  <template #body="{ data }">
                    <div class="text-center text-emerald-600 font-semibold">
                      {{ data.forwardedCount ?? '-' }}
                    </div>
                  </template>
                </Column>

                <Column header="ภายใน" style="width: 80px">
                  <template #body="{ data }">
                    <div class="text-center text-indigo-600 font-semibold">
                      {{ data.internalCount ?? '-' }}
                    </div>
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

        <!-- Tab 2: จัดการเล่มทะเบียน (superadmin only) -->
        <TabPanel v-if="isSuperAdmin" value="2">
          <Card class="shadow-sm border-none mt-2">
            <template #content>
              <div class="flex flex-col gap-5">
                <!-- Add new book -->
                <div class="flex gap-3 items-end p-4 bg-purple-50 rounded-xl border border-purple-100">
                  <div class="flex flex-col gap-2 flex-1">
                    <label class="font-semibold text-sm text-gray-700">
                      <i class="pi pi-book mr-1 text-purple-500"></i>ชื่อเล่มทะเบียนใหม่
                    </label>
                    <InputText v-model="newBookName" placeholder="เช่น ทะเบียนรับหนังสือภายนอก" class="w-full"
                      @keyup.enter="addBook" />
                  </div>
                  <Button label="เพิ่ม" icon="pi pi-plus" severity="help" :loading="isAddingBook"
                    :disabled="!newBookName.trim()" @click="addBook" />
                </div>

                <!-- Book list -->
                <div v-if="sarabanBooks.length === 0" class="text-center text-gray-400 py-10">
                  <i class="pi pi-book text-3xl mb-2 block"></i>
                  ยังไม่มีเล่มทะเบียน กรุณาเพิ่มด้านบน
                </div>
                <div v-else class="flex flex-col gap-2">
                  <div v-for="book in sarabanBooks" :key="book.id"
                    class="flex items-center justify-between px-4 py-3 bg-white border border-gray-100 rounded-lg hover:bg-gray-50 transition-colors">
                    <div class="flex items-center gap-3">
                      <div class="w-8 h-8 bg-purple-100 rounded-lg flex items-center justify-center shrink-0">
                        <i class="pi pi-book text-purple-600 text-sm"></i>
                      </div>
                      <span class="font-medium text-gray-800">{{ book.name }}</span>
                    </div>
                    <Button icon="pi pi-trash" text rounded severity="danger" size="small" v-tooltip.top="'ลบ'"
                      @click="confirmDeleteBook(book)" />
                  </div>
                </div>
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
            <i class="pi pi-user text-purple-600 text-xl"></i>
          </div>
          <div>
            <p class="font-bold text-gray-800 text-lg">{{ getRecordDisplayName(selectedRecord) }}</p>
            <p class="text-sm text-gray-500">{{ getRecordDisplayDate(selectedRecord) }}</p>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3 text-sm">
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">หน่วยงาน</p>
            <p class="font-semibold text-gray-800">{{ getDeptName(selectedRecord.departmentId) }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-xs text-gray-400 mb-1">เล่มทะเบียน</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.bookName || '-' }}</p>
          </div>
          <div class="bg-purple-50 rounded-lg p-3">
            <p class="text-xs text-purple-400 mb-1">จำนวนเอกสารรับเข้า</p>
            <p class="font-bold text-purple-700 text-lg">{{ selectedRecord.receivedCount ?? '-' }}</p>
          </div>
          <div class="bg-orange-50 rounded-lg p-3">
            <p class="text-xs text-orange-400 mb-1">ลงรับภายนอก (กระดาษ)</p>
            <p class="font-bold text-orange-700 text-lg">
              {{ selectedRecord.externalPaperCount ?? (selectedRecord.paperCount ?? '-') }}
            </p>
          </div>
          <div class="bg-blue-50 rounded-lg p-3">
            <p class="text-xs text-blue-400 mb-1">ลงรับภายนอก (ดิจิทัล)</p>
            <p class="font-bold text-blue-700 text-lg">
              {{ selectedRecord.externalDigitalCount ?? (selectedRecord.digitalCount ?? '-') }}
            </p>
          </div>
          <div class="bg-emerald-50 rounded-lg p-3">
            <p class="text-xs text-emerald-400 mb-1">จำนวนเอกสารส่งต่อ</p>
            <p class="font-bold text-emerald-700 text-lg">{{ selectedRecord.forwardedCount ?? '-' }}</p>
          </div>
          <div class="bg-indigo-50 rounded-lg p-3 col-span-2">
            <p class="text-xs text-indigo-400 mb-1">จำนวนเอกสารลงรับภายใน</p>
            <p class="font-black text-indigo-700 text-xl">{{ selectedRecord.internalCount ?? '-' }}</p>
          </div>
        </div>

        <div class="border-t pt-3 text-xs text-gray-400 flex justify-between">
          <span><i class="pi pi-user mr-1"></i>{{ selectedRecord.recordedByName }}</span>
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
            <label class="text-sm font-semibold text-gray-700">วันที่บันทึก <span class="text-red-500">*</span></label>
            <DatePicker v-model="editForm.recordDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">หน่วยงาน</label>
            <Select v-if="isSuperAdmin" v-model="editForm.departmentId" :options="departments" optionLabel="name"
              optionValue="id" class="w-full" />
            <InputText v-else :value="getDeptName(editForm.departmentId)" disabled class="w-full bg-gray-50" />
          </div>
          <div class="flex flex-col gap-2 col-span-2">
            <label class="text-sm font-semibold text-gray-700">เล่มทะเบียน</label>
            <InputText v-model="editForm.bookName" placeholder="เช่น ทะเบียนรับหนังสือภายนอก" class="w-full" />
          </div>
          <div class="flex flex-col gap-2 col-span-2">
            <label class="text-sm font-semibold text-gray-700">รายชื่อ <span class="text-red-500">*</span></label>
            <InputText v-model="editForm.personName" placeholder="ชื่อ-นามสกุล" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">จำนวนเอกสารรับเข้า</label>
            <InputNumber v-model="editForm.receivedCount" :min="0" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ลงรับภายนอก (กระดาษ)</label>
            <InputNumber v-model="editForm.externalPaperCount" :min="0" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ลงรับภายนอก (ดิจิทัล)</label>
            <InputNumber v-model="editForm.externalDigitalCount" :min="0" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">จำนวนเอกสารส่งต่อ</label>
            <InputNumber v-model="editForm.forwardedCount" :min="0" class="w-full" />
          </div>
          <div class="flex flex-col gap-2 col-span-2">
            <label class="text-sm font-semibold text-gray-700">จำนวนเอกสารลงรับภายใน</label>
            <InputNumber v-model="editForm.internalCount" :min="0" class="w-full" />
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
          ต้องการลบข้อมูลของ
          <strong>{{ getRecordDisplayName(recordToDelete!) }}</strong>
          วันที่ {{ getRecordDisplayDate(recordToDelete!) }} หรือไม่?
        </p>
      </div>
      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="deleteConfirmVisible = false" />
        <Button label="ลบข้อมูล" icon="pi pi-trash" severity="danger" @click="deleteRecord" />
      </template>
    </Dialog>

    <!-- Delete Book Confirm Dialog -->
    <Dialog v-model:visible="deleteBookConfirmVisible" modal header="ยืนยันการลบเล่มทะเบียน" :style="{ width: '380px' }"
      :draggable="false">
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 bg-red-100 rounded-full flex items-center justify-center shrink-0">
          <i class="pi pi-exclamation-triangle text-red-500"></i>
        </div>
        <p class="text-gray-700">
          ต้องการลบเล่มทะเบียน <strong>{{ bookToDelete?.name }}</strong> หรือไม่?<br>
          <span class="text-xs text-gray-400">ข้อมูลที่บันทึกไว้แล้วจะไม่ถูกลบ</span>
        </p>
      </div>
      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="deleteBookConfirmVisible = false" />
        <Button label="ลบ" icon="pi pi-trash" severity="danger" @click="deleteBook" />
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
