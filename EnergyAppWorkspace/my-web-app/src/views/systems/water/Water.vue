<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'

defineOptions({ name: 'WaterSystem' })
const toast = useAppToast()
import {
  collection,
  serverTimestamp,
  writeBatch,
  query,
  orderBy,
  where,
  getDocs,
  startAfter,
  limit,
  Timestamp,
  doc,
  updateDoc,
  type QueryDocumentSnapshot,
  type QueryConstraint,
} from 'firebase/firestore'
import { toMonthKey, batchUpdateSummary } from '@/utils/monthlySummary'
// Firebase Removed
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
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

// 1. Interfaces อัปเดตฟิลด์ตามบิลประปาจริง
interface WaterRecord {
  docReceiveNumber: string // เลขที่รับหน่วยงาน
  docNumber: string // เลขที่หนังสือ
  invoiceNumber: string // เลขที่ใบแจ้งค่าน้ำประปา
  billingCycle: Date | null // รอบบิล (เดือน/ปี)

  registrationNo: string // ทะเบียนผู้ใช้น้ำ
  userName: string // ชื่อ
  usageAddress: string // ที่ใช้น้ำ

  readingDate: Date | null // วันที่อ่านน้ำครั้งนี้
  currentMeter: number | null // เลขที่อ่านน้ำครั้งนี้
  waterUnitUsed: number | null // จำนวนหน่วยที่ใช้

  rawWaterCharge: number | null // ค่าน้ำดิบ
  waterAmount: number | null // จำนวนเงินค่าน้ำ
  monthlyServiceFee: number | null // ค่าบริการรายเดือน
  vatAmount: number | null // ภาษีมูลค่าเพิ่ม
}

interface FetchedWaterRecord {
  id: string
  docReceiveNumber: string
  docNumber: string
  invoiceNumber: string
  billingCycle: Timestamp | null
  registrationNo: string
  userName: string
  usageAddress: string
  readingDate: Timestamp | null
  currentMeter: number
  waterUnitUsed: number
  rawWaterCharge: number
  waterAmount: number
  monthlyServiceFee: number
  vatAmount: number
  totalAmount: number // รวมเงิน (บันทึกลง DB ด้วย)
  recordedBy: string
  createdAt: Timestamp
}

const authStore = useAuthStore()
const { isSystemAdmin } = usePermissions()
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const formData = ref<WaterRecord>({
  docReceiveNumber: '',
  docNumber: '',
  invoiceNumber: '',
  billingCycle: null,
  registrationNo: '',
  userName: '',
  usageAddress: '',
  readingDate: null,
  currentMeter: null,
  waterUnitUsed: null,
  rawWaterCharge: null,
  waterAmount: null,
  monthlyServiceFee: null,
  vatAmount: null,
})

const isSubmitting = ref<boolean>(false)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')

const historyRecords = ref<FetchedWaterRecord[]>([])
const isLoadingHistory = ref<boolean>(true)
const isLoadingMore = ref<boolean>(false)
const hasMore = ref<boolean>(false)
const lastDoc = ref<QueryDocumentSnapshot | null>(null)
const PAGE_SIZE = 20

// 2. ระบบคำนวณ "รวมเงิน (ออโต้)"
const computedTotalAmount = computed<number>(() => {
  const raw = formData.value.rawWaterCharge || 0
  const water = formData.value.waterAmount || 0
  const service = formData.value.monthlyServiceFee || 0
  const vat = formData.value.vatAmount || 0
  return raw + water + service + vat
})

// 3. ฟังก์ชันดึงประวัติ (Load-once + Pagination)
const fetchHistory = async (loadMore = false): Promise<void> => {
  if (loadMore) isLoadingMore.value = true
  else { isLoadingHistory.value = true; historyRecords.value = []; lastDoc.value = null }
  try {
    const isAdmin = isSystemAdmin('water')
    const constraints: QueryConstraint[] = [orderBy('createdAt', 'desc'), limit(PAGE_SIZE + 1)]
    if (!isAdmin) constraints.unshift(where('departmentId', '==', currentUserDepartment.value))
    if (loadMore && lastDoc.value) constraints.push(startAfter(lastDoc.value))
    const snap = await getDocs(query(collection(db, 'water_records'), ...constraints))
    const docs = snap.docs.slice(0, PAGE_SIZE)
    hasMore.value = snap.docs.length > PAGE_SIZE
    if (docs.length > 0) lastDoc.value = docs[docs.length - 1] ?? null
    const records = docs.map((d) => ({ id: d.id, ...d.data() } as FetchedWaterRecord))
    if (loadMore) historyRecords.value.push(...records)
    else historyRecords.value = records
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูลน้ำประปาได้')
  } finally {
    isLoadingHistory.value = false
    isLoadingMore.value = false
  }
}

