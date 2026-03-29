<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import {
  collection,
  query,
  orderBy,
  where,
  getDocs,
  limit,
  startAfter,
  Timestamp,
  doc,
  updateDoc,
  deleteDoc,
  writeBatch,
  type QueryConstraint,
  type QueryDocumentSnapshot
} from 'firebase/firestore'
// Firebase Removed
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'

import Card from 'primevue/card'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import Select from 'primevue/select'
import Button from 'primevue/button'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Textarea from 'primevue/textarea'
import Dialog from 'primevue/dialog'

interface FetchedFuelRecord {
  id: string
  departmentId: string
  refuelDate: Timestamp | null
  documentType: string
  documentNumber: string
  vehiclePlate: string
  vehicleProvince: string
  purchaserName: string
  fuelType: string
  liters: number | null
  totalAmount: number | null
  gasStationCompany: string
  note: string
  recordedByName: string
  recordedByUid: string
  createdAt: Timestamp
}

interface Department { id: string; name: string }
interface FuelType { id: string; name: string; severity: string }

const authStore = useAuthStore()
const toast = useAppToast()
const currentUserRole = computed(() => authStore.userProfile?.role || 'user')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const historyRecords = ref<FetchedFuelRecord[]>([])
const departments = ref<Department[]>([])
const fuelTypes = ref<FuelType[]>([])
const isLoading = ref(true)

// ─── Filters ───────────────────────────────────────────────────────────────
const filterDateFrom = ref<Date | null>(null)
const filterDateTo = ref<Date | null>(null)
const filterDeptId = ref('')
const filterFuelType = ref('')

// Summary totals should operate on the currently loaded records
const totalAmount = computed(() => historyRecords.value.reduce((s, r) => s + (r.totalAmount ?? 0), 0))
const totalLiters = computed(() => historyRecords.value.reduce((s, r) => s + (r.liters ?? 0), 0))

const clearFilters = () => {
  filterDateFrom.value = null
  filterDateTo.value = null
  filterDeptId.value = ''
  filterFuelType.value = ''
  // Watcher will trigger refetch
}

// ─── Firestore Pagination & Query ──────────────────────────────────────────
const PAGE_SIZE = 20
const lastVisible = ref<QueryDocumentSnapshot | null>(null)
const hasMore = ref(true)

const fetchRecords = async (loadMore = false) => {
  if (!hasMore.value && loadMore) return
  isLoading.value = true

  try {
    const fuelRef = collection(db, 'fuel_records')
    const constraints: QueryConstraint[] = [orderBy('createdAt', 'desc')]

    // Apply filters
    if (currentUserRole.value !== 'superadmin') {
      constraints.push(where('departmentId', '==', currentUserDepartment.value))
    } else if (filterDeptId.value) {
      constraints.push(where('departmentId', '==', filterDeptId.value))
    }
    if (filterFuelType.value) {
      constraints.push(where('fuelType', '==', filterFuelType.value))
    }
    if (filterDateFrom.value) {
      const from = new Date(filterDateFrom.value); from.setHours(0, 0, 0, 0)
      constraints.push(where('refuelDate', '>=', Timestamp.fromDate(from)))
    }
    if (filterDateTo.value) {
      const to = new Date(filterDateTo.value); to.setHours(23, 59, 59, 999)
      constraints.push(where('refuelDate', '<=', Timestamp.fromDate(to)))
    }

    // Apply pagination
    if (loadMore && lastVisible.value) {
      constraints.push(startAfter(lastVisible.value))
    }
    constraints.push(limit(PAGE_SIZE))

    const q = query(fuelRef, ...constraints)
    const recordsSnap = await getDocs(q)
    
    const newRecords = recordsSnap.docs.map((d) => ({ id: d.id, ...d.data() } as FetchedFuelRecord))

    if (loadMore) {
      historyRecords.value.push(...newRecords)
    } else {
      historyRecords.value = newRecords
    }

    lastVisible.value = recordsSnap.docs[recordsSnap.docs.length - 1] || null
    hasMore.value = newRecords.length === PAGE_SIZE

  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดประวัติการเติมน้ำมันได้')
    hasMore.value = false // Stop trying on error
  } finally {
    isLoading.value = false
  }
}