onMounted(() => { fetchHistory() })

// 4. ฟังก์ชันบันทึกข้อมูล
const submitForm = async (): Promise<void> => {
  successMessage.value = ''
  errorMessage.value = ''

  // ตรวจสอบข้อมูลบังคับกรอก
  if (
    !formData.value.billingCycle ||
    !formData.value.registrationNo ||
    formData.value.waterUnitUsed === null
  ) {
    errorMessage.value = 'กรุณากรอกข้อมูลที่จำเป็น (*) ให้ครบถ้วน'
    return
  }

  try {
    isSubmitting.value = true
    const docData = {
      departmentId: currentUserDepartment.value,
      docReceiveNumber: formData.value.docReceiveNumber,
      docNumber: formData.value.docNumber,
      invoiceNumber: formData.value.invoiceNumber,
      billingCycle: formData.value.billingCycle,

      registrationNo: formData.value.registrationNo,
      userName: formData.value.userName,
      usageAddress: formData.value.usageAddress,

      readingDate: formData.value.readingDate,
      currentMeter: formData.value.currentMeter || 0,
      waterUnitUsed: formData.value.waterUnitUsed || 0,

      rawWaterCharge: formData.value.rawWaterCharge || 0,
      waterAmount: formData.value.waterAmount || 0,
      monthlyServiceFee: formData.value.monthlyServiceFee || 0,
      vatAmount: formData.value.vatAmount || 0,

      totalAmount: computedTotalAmount.value, // บันทึกยอดรวมออโต้ลงฐานข้อมูลด้วย

      recordedBy: authStore.user?.uid || 'unknown',
      createdAt: serverTimestamp(),
    }

    const newDocRef = doc(collection(db, 'water_records'))
    const batch = writeBatch(db)
    batch.set(newDocRef, docData)
    const monthKey = toMonthKey(formData.value.billingCycle)
    if (monthKey) {
      batchUpdateSummary(batch, monthKey, 'water', {
        totalAmount: computedTotalAmount.value,
        count: 1,
      })
    }
    await batch.commit()
    successMessage.value = 'บันทึกข้อมูลค่าน้ำประปาสำเร็จ'

    // เคลียร์ฟอร์ม
    formData.value = {
      docReceiveNumber: '',
      docNumber: '',
      invoiceNumber: '',
      billingCycle: null,
      registrationNo: '',
      userName: '',
      usageAddress: '',
      readingDate: null,
      currentMeter: null,
      waterUnitUsed: null,
      rawWaterCharge: null,
      waterAmount: null,
      monthlyServiceFee: null,
      vatAmount: null,
    }
  } catch (error: unknown) {
    if (error instanceof Error) errorMessage.value = `เกิดข้อผิดพลาด: ${error.message}`
    else errorMessage.value = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ'
  } finally {
    isSubmitting.value = false
  }
}

const formatThaiMonth = (ts: Timestamp | null | undefined): string => {
  if (!ts) return '-'
  return ts.toDate().toLocaleDateString('th-TH', { year: 'numeric', month: 'long' })
}
const formatCurrency = (val: number | null | undefined): string => {
  if (val === null || val === undefined) return '-'
  return new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val)
}

// Detail / Edit dialog
const selectedRecord = ref<FetchedWaterRecord | null>(null)
const detailVisible = ref(false)
const editVisible = ref(false)
const isSaving = ref(false)

interface WaterEditForm {
  docReceiveNumber: string
  docNumber: string
  invoiceNumber: string
  billingCycle: Date | null
  registrationNo: string
  userName: string
  usageAddress: string
  readingDate: Date | null
  currentMeter: number | null
  waterUnitUsed: number | null
  rawWaterCharge: number | null
  waterAmount: number | null
  monthlyServiceFee: number | null
  vatAmount: number | null
}