const handleFilterChange = () => {
  lastVisible.value = null
  hasMore.value = true
  historyRecords.value = []
  fetchRecords(false)
}

watch([filterDateFrom, filterDateTo, filterDeptId, filterFuelType], handleFilterChange)

onMounted(async () => {
  // Initial fetch
  handleFilterChange()

  // Fetch static data
  try {
    const [deptsSnap, fuelTypesSnap] = await Promise.all([
      getDocs(query(collection(db, 'departments'), orderBy('name'))),
      getDocs(query(collection(db, 'fuel_types'), orderBy('createdAt', 'asc'))),
    ])
    departments.value = deptsSnap.docs.map((d) => ({ id: d.id, name: d.data().name as string }))
    fuelTypes.value = fuelTypesSnap.docs.map((d) => ({
      id: d.id, name: d.data().name as string, severity: (d.data().severity as string) || 'secondary',
    }))
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูลหน่วยงาน/ประเภทน้ำมันได้')
  }
})

// ─── Helpers ───────────────────────────────────────────────────────────────
const getDeptName = (id: string) => departments.value.find((x) => x.id === id)?.name || id
const getFuelTypeName = (id: string) => fuelTypes.value.find((x) => x.id === id)?.name || id
const getFuelTagSeverity = (id: string) => fuelTypes.value.find((x) => x.id === id)?.severity || 'secondary'
const formatThaiDate = (ts: Timestamp | null | undefined) =>
  ts ? ts.toDate().toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' }) : '-'
const formatCurrency = (val: number | null | undefined) =>
  val != null ? new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val) : '-'

// ─── Detail / Edit ──────────────────────────────────────────────────────────
const selectedRecord = ref<FetchedFuelRecord | null>(null)
const detailVisible = ref(false)
const editVisible = ref(false)
const isSaving = ref(false)

const provinces = [
  'กรุงเทพมหานคร', 'นนทบุรี', 'ปทุมธานี', 'สมุทรปราการ',
  'เชียงใหม่', 'ขอนแก่น', 'นครราชสีมา', 'อุดรธานี',
]
const documentTypes = ['ใบสั่งซื้อ', 'ใบเสร็จรับเงิน', 'ใบกำกับภาษี']

interface EditForm {
  departmentId: string; refuelDate: Date | null; documentType: string; documentNumber: string
  vehiclePlate: string; vehicleProvince: string; purchaserName: string; fuelType: string
  liters: number | null; totalAmount: number | null; gasStationCompany: string; note: string
}

const editForm = ref<EditForm>({
  departmentId: '', refuelDate: null, documentType: '', documentNumber: '',
  vehiclePlate: '', vehicleProvince: '', purchaserName: '', fuelType: '',
  liters: null, totalAmount: null, gasStationCompany: '', note: '',
})

const editPricePerLiter = computed(() =>
  editForm.value.liters && editForm.value.totalAmount && editForm.value.liters > 0
    ? editForm.value.totalAmount / editForm.value.liters : 0
)

const openDetail = (event: { data: FetchedFuelRecord }) => {
  selectedRecord.value = event.data
  detailVisible.value = true
}

const openEdit = () => {
  if (!selectedRecord.value) return
  const r = selectedRecord.value
  editForm.value = {
    departmentId: r.departmentId, refuelDate: r.refuelDate ? r.refuelDate.toDate() : null,
    documentType: r.documentType, documentNumber: r.documentNumber,
    vehiclePlate: r.vehiclePlate, vehicleProvince: r.vehicleProvince,
    purchaserName: r.purchaserName, fuelType: r.fuelType,
    liters: r.liters, totalAmount: r.totalAmount,
    gasStationCompany: r.gasStationCompany, note: r.note,
  }
  detailVisible.value = false
  editVisible.value = true
}