const editForm = ref<WaterEditForm>({
  docReceiveNumber: '', docNumber: '', invoiceNumber: '', billingCycle: null,
  registrationNo: '', userName: '', usageAddress: '', readingDate: null,
  currentMeter: null, waterUnitUsed: null, rawWaterCharge: null,
  waterAmount: null, monthlyServiceFee: null, vatAmount: null,
})

const editTotal = computed(() =>
  (editForm.value.rawWaterCharge || 0) + (editForm.value.waterAmount || 0) +
  (editForm.value.monthlyServiceFee || 0) + (editForm.value.vatAmount || 0)
)

const openDetail = (event: { data: FetchedWaterRecord }) => {
  selectedRecord.value = event.data
  detailVisible.value = true
}

const openEdit = () => {
  if (!selectedRecord.value) return
  const r = selectedRecord.value
  editForm.value = {
    docReceiveNumber: r.docReceiveNumber,
    docNumber: r.docNumber,
    invoiceNumber: r.invoiceNumber,
    billingCycle: r.billingCycle ? r.billingCycle.toDate() : null,
    registrationNo: r.registrationNo,
    userName: r.userName,
    usageAddress: r.usageAddress,
    readingDate: r.readingDate ? r.readingDate.toDate() : null,
    currentMeter: r.currentMeter,
    waterUnitUsed: r.waterUnitUsed,
    rawWaterCharge: r.rawWaterCharge,
    waterAmount: r.waterAmount,
    monthlyServiceFee: r.monthlyServiceFee,
    vatAmount: r.vatAmount,
  }
  detailVisible.value = false
  editVisible.value = true
}

const saveEdit = async () => {
  if (!selectedRecord.value) return
  isSaving.value = true
  try {
    await updateDoc(doc(db, 'water_records', selectedRecord.value.id), {
      docReceiveNumber: editForm.value.docReceiveNumber,
      docNumber: editForm.value.docNumber,
      invoiceNumber: editForm.value.invoiceNumber,
      billingCycle: editForm.value.billingCycle,
      registrationNo: editForm.value.registrationNo,
      userName: editForm.value.userName,
      usageAddress: editForm.value.usageAddress,
      readingDate: editForm.value.readingDate,
      currentMeter: editForm.value.currentMeter || 0,
      waterUnitUsed: editForm.value.waterUnitUsed || 0,
      rawWaterCharge: editForm.value.rawWaterCharge || 0,
      waterAmount: editForm.value.waterAmount || 0,
      monthlyServiceFee: editForm.value.monthlyServiceFee || 0,
      vatAmount: editForm.value.vatAmount || 0,
      totalAmount: editTotal.value,
    })
    editVisible.value = false
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่')
  } finally {
    isSaving.value = false
  }
}

const deleteRecord = async () => {
  if (!selectedRecord.value) return
  if (!confirm('ยืนยันการลบข้อมูลนี้?')) return
  try {
    const record = selectedRecord.value
    const batch = writeBatch(db)
    batch.delete(doc(db, 'water_records', record.id))
    const monthKey = toMonthKey(record.billingCycle)
    if (monthKey) {
      batchUpdateSummary(batch, monthKey, 'water', {
        totalAmount: -(record.totalAmount || 0),
        count: -1,
      })
    }
    await batch.commit()
    editVisible.value = false
    detailVisible.value = false
    selectedRecord.value = null
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่')
  }
}
</script>