const saveEdit = async () => {
  if (!selectedRecord.value) return
  isSaving.value = true
  try {
    const originalRecord = historyRecords.value.find(r => r.id === selectedRecord.value!.id)
    if (!originalRecord) throw new Error('Original record not found')

    const newValues = { ...editForm.value, refuelDate: editForm.value.refuelDate ? Timestamp.fromDate(editForm.value.refuelDate) : null }
    await updateDoc(doc(db, 'fuel_records', selectedRecord.value.id), newValues)
    
    // Update local record to avoid re-fetch
    Object.assign(originalRecord, newValues, { id: selectedRecord.value.id })

    toast.success('บันทึกข้อมูลสำเร็จ')
    editVisible.value = false
  } catch (e) { toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่') }
  finally { isSaving.value = false }
}

const deleteRecord = async () => {
  if (!selectedRecord.value) return
  if (!confirm('ยืนยันการลบข้อมูลนี้?')) return
  isSaving.value = true
  try {
    await deleteDoc(doc(db, 'fuel_records', selectedRecord.value.id))
    historyRecords.value = historyRecords.value.filter(r => r.id !== selectedRecord.value!.id)
    toast.success('ลบข้อมูลสำเร็จ')
    editVisible.value = false
    detailVisible.value = false
    selectedRecord.value = null
  } catch(e) {
    toast.fromError(e, 'ไม่สามารถลบข้อมูลได้')
  } finally {
    isSaving.value = false
  }
}

// ─── CSV Import ────────────────────────────────────────────────────────────
const fileInput = ref<HTMLInputElement | null>(null)

const downloadTemplate = () => {
  const BOM = "\uFEFF"
  const headers = "ชื่อหน่วยงาน,วันที่เติม(YYYY-MM-DD),ประเภทเอกสาร,เลขที่เอกสาร,ทะเบียนรถ,จังหวัด,ชื่อผู้จัดซื้อ,ประเภทน้ำมัน,ปริมาณ(ลิตร),ยอดเงินรวม(บาท),บริษัทปั๊มน้ำมัน,หมายเหตุ\n"
  const row = "ไอที,2026-12-31,ใบเสร็จรับเงิน,REC-001,กข-1234,กรุงเทพมหานคร,นายสมชาย ทดสอบ,ดีเซล,40,1200,ปตท,ทดสอบระบบนำเข้า\n"
  const blob = new Blob([BOM + headers + row], { type: 'text/csv;charset=utf-8;' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = "template_fuel_import.csv"
  link.click()
  URL.revokeObjectURL(link.href)
}

const triggerFileInput = () => {
  if (fileInput.value) fileInput.value.click()
}

const handleFileUpload = (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return
  isSaving.value = true

  const reader = new FileReader()
  reader.onload = async (e) => {
    try {
      const text = e.target?.result as string
      const lines = text.split('\n').filter(r => r.trim().length > 0)
      if (lines.length < 2) throw new Error('ไม่พบข้อมูล หรือไฟล์ว่างเปล่า (ต้องมีหัว Column)')

      const batch = writeBatch(db)
      const collRef = collection(db, 'fuel_records')
      let importedCount = 0

      for (let i = 1; i < lines.length; i++) {
        const rowString = (lines[i] || '').trim()
        if(!rowString) continue
        
        const cells = rowString.split(/,(?=(?:(?:[^"]*"){2})*[^"]*$)/).map(v => (v || '').replace(/^"|"$/g, '').trim())
        if (cells.length < 10) continue

        const [deptNameStr, dateStr, docType, docNum, plate, prov, purch, fuelNameStr, litersStr, amtStr, gasco, noteStr] = cells

        const matchedDept = departments.value.find(d => d.name === deptNameStr)?.id || 'DEP-UNKNOWN'
        const matchedFuelType = fuelTypes.value.find(f => f.name === fuelNameStr)?.id || fuelNameStr

        let refuelTS = null
        if (dateStr) {
          const dObj = new Date(dateStr)
          if (!isNaN(dObj.getTime())) {
            refuelTS = Timestamp.fromDate(dObj)
          }
        }

        const newRec = {
          departmentId: matchedDept,
          refuelDate: refuelTS,
          documentType: docType || '-',
          documentNumber: docNum || '-',
          vehiclePlate: plate || '-',
          vehicleProvince: prov || '-',
          purchaserName: purch || '',
          fuelType: matchedFuelType,
          liters: parseFloat(litersStr || '0') || 0,
          totalAmount: parseFloat(amtStr || '0') || 0,
          gasStationCompany: gasco || '',
          note: noteStr || '',
          recordedByName: authStore.userProfile?.displayName || 'System Batch Import',
          recordedByUid: 'batch-import-' + Date.now(),
          createdAt: Timestamp.now()
        }

        batch.set(doc(collRef), newRec)
        importedCount++
      }

      if (importedCount > 0) {
        await batch.commit()
        toast.success(`นำเข้าสำเร็จ ${importedCount} รายการ`, 'กรุณารีเฟรชหน้าเพื่อดูข้อมูลที่นำเข้า')
        handleFilterChange() // Refetch data
      } else {
        toast.warn('ไม่พบแถวข้อมูลที่สามารถนำเข้าได้', 'ฟอร์แมตอาจจะไม่ถูกต้อง')
      }

    } catch (err: unknown) {
      toast.fromError(err, 'เกิดข้อผิดพลาดในการนำเข้าไฟล์')
    } finally {
      isSaving.value = false
      if (fileInput.value) fileInput.value.value = ''
    }
  }
  reader.readAsText(file, 'utf-8')
}
</script>

<template>
  <div class="max-w-6xl mx-auto pb-10">
    <div class="mb-6 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          <i class="pi pi-history text-red-500 mr-2"></i>ประวัติการเติมน้ำมัน
        </h2>
        <p class="text-gray-500 mt-1">ค้นหาและจัดการข้อมูลการเติมน้ำมันทั้งหมด</p>
      </div>
      <div v-if="currentUserRole === 'superadmin'" class="flex gap-2">
        <input type="file" ref="fileInput" accept=".csv" class="hidden" @change="handleFileUpload" />
        <Button label="โหลด Template" icon="pi pi-download" severity="secondary" outlined @click="downloadTemplate" />
        <Button label="นำเข้า CSV" icon="pi pi-upload" severity="info" :loading="isSaving" @click="triggerFileInput" />
      </div>
    </div>

    <Card class="shadow-sm border-none mb-4">
      <template #content>
        <!-- Filters -->
        <div class="mb-4">
          <h3 class="font-bold text-gray-700 border-b pb-2 mb-4">
            <i class="pi pi-filter mr-2 text-red-500"></i>ตัวกรองข้อมูล
          </h3>
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 items-end">
            <div class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">วันที่เริ่มต้น</label>
              <DatePicker v-model="filterDateFrom" dateFormat="dd/mm/yy" showIcon class="w-full" />
            </div>
            <div class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">วันที่สิ้นสุด</label>
              <DatePicker v-model="filterDateTo" dateFormat="dd/mm/yy" showIcon class="w-full" />
            </div>
            <div v-if="currentUserRole === 'superadmin'" class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">หน่วยงาน</label>
              <Select v-model="filterDeptId"
                :options="[{ id: '', name: 'ทั้งหมด' }, ...departments]"
                optionLabel="name" optionValue="id" class="w-full" />
            </div>
            <div class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">ประเภทน้ำมัน</label>
              <Select v-model="filterFuelType"
                :options="[{ id: '', name: 'ทั้งหมด' }, ...fuelTypes]"
                optionLabel="name" optionValue="id" class="w-full" />
            </div>
            <div class="flex items-end">
              <Button label="ล้างตัวกรอง" icon="pi pi-times" severity="secondary" text @click="clearFilters" />
            </div>
          </div>
        </div>

        <!-- Summary -->
        <div class="bg-red-50 border border-red-100 rounded-xl p-4 flex flex-wrap gap-6 items-center mb-4">
          <div>
            <p class="text-xs text-gray-500">รายการที่แสดง</p>
            <p class="font-bold text-xl text-gray-800">{{ historyRecords.length }} รายการ</p>
          </div>
          <div>
            <p class="text-xs text-gray-500">รวมวงเงิน</p>
            <p class="font-bold text-xl text-red-600">{{ formatCurrency(totalAmount) }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500">รวมลิตร</p>
            <p class="font-bold text-xl text-gray-800">{{ totalLiters.toFixed(3) }} ลิตร</p>
          </div>
        </div>

        <!-- Table -->
        <DataTable
          :value="historyRecords"
          :loading="isLoading"
          stripedRows
          responsiveLayout="scroll"
          emptyMessage="ไม่พบข้อมูล"
          selectionMode="single"
          @row-click="openDetail"
          class="cursor-pointer"
        >
          <Column v-if="currentUserRole !== 'user'" header="หน่วยงาน">
            <template #body="sp">
              <div class="font-bold text-gray-700">{{ getDeptName(sp.data.departmentId) }}</div>
              <div class="text-xs text-gray-500"><i class="pi pi-user mr-1"></i>{{ sp.data.recordedByName }}</div>
            </template>
          </Column>
          <Column header="วันที่ / ทะเบียน">
            <template #body="sp">
              <div class="font-semibold text-gray-800">
                {{ sp.data.vehiclePlate }}
                <span class="text-xs font-normal text-gray-500">({{ sp.data.vehicleProvince }})</span>
              </div>
              <div class="text-xs text-gray-500 mt-1">
                <i class="pi pi-calendar mr-1"></i>{{ formatThaiDate(sp.data.refuelDate) }}
              </div>
            </template>
          </Column>
          <Column header="ประเภทน้ำมัน / ปริมาณ">
            <template #body="sp">
              <Tag :value="getFuelTypeName(sp.data.fuelType)" :severity="getFuelTagSeverity(sp.data.fuelType)" rounded class="mb-1 text-xs" />
              <div class="text-sm text-gray-600 font-medium">{{ sp.data.liters }} ลิตร</div>
            </template>
          </Column>
          <Column header="วงเงิน">
            <template #body="sp">
              <div class="font-bold text-red-600 text-lg">{{ formatCurrency(sp.data.totalAmount) }}</div>
              <div class="text-xs text-gray-400">{{ sp.data.documentType }}</div>
            </template>
          </Column>
          <Column header="" style="width:3rem">
            <template #body><i class="pi pi-chevron-right text-gray-400"></i></template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <div v-if="hasMore" class="mt-4 text-center">
      <Button
        label="โหลดเพิ่ม..."
        icon="pi pi-arrow-down"
        severity="secondary"
        outlined
        :loading="isLoading"
        @click="fetchRecords(true)"
      />
    </div>

    <!-- Detail Dialog -->
    <Dialog v-model:visible="detailVisible" modal header="รายละเอียดการเติมน้ำมัน"
      :style="{ width: '560px' }" :draggable="false">
      <div v-if="selectedRecord" class="flex flex-col gap-4">
        <div class="grid grid-cols-2 gap-3 text-sm">
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-gray-500 text-xs mb-1">หน่วยงาน</p>
            <p class="font-semibold text-gray-800">{{ getDeptName(selectedRecord.departmentId) }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-gray-500 text-xs mb-1">วันที่</p>
            <p class="font-semibold text-gray-800">{{ formatThaiDate(selectedRecord.refuelDate) }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-gray-500 text-xs mb-1">ทะเบียนรถ</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.vehiclePlate }}
              <span class="text-xs font-normal text-gray-500">({{ selectedRecord.vehicleProvince }})</span></p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-gray-500 text-xs mb-1">ผู้จัดซื้อ</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.purchaserName || '-' }}</p>
          </div>
          <div v-if="selectedRecord.documentNumber" class="bg-gray-50 rounded-lg p-3">
            <p class="text-gray-500 text-xs mb-1">เลขที่เอกสาร ({{ selectedRecord.documentType }})</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.documentNumber }}</p>
          </div>
          <div v-if="selectedRecord.gasStationCompany" class="bg-gray-50 rounded-lg p-3">
            <p class="text-gray-500 text-xs mb-1">บริษัทจำหน่ายน้ำมัน</p>
            <p class="font-semibold text-gray-800">{{ selectedRecord.gasStationCompany }}</p>
          </div>
        </div>
        <div class="bg-red-50 rounded-xl p-4 border border-red-100">
          <p class="font-bold text-red-800 mb-3"><i class="pi pi-gauge mr-2"></i>รายละเอียดการเติมน้ำมัน</p>
          <div class="flex flex-col gap-2 text-sm">
            <div class="flex justify-between">
              <span class="text-gray-600">ประเภทน้ำมัน</span>
              <Tag :value="getFuelTypeName(selectedRecord.fuelType)" :severity="getFuelTagSeverity(selectedRecord.fuelType)" rounded class="text-xs" />
            </div>
            <div class="flex justify-between">
              <span class="text-gray-600">ปริมาณ</span>
              <span class="font-semibold">{{ selectedRecord.liters }} ลิตร</span>
            </div>
            <div class="flex justify-between border-t border-red-200 pt-2 mt-1">
              <span class="font-bold text-red-800">วงเงินรวม</span>
              <span class="font-bold text-red-700 text-lg">{{ formatCurrency(selectedRecord.totalAmount) }}</span>
            </div>
          </div>
        </div>
        <div v-if="selectedRecord.note" class="bg-amber-50 rounded-lg p-3 border border-amber-100 text-sm">
          <p class="text-gray-500 text-xs mb-1">หมายเหตุ</p>
          <p class="text-gray-700">{{ selectedRecord.note }}</p>
        </div>
      </div>
      <template #footer>
        <Button label="ปิด" severity="secondary" text @click="detailVisible = false" />
        <Button label="แก้ไข" icon="pi pi-pencil" severity="warning" @click="openEdit" />
      </template>
    </Dialog>

    <!-- Edit Dialog -->
    <Dialog v-model:visible="editVisible" modal header="แก้ไขข้อมูลการเติมน้ำมัน"
      :style="{ width: '620px' }" :draggable="false">
      <div class="flex flex-col gap-4">
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">หน่วยงาน</label>
            <Select v-if="currentUserRole === 'superadmin'" v-model="editForm.departmentId" :options="departments" optionLabel="name" optionValue="id" class="w-full" />
            <InputText v-else :value="getDeptName(editForm.departmentId)" disabled class="w-full bg-gray-100" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">วันที่</label>
            <DatePicker v-model="editForm.refuelDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ประเภทเอกสาร</label>
            <Select v-model="editForm.documentType" :options="documentTypes" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">เลขที่เอกสาร</label>
            <InputText v-model="editForm.documentNumber" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">เลขทะเบียนรถ</label>
            <InputText v-model="editForm.vehiclePlate" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">จังหวัด</label>
            <Select v-model="editForm.vehicleProvince" :options="provinces" filter class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ผู้จัดซื้อน้ำมัน</label>
            <InputText v-model="editForm.purchaserName" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">บริษัทจำหน่ายน้ำมัน</label>
            <InputText v-model="editForm.gasStationCompany" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ประเภทน้ำมัน</label>
            <Select v-model="editForm.fuelType" :options="fuelTypes" optionLabel="name" optionValue="id" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">ปริมาณ (ลิตร)</label>
            <InputNumber v-model="editForm.liters" :minFractionDigits="0" :maxFractionDigits="2" suffix=" ลิตร" class="w-full" />
          </div>
          <div class="flex flex-col gap-2">
            <label class="text-sm font-semibold text-gray-700">วงเงินรวม (บาท)</label>
            <InputNumber v-model="editForm.totalAmount" mode="currency" currency="THB" locale="th-TH" class="w-full" />
          </div>
          <div class="flex items-end pb-1 text-sm">
            <span class="text-gray-500 mr-1">ราคาเฉลี่ย:</span>
            <span class="font-bold text-red-600">{{ editPricePerLiter > 0 ? editPricePerLiter.toFixed(2) : '0.00' }} บาท/ลิตร</span>
          </div>
          <div class="flex flex-col gap-2 sm:col-span-2">
            <label class="text-sm font-semibold text-gray-700">หมายเหตุ</label>
            <Textarea v-model="editForm.note" rows="2" class="w-full" />
          </div>
        </div>
      </div>
      <template #footer>
        <Button label="ลบ" icon="pi pi-trash" severity="danger" text @click="deleteRecord" />
        <Button label="ยกเลิก" severity="secondary" text @click="editVisible = false" />
        <Button label="บันทึก" icon="pi pi-save" severity="danger" :loading="isSaving" @click="saveEdit" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
:deep(.p-inputnumber-input) { width: 100%; }
:deep(.p-datatable-header-cell) {
  background-color: #f8fafc !important;
  color: #475569 !important;
  font-weight: 700 !important;
}
</style>