<template>
  <div class="max-w-6xl mx-auto pb-10">
    <div class="mb-6">
      <h2 class="text-2xl font-bold text-blue-600">
        <i class="pi pi-tint mr-2"></i>บันทึกค่าน้ำประปา
      </h2>
      <p class="text-gray-500 mt-1">บันทึกรายละเอียดใบแจ้งค่าน้ำประปา (กปภ./กปน.)</p>
    </div>

    <Tabs value="0" lazy>
      <TabList>
        <Tab value="0"><i class="pi pi-file-edit mr-2"></i>บันทึกข้อมูล</Tab>
        <Tab value="1">
          <i class="pi pi-history mr-2"></i>ประวัติย้อนหลัง
          <span v-if="historyRecords.length > 0"
            class="ml-2 bg-blue-100 text-blue-600 px-2 py-0.5 rounded-full text-xs">{{ historyRecords.length }}</span>
        </Tab>
      </TabList>

      <TabPanels>
        <TabPanel value="0">
          <Card class="shadow-sm border-none mt-2">
            <template #content>
              <form @submit.prevent="submitForm" class="flex flex-col gap-8">
                <Message v-if="successMessage" severity="success" :closable="true">{{
                  successMessage
                }}</Message>
                <Message v-if="errorMessage" severity="error" :closable="true">{{
                  errorMessage
                }}</Message>

                <div>
                  <h3 class="font-bold text-gray-700 border-b pb-2 mb-4 text-lg">
                    <i class="pi pi-file mr-2 text-blue-500"></i>ข้อมูลเอกสาร
                  </h3>
                  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">เลขที่รับหน่วยงาน</label>
                      <InputText v-model="formData.docReceiveNumber" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">เลขที่หนังสือ</label>
                      <InputText v-model="formData.docNumber" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">เลขที่ใบแจ้งค่าน้ำ</label>
                      <InputText v-model="formData.invoiceNumber" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">รอบบิล (เดือน/ปี) <span
                          class="text-red-500">*</span></label>
                      <DatePicker v-model="formData.billingCycle" view="month" dateFormat="MM yy" class="w-full" />
                    </div>
                  </div>
                </div>

                <div>
                  <h3 class="font-bold text-gray-700 border-b pb-2 mb-4 text-lg">
                    <i class="pi pi-user mr-2 text-blue-500"></i>ข้อมูลผู้ใช้น้ำ
                  </h3>
                  <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">ทะเบียนผู้ใช้น้ำ <span
                          class="text-red-500">*</span></label>
                      <InputText v-model="formData.registrationNo" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">ชื่อ</label>
                      <InputText v-model="formData.userName" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">ที่ใช้น้ำ</label>
                      <InputText v-model="formData.usageAddress" class="w-full" />
                    </div>
                  </div>
                </div>

                <div>
                  <h3 class="font-bold text-gray-700 border-b pb-2 mb-4 text-lg">
                    <i class="pi pi-search mr-2 text-blue-500"></i>ข้อมูลการอ่านน้ำ
                  </h3>
                  <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">วันที่อ่านน้ำครั้งนี้</label>
                      <DatePicker v-model="formData.readingDate" dateFormat="dd/mm/yy" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">เลขที่อ่านน้ำครั้งนี้</label>
                      <InputNumber v-model="formData.currentMeter" :minFractionDigits="0" :maxFractionDigits="2"
                        class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">จำนวนหน่วยที่ใช้ (ลบ.ม.) <span
                          class="text-red-500">*</span></label>
                      <InputNumber v-model="formData.waterUnitUsed" :minFractionDigits="0" :maxFractionDigits="2"
                        class="w-full" />
                    </div>
                  </div>
                </div>

                <div class="bg-blue-50/50 p-4 rounded-lg border border-blue-100">
                  <h3 class="font-bold text-blue-800 border-b border-blue-200 pb-2 mb-4 text-lg">
                    <i class="pi pi-calculator mr-2"></i>สรุปค่าใช้จ่าย
                  </h3>
                  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">ค่าน้ำดิบ</label>
                      <InputNumber v-model="formData.rawWaterCharge" mode="currency" currency="THB" locale="th-TH"
                        class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">จำนวนเงินค่าน้ำ</label>
                      <InputNumber v-model="formData.waterAmount" mode="currency" currency="THB" locale="th-TH"
                        class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">ค่าบริการรายเดือน</label>
                      <InputNumber v-model="formData.monthlyServiceFee" mode="currency" currency="THB" locale="th-TH"
                        class="w-full" />
                    </div>
                    <div class="flex flex-col gap-2">
                      <label class="font-semibold text-sm text-gray-700">ภาษีมูลค่าเพิ่ม (VAT)</label>
                      <InputNumber v-model="formData.vatAmount" mode="currency" currency="THB" locale="th-TH"
                        class="w-full" />
                    </div>

                    <div class="flex flex-col gap-2 bg-blue-100 p-2 rounded-md">
                      <label class="font-bold text-sm text-blue-800">รวมเงิน (ออโต้)</label>
                      <InputNumber :modelValue="computedTotalAmount" mode="currency" currency="THB" locale="th-TH"
                        readonly class="w-full font-bold text-xl text-blue-700"
                        inputClass="bg-transparent border-none text-right font-bold text-blue-700" />
                    </div>
                  </div>
                </div>

                <div class="flex justify-end mt-2 pt-4">
                  <Button type="submit" label="บันทึกค่าน้ำประปา" icon="pi pi-save" :loading="isSubmitting"
                    class="px-8 py-3 text-lg" />
                </div>
              </form>
            </template>
          </Card>
        </TabPanel>

        <TabPanel value="1">
          <Card class="shadow-sm border-none mt-2 overflow-hidden">
            <template #content>
              <DataTable :value="historyRecords" :loading="isLoadingHistory" paginator :rows="10" stripedRows
                responsiveLayout="scroll" emptyMessage="ยังไม่มีข้อมูล" selectionMode="single" @row-click="openDetail"
                class="cursor-pointer">
                <Column header="ทะเบียนผู้ใช้น้ำ">
                  <template #body="sp">
                    <span class="font-semibold">{{ sp.data.registrationNo }}</span>
                    <div class="text-xs text-gray-500">{{ sp.data.userName }}</div>
                  </template>
                </Column>
                <Column header="รอบบิล">
                  <template #body="sp">{{ formatThaiMonth(sp.data.billingCycle) }}</template>
                </Column>
                <Column header="รายละเอียดมิเตอร์">
                  <template #body="sp">
                    <div class="text-gray-600 text-sm">เลขล่าสุด: <span class="font-bold">{{ sp.data.currentMeter
                    }}</span></div>
                    <div class="text-xs text-blue-500 mt-1">ใช้ไป: {{ sp.data.waterUnitUsed }} ลบ.ม.</div>
                  </template>
                </Column>
                <Column header="รวมเงิน">
                  <template #body="sp">
                    <span class="font-bold text-blue-600 text-lg">{{ formatCurrency(sp.data.totalAmount) }}</span>
                  </template>
                </Column>
                <Column header="" style="width: 3rem">
                  <template #body><i class="pi pi-chevron-right text-gray-400"></i></template>
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

          <!-- Detail Dialog -->
          <Dialog v-model:visible="detailVisible" modal header="รายละเอียดค่าน้ำประปา" :style="{ width: '560px' }"
            :draggable="false">
            <div v-if="selectedRecord" class="flex flex-col gap-4">
              <div class="grid grid-cols-2 gap-3 text-sm">
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">ทะเบียนผู้ใช้น้ำ</p>
                  <p class="font-semibold text-gray-800">{{ selectedRecord.registrationNo }}</p>
                </div>
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">รอบบิล</p>
                  <p class="font-semibold text-gray-800">{{ formatThaiMonth(selectedRecord.billingCycle) }}</p>
                </div>
                <div v-if="selectedRecord.userName" class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">ชื่อ</p>
                  <p class="font-semibold text-gray-800">{{ selectedRecord.userName }}</p>
                </div>
                <div v-if="selectedRecord.usageAddress" class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">ที่ใช้น้ำ</p>
                  <p class="font-semibold text-gray-800">{{ selectedRecord.usageAddress }}</p>
                </div>
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">เลขมิเตอร์</p>
                  <p class="font-semibold text-gray-800">{{ selectedRecord.currentMeter }}</p>
                </div>
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">หน่วยที่ใช้</p>
                  <p class="font-semibold text-gray-800">{{ selectedRecord.waterUnitUsed }} ลบ.ม.</p>
                </div>
              </div>
              <div class="bg-blue-50 rounded-xl p-4 border border-blue-100">
                <p class="font-bold text-blue-800 mb-3"><i class="pi pi-calculator mr-2"></i>สรุปค่าใช้จ่าย</p>
                <div class="flex flex-col gap-2 text-sm">
                  <div class="flex justify-between">
                    <span class="text-gray-600">ค่าน้ำดิบ</span>
                    <span>{{ formatCurrency(selectedRecord.rawWaterCharge) }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-600">จำนวนเงินค่าน้ำ</span>
                    <span>{{ formatCurrency(selectedRecord.waterAmount) }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-600">ค่าบริการรายเดือน</span>
                    <span>{{ formatCurrency(selectedRecord.monthlyServiceFee) }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-600">VAT</span>
                    <span>{{ formatCurrency(selectedRecord.vatAmount) }}</span>
                  </div>
                  <div class="flex justify-between border-t border-blue-200 pt-2 mt-1">
                    <span class="font-bold text-blue-800">รวมเงิน</span>
                    <span class="font-bold text-blue-700 text-lg">{{ formatCurrency(selectedRecord.totalAmount)
                    }}</span>
                  </div>
                </div>
              </div>
            </div>
            <template #footer>
              <Button label="ปิด" severity="secondary" text @click="detailVisible = false" />
              <Button label="แก้ไข" icon="pi pi-pencil" severity="warning" @click="openEdit" />
            </template>
          </Dialog>

          <!-- Edit Dialog -->
          <Dialog v-model:visible="editVisible" modal header="แก้ไขค่าน้ำประปา" :style="{ width: '640px' }"
            :draggable="false">
            <div class="flex flex-col gap-4">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">เลขที่รับหน่วยงาน</label>
                  <InputText v-model="editForm.docReceiveNumber" class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">เลขที่หนังสือ</label>
                  <InputText v-model="editForm.docNumber" class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">เลขที่ใบแจ้งค่าน้ำ</label>
                  <InputText v-model="editForm.invoiceNumber" class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">รอบบิล</label>
                  <DatePicker v-model="editForm.billingCycle" view="month" dateFormat="MM yy" class="w-full" showIcon />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">ทะเบียนผู้ใช้น้ำ</label>
                  <InputText v-model="editForm.registrationNo" class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">ชื่อ</label>
                  <InputText v-model="editForm.userName" class="w-full" />
                </div>
                <div class="flex flex-col gap-2 sm:col-span-2">
                  <label class="text-sm font-semibold text-gray-700">ที่ใช้น้ำ</label>
                  <InputText v-model="editForm.usageAddress" class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">เลขมิเตอร์</label>
                  <InputNumber v-model="editForm.currentMeter" :minFractionDigits="0" :maxFractionDigits="2"
                    class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">หน่วยที่ใช้ (ลบ.ม.)</label>
                  <InputNumber v-model="editForm.waterUnitUsed" :minFractionDigits="0" :maxFractionDigits="2"
                    class="w-full" />
                </div>
              </div>
              <div class="bg-blue-50/50 p-4 rounded-xl border border-blue-100">
                <p class="font-bold text-blue-800 text-sm mb-3"><i class="pi pi-calculator mr-2"></i>ค่าใช้จ่าย</p>
                <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
                  <div class="flex flex-col gap-2">
                    <label class="text-xs font-semibold text-gray-700">ค่าน้ำดิบ</label>
                    <InputNumber v-model="editForm.rawWaterCharge" mode="currency" currency="THB" locale="th-TH"
                      class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="text-xs font-semibold text-gray-700">จำนวนเงินค่าน้ำ</label>
                    <InputNumber v-model="editForm.waterAmount" mode="currency" currency="THB" locale="th-TH"
                      class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="text-xs font-semibold text-gray-700">ค่าบริการรายเดือน</label>
                    <InputNumber v-model="editForm.monthlyServiceFee" mode="currency" currency="THB" locale="th-TH"
                      class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="text-xs font-semibold text-gray-700">VAT</label>
                    <InputNumber v-model="editForm.vatAmount" mode="currency" currency="THB" locale="th-TH"
                      class="w-full" />
                  </div>
                </div>
                <div class="flex justify-end mt-3 pt-3 border-t border-blue-200">
                  <span class="font-bold text-blue-700">รวมเงิน: {{ formatCurrency(editTotal) }}</span>
                </div>
              </div>
            </div>
            <template #footer>
              <Button label="ลบ" icon="pi pi-trash" severity="danger" text @click="deleteRecord" />
              <Button label="ยกเลิก" severity="secondary" text @click="editVisible = false" />
              <Button label="บันทึก" icon="pi pi-save" severity="success" :loading="isSaving" @click="saveEdit" />
            </template>
          </Dialog>
        </TabPanel>
      </TabPanels>
    </Tabs>
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

/* ปรับแต่งช่องรวมเงินให้ดูโดดเด่นและพิมพ์ไม่ได้ */
:deep(.bg-transparent) {
  background-color: transparent !important;
  cursor: default;
}
</style>
