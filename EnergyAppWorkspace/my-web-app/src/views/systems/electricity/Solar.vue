<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/services/api'

import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'
defineOptions({ name: 'SolarSystem' })

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

interface SolarRecord {
  recordDate: Date | null
  buildingId: string
  solarUnitProduced: number | null
  note: string
}

interface FetchedSolarRecord {
  id: string
  type: string
  recordDate: string | null
  buildingId: string
  solarUnitProduced: number
  productionWh: number
  toBatteryWh: number
  toGridWh: number
  toHomeWh: number
  consumptionWh: number
  fromBatteryWh: number
  fromGridWh: number
  fromSolarWh: number
  note: string
  recordedBy: string
  departmentId: string
  createdAt: string
}

interface Building {
  id: string
  name: string
}

const authStore = useAuthStore()
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('electricity')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')
const buildings = ref<Building[]>([])

const manualForm = ref({
  recordDate: null as Date | null,
  buildingId: '',
  productionWh: null as number | null,
  toBatteryWh: null as number | null,
  toGridWh: null as number | null,
  toHomeWh: null as number | null,
  consumptionWh: null as number | null,
  fromBatteryWh: null as number | null,
  fromGridWh: null as number | null,
  fromSolarWh: null as number | null,
  note: '',
})
const isSubmitting = ref<boolean>(false)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')
const csvSuccessMessage = ref<string>('')
const csvErrorMessage = ref<string>('')

const historyRecords = ref<FetchedSolarRecord[]>([])
const isLoadingHistory = ref<boolean>(true)
const isLoadingMore = ref<boolean>(false)
const hasMore = ref<boolean>(false)
const skip = ref(0)
const PAGE_SIZE = 20

const fetchHistory = async (loadMore = false): Promise<void> => {
  if (loadMore) isLoadingMore.value = true
  else { isLoadingHistory.value = true; historyRecords.value = []; skip.value = 0 }
  try {
    const response = await api.get('/SolarProduction', {
      params: {
        skip: loadMore ? skip.value : 0,
        take: PAGE_SIZE,
      },
    })
    const data = response.data
    const records: FetchedSolarRecord[] = Array.isArray(data) ? data : (data.items ?? [])
    const total: number = Array.isArray(data) ? records.length : (data.total ?? records.length)
    if (loadMore) historyRecords.value.push(...records)
    else historyRecords.value = records
    skip.value = historyRecords.value.length
    hasMore.value = historyRecords.value.length < total
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูล Solar ได้')
  } finally {
    isLoadingHistory.value = false
    isLoadingMore.value = false
  }
}

onMounted(async () => {
  try {
    const response = await api.get('/Building')
    buildings.value = response.data.map((b: { id: string; name: string }) => ({ id: b.id, name: b.name }))
  } catch (e) {
    toast.fromError(e, 'ไม่สามารถโหลดข้อมูลอาคารได้')
  }
  fetchHistory()
})


const submitManualForm = async (): Promise<void> => {
  successMessage.value = ''
  errorMessage.value = ''
  const d = manualForm.value
  if (!d.buildingId) {
    errorMessage.value = 'กรุณาเลือกอาคาร/จุดติดตั้ง'
    return
  }
  if (!d.recordDate) {
    errorMessage.value = 'กรุณาเลือกวันที่'
    return
  }
  if (
    d.productionWh == null ||
    d.toBatteryWh == null ||
    d.toGridWh == null ||
    d.toHomeWh == null ||
    d.consumptionWh == null ||
    d.fromBatteryWh == null ||
    d.fromGridWh == null ||
    d.fromSolarWh == null
  ) {
    errorMessage.value = 'กรุณากรอกข้อมูลให้ครบถ้วน'
    return
  }
  isSubmitting.value = true
  try {
    await api.post('/SolarProduction', {
      buildingId: d.buildingId,
      recordDate: d.recordDate.toISOString(),
      solarUnitProduced: d.productionWh / 1000,
      productionWh: d.productionWh,
      toBatteryWh: d.toBatteryWh,
      toGridWh: d.toGridWh,
      toHomeWh: d.toHomeWh,
      consumptionWh: d.consumptionWh,
      fromBatteryWh: d.fromBatteryWh,
      fromGridWh: d.fromGridWh,
      fromSolarWh: d.fromSolarWh,
      note: d.note,
      departmentId: currentUserDepartment.value,
      recordedBy: authStore.user?.uid || authStore.user?.email || 'unknown',
    })
    successMessage.value = 'บันทึกข้อมูล Solar สำเร็จ'
    manualForm.value = {
      recordDate: null, buildingId: '', productionWh: null, toBatteryWh: null, toGridWh: null, toHomeWh: null,
      consumptionWh: null, fromBatteryWh: null, fromGridWh: null, fromSolarWh: null, note: ''
    }
    fetchHistory()
  } catch (err: unknown) {
    errorMessage.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาด'
  } finally {
    isSubmitting.value = false
  }
}

const formData = ref({ buildingId: '', note: '' })
const selectedFile = ref<File | null>(null)

const handleFileSelect = (event: Event): void => {
  const target = event.target as HTMLInputElement
  selectedFile.value = target.files?.[0] || null
}

const parseCSVRow = (row: string): string[] => {
  const result: string[] = []
  let current = ''
  let inQuotes = false
  for (const char of row) {
    if (char === '"') inQuotes = !inQuotes
    else if (char === ',' && !inQuotes) { result.push(current); current = '' }
    else current += char
  }
  result.push(current)
  return result
}

const submitForm = async (): Promise<void> => {
  successMessage.value = ''
  csvErrorMessage.value = ''
  csvSuccessMessage.value = ''
  if (!formData.value.buildingId) {
    csvErrorMessage.value = 'กรุณาเลือกอาคาร/จุดติดตั้ง'
    return
  }
  if (!selectedFile.value) {
    csvErrorMessage.value = 'กรุณาเลือกไฟล์ CSV'
    return
  }
  isSubmitting.value = true
  const reader = new FileReader()
  reader.onload = async (e) => {
    try {
      const text = e.target?.result
      if (typeof text !== 'string') throw new Error('ไม่สามารถอ่านไฟล์ได้')
      const rows = text.split('\n').map(r => r.trim()).filter(r => r.length > 0)
      if (rows.length < 2) throw new Error('ไฟล์ไม่มีข้อมูล')
      const records: object[] = []
      for (const row of rows.slice(1)) {
        const cols = parseCSVRow(row)
        if (cols.length < 9) continue
        const rawDate = cols[0] || ''
        const parts = rawDate.split('/')
        if (parts.length !== 3) continue
        const mm = parseInt(parts[0] || '1', 10)
        const dd = parseInt(parts[1] || '1', 10)
        let yyyy = parseInt((parts[2] || '').split(' ')[0] || '2000', 10)
        if (yyyy < 2000) yyyy += 2000
        const productionWh = Number((cols[1] || '').replace(/,/g, '')) || 0
        records.push({
          buildingId: formData.value.buildingId,
          recordDate: new Date(yyyy, mm - 1, dd).toISOString(),
          solarUnitProduced: productionWh / 1000,
          productionWh,
          toBatteryWh: Number((cols[2] || '').replace(/,/g, '')) || 0,
          toGridWh: Number((cols[3] || '').replace(/,/g, '')) || 0,
          toHomeWh: Number((cols[4] || '').replace(/,/g, '')) || 0,
          consumptionWh: Number((cols[5] || '').replace(/,/g, '')) || 0,
          fromBatteryWh: Number((cols[6] || '').replace(/,/g, '')) || 0,
          fromGridWh: Number((cols[7] || '').replace(/,/g, '')) || 0,
          fromSolarWh: Number((cols[8] || '').replace(/,/g, '')) || 0,
          note: formData.value.note || selectedFile.value?.name || 'นำเข้าข้อมูล CSV',
          recordedBy: authStore.user?.uid || authStore.user?.email || 'unknown',
        })
      }

      if (records.length === 0) {
        throw new Error('ไม่พบข้อมูลรูปแบบที่ถูกต้องในไฟล์')
      }

      // POST records one-by-one (or in batch if API supports)
      for (const record of records) {
        await api.post('/SolarProduction', record)
      }

      csvSuccessMessage.value = `นำเข้าข้อมูล Solar สำเร็จจำนวน ${records.length} รายการ`

      selectedFile.value = null
      const fileInput = document.getElementById('solarCsv') as HTMLInputElement
      if (fileInput) fileInput.value = ''
      formData.value.note = ''
      fetchHistory()
    } catch (err: unknown) {
      csvErrorMessage.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการอ่านไฟล์'
    } finally {
      isSubmitting.value = false
    }
  }
  reader.readAsText(selectedFile.value)
}

const downloadTemplate = (): void => {
  const header = 'Date,Production(Wh),To Battery(Wh),To Grid(Wh),To Home(Wh),Consumption(Wh),From Battery(Wh),From Grid(Wh),From Solar(Wh)'
  const example = '04/01/2026,12500,2000,3000,7500,9800,500,800,8500'
  const csv = `${header}\n${example}\n`
  const a = document.createElement('a')
  a.href = 'data:text/csv;charset=utf-8,' + encodeURIComponent(csv)
  a.download = 'solar_template.csv'
  a.click()
}

const getBuildingName = (id: string): string => buildings.value.find((x) => x.id === id)?.name || id
const formatThaiDate = (dateStr: string | null | undefined): string => {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' })
}

// Detail / Edit dialog
const selectedRecord = ref<FetchedSolarRecord | null>(null)
const detailVisible = ref(false)
const editVisible = ref(false)
const isSaving = ref(false)

interface SolarEditForm {
  buildingId: string
  recordDate: Date | null
  solarUnitProduced: number | null
  productionWh: number
  toBatteryWh: number
  toGridWh: number
  toHomeWh: number
  consumptionWh: number
  fromBatteryWh: number
  fromGridWh: number
  fromSolarWh: number
  note: string
}

const editForm = ref<SolarEditForm>({
  buildingId: '', recordDate: null, solarUnitProduced: null,
  productionWh: 0, toBatteryWh: 0, toGridWh: 0, toHomeWh: 0,
  consumptionWh: 0, fromBatteryWh: 0, fromGridWh: 0, fromSolarWh: 0,
  note: '',
})

const editFile = ref<File | null>(null)
const handleEditFileSelect = (event: Event): void => {
  const target = event.target as HTMLInputElement
  editFile.value = target.files?.[0] || null
}

const openDetail = (event: { data: FetchedSolarRecord }) => {
  selectedRecord.value = event.data
  detailVisible.value = true
}

const openEdit = () => {
  if (!selectedRecord.value) return
  const r = selectedRecord.value
  editForm.value = {
    buildingId: r.buildingId,
    recordDate: r.recordDate ? new Date(r.recordDate) : null,
    solarUnitProduced: r.solarUnitProduced,
    productionWh: r.productionWh || 0,
    toBatteryWh: r.toBatteryWh || 0,
    toGridWh: r.toGridWh || 0,
    toHomeWh: r.toHomeWh || 0,
    consumptionWh: r.consumptionWh || 0,
    fromBatteryWh: r.fromBatteryWh || 0,
    fromGridWh: r.fromGridWh || 0,
    fromSolarWh: r.fromSolarWh || 0,
    note: r.note,
  }
  editFile.value = null
  detailVisible.value = false
  editVisible.value = true
}

const saveEdit = async () => {
  if (!selectedRecord.value) return
  isSaving.value = true

  const doSave = async (data: Omit<Partial<SolarEditForm>, 'recordDate'> & { recordDate?: string | null }) => {
    await api.put(`/SolarProduction/${selectedRecord.value!.id}`, {
      buildingId: data.buildingId ?? editForm.value.buildingId,
      recordDate: data.recordDate,
      solarUnitProduced: data.solarUnitProduced ?? editForm.value.solarUnitProduced ?? 0,
      productionWh: data.productionWh ?? editForm.value.productionWh,
      toBatteryWh: data.toBatteryWh ?? editForm.value.toBatteryWh,
      toGridWh: data.toGridWh ?? editForm.value.toGridWh,
      toHomeWh: data.toHomeWh ?? editForm.value.toHomeWh,
      consumptionWh: data.consumptionWh ?? editForm.value.consumptionWh,
      fromBatteryWh: data.fromBatteryWh ?? editForm.value.fromBatteryWh,
      fromGridWh: data.fromGridWh ?? editForm.value.fromGridWh,
      fromSolarWh: data.fromSolarWh ?? editForm.value.fromSolarWh,
      note: data.note ?? editForm.value.note,
    })
    fetchHistory()
    toast.success('บันทึกข้อมูลสำเร็จ')
    editVisible.value = false
  }

  try {
    if (editFile.value) {
      // อ่าน CSV แล้วอัปเดตจากแถวแรก
      const reader = new FileReader()
      reader.onload = async (e) => {
        try {
          const text = e.target?.result
          if (typeof text !== 'string') throw new Error('ไม่สามารถอ่านไฟล์ได้')
          const rows = text.split('\n').map(r => r.trim()).filter(r => r.length > 0)
          if (rows.length < 2) throw new Error('ไฟล์ไม่มีข้อมูล')
          const cols = parseCSVRow(rows[1] || '')
          if (cols.length < 9) throw new Error('รูปแบบไฟล์ไม่ถูกต้อง (ต้องมีอย่างน้อย 9 คอลัมน์)')

          const rawDate = cols[0] || ''
          const parts = rawDate.split('/')
          if (parts.length !== 3) throw new Error('รูปแบบวันที่ไม่ถูกต้อง (MM/DD/YYYY)')
          const mm = parseInt(parts[0] || '1', 10)
          const dd = parseInt(parts[1] || '1', 10)
          let yyyy = parseInt((parts[2] || '').split(' ')[0] || '2000', 10)
          if (yyyy < 2000) yyyy += 2000

          const productionWh = Number((cols[1] || '').replace(/,/g, '')) || 0
          await doSave({
            buildingId: editForm.value.buildingId,
            recordDate: new Date(yyyy, mm - 1, dd).toISOString(),
            solarUnitProduced: productionWh / 1000,
            productionWh,
            toBatteryWh: Number((cols[2] || '').replace(/,/g, '')) || 0,
            toGridWh: Number((cols[3] || '').replace(/,/g, '')) || 0,
            toHomeWh: Number((cols[4] || '').replace(/,/g, '')) || 0,
            consumptionWh: Number((cols[5] || '').replace(/,/g, '')) || 0,
            fromBatteryWh: Number((cols[6] || '').replace(/,/g, '')) || 0,
            fromGridWh: Number((cols[7] || '').replace(/,/g, '')) || 0,
            fromSolarWh: Number((cols[8] || '').replace(/,/g, '')) || 0,
            note: editForm.value.note || editFile.value?.name || '',
          })
        } catch (err: unknown) {
          toast.fromError(err, 'เกิดข้อผิดพลาดในการอ่านไฟล์')
        } finally {
          isSaving.value = false
        }
      }
      reader.readAsText(editFile.value)
    } else {
      await doSave({
        recordDate: editForm.value.recordDate ? editForm.value.recordDate.toISOString() : null,
      })
      isSaving.value = false
    }
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่')
    isSaving.value = false
  }
}

const deleteRecord = async () => {
  if (!selectedRecord.value) return
  if (!confirm('ยืนยันการลบข้อมูลนี้?')) return
  try {
    await api.delete(`/SolarProduction/${selectedRecord.value.id}`)
    historyRecords.value = historyRecords.value.filter(r => r.id !== selectedRecord.value!.id)
    skip.value = historyRecords.value.length
    toast.success('ลบข้อมูลสำเร็จ')
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
      <h2 class="text-2xl font-bold text-gray-800">บันทึกข้อมูลพลังงาน Solar</h2>
      <p class="text-gray-500 mt-1">บันทึกหน่วยพลังงานไฟฟ้าที่ผลิตได้จากแผงโซลาร์เซลล์</p>
    </div>

    <Tabs value="0" lazy>
      <TabList>
        <Tab value="0" v-if="isAdmin"><i class="pi pi-pencil mr-2"></i>บันทึกข้อมูล Solar</Tab>
        <Tab value="1" v-if="isAdmin"><i class="pi pi-upload mr-2"></i>นำเข้าจากไฟล์ CSV</Tab>
        <Tab value="2">
          <i class="pi pi-history mr-2"></i>ประวัติย้อนหลัง
          <span
            v-if="historyRecords.length > 0"
            class="ml-2 bg-green-100 text-green-600 px-2 py-0.5 rounded-full text-xs"
            >{{ historyRecords.length }}</span
          >
        </Tab>
      </TabList>

      <TabPanels>
        <!-- Tab 0: บันทึกทีละรายการ -->
        <TabPanel value="0">
          <form @submit.prevent="submitManualForm" class="mt-2">
            <Message v-if="successMessage" severity="success" :closable="true" class="mb-4">{{ successMessage }}</Message>
            <Message v-if="errorMessage" severity="error" :closable="true" class="mb-4">{{ errorMessage }}</Message>

            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">

              <!-- คอลัมน์ซ้าย: ข้อมูลทั่วไป -->
              <div class="flex flex-col gap-4">
                <div class="bg-white rounded-xl border shadow-sm p-5">
                  <div class="flex items-center gap-2 mb-4 pb-3 border-b">
                    <div class="w-8 h-8 bg-emerald-100 rounded-lg flex items-center justify-center">
                      <i class="pi pi-info-circle text-emerald-600 text-sm"></i>
                    </div>
                    <h3 class="font-bold text-gray-700">ข้อมูลทั่วไป</h3>
                  </div>
                  <div class="flex flex-col gap-4">
                    <div class="flex flex-col gap-1.5">
                      <label class="font-semibold text-sm text-gray-600">อาคาร/จุดติดตั้ง <span class="text-red-500">*</span></label>
                      <Select v-model="manualForm.buildingId" :options="buildings" optionLabel="name" optionValue="id"
                        placeholder="-- กรุณาเลือกอาคาร --" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-1.5">
                      <label class="font-semibold text-sm text-gray-600">วันที่บันทึก <span class="text-red-500">*</span></label>
                      <DatePicker v-model="manualForm.recordDate" dateFormat="dd/mm/yy" showIcon
                        placeholder="-- เลือกวันที่ --" class="w-full" />
                    </div>
                    <div class="flex flex-col gap-1.5">
                      <label class="font-semibold text-sm text-gray-600">หมายเหตุ</label>
                      <InputText v-model="manualForm.note" placeholder="หมายเหตุเพิ่มเติม..." class="w-full" />
                    </div>
                  </div>
                </div>

                <!-- Production KPI -->
                <div class="bg-emerald-600 rounded-xl p-5 text-white">
                  <p class="text-emerald-200 text-xs font-semibold uppercase tracking-wider mb-1">Production รวม</p>
                  <p class="text-3xl font-bold">
                    {{ manualForm.productionWh != null ? ((manualForm.productionWh) / 1000).toFixed(2) : '0.00' }}
                    <span class="text-lg font-normal text-emerald-200">kWh</span>
                  </p>
                  <p class="text-emerald-300 text-xs mt-2">= {{ (manualForm.productionWh || 0).toLocaleString() }} Wh</p>
                </div>
              </div>

              <!-- คอลัมน์กลาง: Solar Output (จ่ายออก) -->
              <div class="bg-white rounded-xl border shadow-sm p-5">
                <div class="flex items-center gap-2 mb-4 pb-3 border-b">
                  <div class="w-8 h-8 bg-blue-100 rounded-lg flex items-center justify-center">
                    <i class="pi pi-arrow-up-right text-blue-600 text-sm"></i>
                  </div>
                  <div>
                    <h3 class="font-bold text-gray-700">Solar Output</h3>
                    <p class="text-xs text-gray-400">พลังงานที่ผลิตและจ่ายออกไป (Wh)</p>
                  </div>
                </div>
                <div class="flex flex-col gap-4">
                  <div class="flex flex-col gap-1.5">
                    <label class="font-semibold text-sm text-gray-600">
                      <span class="inline-block w-2 h-2 rounded-full bg-emerald-500 mr-1.5"></span>
                      Production (Wh) <span class="text-red-500">*</span>
                    </label>
                    <InputNumber v-model="manualForm.productionWh" :minFractionDigits="0" :maxFractionDigits="2"
                      placeholder="0" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-1.5">
                    <label class="font-semibold text-sm text-gray-600">
                      <span class="inline-block w-2 h-2 rounded-full bg-blue-400 mr-1.5"></span>
                      To Battery (Wh) <span class="text-red-500">*</span>
                    </label>
                    <InputNumber v-model="manualForm.toBatteryWh" :minFractionDigits="0" :maxFractionDigits="2"
                      placeholder="0" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-1.5">
                    <label class="font-semibold text-sm text-gray-600">
                      <span class="inline-block w-2 h-2 rounded-full bg-violet-400 mr-1.5"></span>
                      To Grid (Wh) <span class="text-red-500">*</span>
                    </label>
                    <InputNumber v-model="manualForm.toGridWh" :minFractionDigits="0" :maxFractionDigits="2"
                      placeholder="0" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-1.5">
                    <label class="font-semibold text-sm text-gray-600">
                      <span class="inline-block w-2 h-2 rounded-full bg-teal-400 mr-1.5"></span>
                      To Home (Wh) <span class="text-red-500">*</span>
                    </label>
                    <InputNumber v-model="manualForm.toHomeWh" :minFractionDigits="0" :maxFractionDigits="2"
                      placeholder="0" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-1.5">
                    <label class="font-semibold text-sm text-gray-600">
                      <span class="inline-block w-2 h-2 rounded-full bg-gray-400 mr-1.5"></span>
                      Consumption (Wh) <span class="text-red-500">*</span>
                    </label>
                    <InputNumber v-model="manualForm.consumptionWh" :minFractionDigits="0" :maxFractionDigits="2"
                      placeholder="0" class="w-full" />
                  </div>
                </div>
              </div>

              <!-- คอลัมน์ขวา: Solar Input (รับเข้า) -->
              <div class="bg-white rounded-xl border shadow-sm p-5">
                <div class="flex items-center gap-2 mb-4 pb-3 border-b">
                  <div class="w-8 h-8 bg-orange-100 rounded-lg flex items-center justify-center">
                    <i class="pi pi-arrow-down-left text-orange-500 text-sm"></i>
                  </div>
                  <div>
                    <h3 class="font-bold text-gray-700">Solar Input</h3>
                    <p class="text-xs text-gray-400">พลังงานที่รับเข้าสู่ระบบ (Wh)</p>
                  </div>
                </div>
                <div class="flex flex-col gap-4">
                  <div class="flex flex-col gap-1.5">
                    <label class="font-semibold text-sm text-gray-600">
                      <span class="inline-block w-2 h-2 rounded-full bg-blue-400 mr-1.5"></span>
                      From Battery (Wh) <span class="text-red-500">*</span>
                    </label>
                    <InputNumber v-model="manualForm.fromBatteryWh" :minFractionDigits="0" :maxFractionDigits="2"
                      placeholder="0" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-1.5">
                    <label class="font-semibold text-sm text-gray-600">
                      <span class="inline-block w-2 h-2 rounded-full bg-rose-400 mr-1.5"></span>
                      From Grid (Wh) <span class="text-red-500">*</span>
                    </label>
                    <InputNumber v-model="manualForm.fromGridWh" :minFractionDigits="0" :maxFractionDigits="2"
                      placeholder="0" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-1.5">
                    <label class="font-semibold text-sm text-gray-600">
                      <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 mr-1.5"></span>
                      From Solar (Wh) <span class="text-red-500">*</span>
                    </label>
                    <InputNumber v-model="manualForm.fromSolarWh" :minFractionDigits="0" :maxFractionDigits="2"
                      placeholder="0" class="w-full" />
                  </div>

                  <!-- Summary box -->
                  <div class="mt-2 bg-gray-50 rounded-lg p-4 border text-sm">
                    <p class="text-xs text-gray-400 font-semibold uppercase mb-2">สรุปพลังงานรับเข้า</p>
                    <div class="flex justify-between text-gray-600 py-1">
                      <span>From Battery</span>
                      <span class="font-medium">{{ (manualForm.fromBatteryWh || 0).toLocaleString() }} Wh</span>
                    </div>
                    <div class="flex justify-between text-gray-600 py-1">
                      <span>From Grid</span>
                      <span class="font-medium">{{ (manualForm.fromGridWh || 0).toLocaleString() }} Wh</span>
                    </div>
                    <div class="flex justify-between text-gray-600 py-1">
                      <span>From Solar</span>
                      <span class="font-medium">{{ (manualForm.fromSolarWh || 0).toLocaleString() }} Wh</span>
                    </div>
                    <div class="flex justify-between font-bold text-gray-800 border-t pt-2 mt-1">
                      <span>รวม</span>
                      <span class="text-emerald-600">
                        {{ ((manualForm.fromBatteryWh || 0) + (manualForm.fromGridWh || 0) + (manualForm.fromSolarWh || 0)).toLocaleString() }} Wh
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Submit -->
            <div class="flex justify-end mt-6 pt-5 border-t">
              <Button type="submit" label="บันทึกข้อมูล Solar" icon="pi pi-save"
                severity="success" :loading="isSubmitting" class="px-10" />
            </div>
          </form>
        </TabPanel>

        <!-- Tab 1: นำเข้า CSV -->
        <TabPanel value="1">
          <Card class="shadow-sm border-none mt-2">
            <template #content>
              <div class="max-w-xl mx-auto">
                <form
                  @submit.prevent="submitForm"
                  class="flex flex-col gap-6 bg-gray-50 rounded-xl p-6 border"
                >
                  <div>
                    <h3 class="font-bold text-lg text-gray-700">นำเข้าข้อมูล Solar จากไฟล์ CSV</h3>
                    <p class="text-sm text-gray-400 mt-1">อัปโหลดไฟล์ CSV เพื่อนำเข้าข้อมูลหลายรายการพร้อมกัน</p>
                  </div>
                  <Message v-if="csvSuccessMessage" severity="success" :closable="true">{{
                    csvSuccessMessage
                  }}</Message>
                  <Message v-if="csvErrorMessage" severity="error" :closable="true">{{
                    csvErrorMessage
                  }}</Message>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700"
                      >เลือกอาคาร/จุดติดตั้ง <span class="text-red-500">*</span></label
                    >
                    <Select
                      v-model="formData.buildingId"
                      :options="buildings"
                      optionLabel="name"
                      optionValue="id"
                      placeholder="-- กรุณาเลือกอาคาร --"
                      class="w-full"
                    />
                  </div>
                  <div class="flex flex-col gap-2">
                    <div class="flex items-center justify-between">
                      <label class="font-semibold text-sm text-gray-700"
                        >ไฟล์ข้อมูล Solar (.csv) <span class="text-red-500">*</span></label
                      >
                      <Button
                        type="button"
                        label="ดาวน์โหลด Template"
                        icon="pi pi-download"
                        severity="secondary"
                        size="small"
                        text
                        @click="downloadTemplate"
                      />
                    </div>
                    <div
                      class="border-2 border-dashed border-gray-300 rounded-xl p-6 text-center hover:bg-gray-50 transition-colors"
                    >
                      <i class="pi pi-cloud-upload text-4xl text-gray-400 mb-3"></i>
                      <p class="text-xs text-gray-400 mb-3">Date, Production(Wh), To Battery, To Grid, To Home, Consumption, From Battery, From Grid, From Solar</p>
                      <input
                        type="file"
                        id="solarCsv"
                        accept=".csv"
                        @change="handleFileSelect"
                        class="block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100 cursor-pointer"
                      />
                    </div>
                    <p v-if="selectedFile" class="text-sm text-blue-600 flex items-center gap-2">
                      <i class="pi pi-file"></i> {{ selectedFile.name }}
                    </p>
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">หมายเหตุเพิ่มเติม</label>
                    <InputText
                      v-model="formData.note"
                      placeholder="เช่น ชื่อผู้รับผิดชอบ, ข้อมูลนำเข้าจากระบบ X"
                      class="w-full"
                    />
                  </div>
                  <div class="flex justify-end mt-4 border-t pt-6">
                    <Button
                      type="submit"
                      label="นำเข้าข้อมูล CSV"
                      icon="pi pi-upload"
                      severity="info"
                      :loading="isSubmitting"
                      class="px-8"
                    />
                  </div>
                </form>
              </div>
            </template>
          </Card>
        </TabPanel>

        <!-- Tab 2: ประวัติ -->
        <TabPanel value="2">
          <Card class="shadow-sm border-none mt-2 overflow-hidden">
            <template #content>
              <DataTable
                :value="historyRecords"
                :loading="isLoadingHistory"
                paginator
                :rows="10"
                stripedRows
                responsiveLayout="scroll"
                emptyMessage="ยังไม่มีข้อมูล"
                selectionMode="single"
                @row-click="openDetail"
                class="cursor-pointer"
              >
                <Column header="จุดติดตั้ง">
                  <template #body="sp">
                    <span class="font-semibold">{{ getBuildingName(sp.data.buildingId) }}</span>
                  </template>
                </Column>
                <Column header="วันที่จด">
                  <template #body="sp">{{ formatThaiDate(sp.data.recordDate) }}</template>
                </Column>
                <Column header="หมายเหตุ">
                  <template #body="sp">
                    <span class="text-gray-500 italic">{{ sp.data.note || '-' }}</span>
                  </template>
                </Column>
                <Column header="พลังงานที่ผลิตได้">
                  <template #body="sp">
                    <span class="font-bold text-green-600"
                      >{{ sp.data.solarUnitProduced || 0 }} kWh</span
                    >
                  </template>
                </Column>
                <Column header="" style="width: 3rem">
                  <template #body><i class="pi pi-chevron-right text-gray-400"></i></template>
                </Column>
              </DataTable>
              <div class="flex justify-center mt-4">
                <Button
                  v-if="hasMore"
                  label="โหลดเพิ่มเติม"
                  icon="pi pi-chevron-down"
                  severity="secondary"
                  outlined
                  :loading="isLoadingMore"
                  @click="fetchHistory(true)"
                />
                <p v-else-if="historyRecords.length > 0" class="text-xs text-gray-400 py-2">
                  แสดงทั้งหมด {{ historyRecords.length }} รายการ
                </p>
              </div>
            </template>
          </Card>

          <!-- Detail Dialog -->
          <Dialog
            v-model:visible="detailVisible"
            modal
            header="รายละเอียดข้อมูล Solar"
            :style="{ width: '560px' }"
            :draggable="false"
          >
            <div v-if="selectedRecord" class="flex flex-col gap-4">
              <div class="grid grid-cols-2 gap-3 text-sm">
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">จุดติดตั้ง</p>
                  <p class="font-semibold text-gray-800">
                    {{ getBuildingName(selectedRecord.buildingId) }}
                  </p>
                </div>
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">วันที่จด</p>
                  <p class="font-semibold text-gray-800">
                    {{ formatThaiDate(selectedRecord.recordDate) }}
                  </p>
                </div>
              </div>

              <div class="bg-green-50 rounded-xl p-4 border border-green-100 text-center">
                <p class="text-gray-500 text-sm mb-1">พลังงานที่ผลิตได้</p>
                <p class="font-bold text-green-700 text-3xl">
                  {{ selectedRecord.solarUnitProduced || 0 }} <span class="text-lg">kWh</span>
                </p>
              </div>

              <div
                v-if="selectedRecord.productionWh || selectedRecord.consumptionWh"
                class="border border-gray-200 rounded-xl overflow-hidden text-sm"
              >
                <div
                  class="bg-gray-100 px-4 py-2 font-semibold text-gray-600 text-xs uppercase tracking-wide"
                >
                  รายละเอียดพลังงาน (Wh)
                </div>
                <div class="divide-y divide-gray-100">
                  <div class="flex justify-between px-4 py-2">
                    <span class="text-gray-500">ผลิตทั้งหมด</span>
                    <span class="font-semibold text-green-600"
                      >{{ (selectedRecord.productionWh || 0).toLocaleString() }} Wh</span
                    >
                  </div>
                  <div class="flex justify-between px-4 py-2">
                    <span class="text-gray-500">การใช้รวม</span>
                    <span class="font-semibold"
                      >{{ (selectedRecord.consumptionWh || 0).toLocaleString() }} Wh</span
                    >
                  </div>
                  <div class="grid grid-cols-2 divide-x divide-gray-100">
                    <div class="px-4 py-2">
                      <p class="text-gray-400 text-xs mb-1">ส่งแบตเตอรี่</p>
                      <p class="font-semibold text-blue-600">
                        {{ (selectedRecord.toBatteryWh || 0).toLocaleString() }} Wh
                      </p>
                    </div>
                    <div class="px-4 py-2">
                      <p class="text-gray-400 text-xs mb-1">ส่งกริด</p>
                      <p class="font-semibold text-blue-600">
                        {{ (selectedRecord.toGridWh || 0).toLocaleString() }} Wh
                      </p>
                    </div>
                    <div class="px-4 py-2">
                      <p class="text-gray-400 text-xs mb-1">ส่งบ้าน</p>
                      <p class="font-semibold text-blue-600">
                        {{ (selectedRecord.toHomeWh || 0).toLocaleString() }} Wh
                      </p>
                    </div>
                    <div class="px-4 py-2">
                      <p class="text-gray-400 text-xs mb-1">รับจากแบตเตอรี่</p>
                      <p class="font-semibold text-orange-500">
                        {{ (selectedRecord.fromBatteryWh || 0).toLocaleString() }} Wh
                      </p>
                    </div>
                    <div class="px-4 py-2">
                      <p class="text-gray-400 text-xs mb-1">รับจากกริด</p>
                      <p class="font-semibold text-orange-500">
                        {{ (selectedRecord.fromGridWh || 0).toLocaleString() }} Wh
                      </p>
                    </div>
                    <div class="px-4 py-2">
                      <p class="text-gray-400 text-xs mb-1">รับจาก Solar</p>
                      <p class="font-semibold text-orange-500">
                        {{ (selectedRecord.fromSolarWh || 0).toLocaleString() }} Wh
                      </p>
                    </div>
                  </div>
                </div>
              </div>

              <div
                v-if="selectedRecord.note"
                class="bg-amber-50 rounded-lg p-3 border border-amber-100 text-sm"
              >
                <p class="text-gray-500 text-xs mb-1">หมายเหตุ</p>
                <p class="text-gray-700 italic">{{ selectedRecord.note }}</p>
              </div>
            </div>
            <template #footer>
              <Button label="ปิด" severity="secondary" text @click="detailVisible = false" />
              <Button
                v-if="isAdmin"
                label="แก้ไข"
                icon="pi pi-pencil"
                severity="warning"
                @click="openEdit"
              />
            </template>
          </Dialog>

          <!-- Edit Dialog -->
          <Dialog
            v-model:visible="editVisible"
            modal
            header="แก้ไขข้อมูล Solar"
            :style="{ width: '520px' }"
            :draggable="false"
          >
            <div class="flex flex-col gap-4">
              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">จุดติดตั้ง</label>
                <Select
                  v-model="editForm.buildingId"
                  :options="buildings"
                  optionLabel="name"
                  optionValue="id"
                  class="w-full"
                />
              </div>

              <!-- CSV Upload (แทนที่ข้อมูลทั้งหมด) -->
              <div class="flex flex-col gap-2">
                <div class="flex items-center justify-between">
                  <label class="text-sm font-semibold text-gray-700"
                    >อัปเดตจากไฟล์ CSV (ไม่บังคับ)</label
                  >
                  <Button
                    type="button"
                    label="ดาวน์โหลด Template"
                    icon="pi pi-download"
                    severity="secondary"
                    size="small"
                    text
                    @click="downloadTemplate"
                  />
                </div>
                <div
                  class="border-2 border-dashed rounded-xl p-4 text-center transition-colors"
                  :class="
                    editFile ? 'border-green-400 bg-green-50' : 'border-gray-300 hover:bg-gray-50'
                  "
                >
                  <input
                    type="file"
                    accept=".csv"
                    @change="handleEditFileSelect"
                    class="block w-full text-sm text-gray-500 file:mr-4 file:py-1.5 file:px-3 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100 cursor-pointer"
                  />
                  <p v-if="editFile" class="text-xs text-green-600 mt-1">{{ editFile.name }}</p>
                  <p v-else class="text-xs text-gray-400 mt-1">
                    ถ้าเลือกไฟล์ จะอ่านแถวแรกของ CSV แทนที่ข้อมูลเดิม
                  </p>
                </div>
              </div>

              <!-- Manual fields (ใช้เมื่อไม่ได้อัปโหลด CSV) -->
              <div v-if="!editFile" class="flex flex-col gap-3">
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">วันที่จด</label>
                  <DatePicker
                    v-model="editForm.recordDate"
                    dateFormat="dd/mm/yy"
                    class="w-full"
                    showIcon
                  />
                </div>
                <div class="grid grid-cols-2 gap-3">
                  <div class="flex flex-col gap-1">
                    <label class="text-xs text-gray-500">ผลิตทั้งหมด (Wh)</label>
                    <InputNumber
                      v-model="editForm.productionWh"
                      :minFractionDigits="0"
                      :maxFractionDigits="2"
                      class="w-full"
                    />
                  </div>
                  <div class="flex flex-col gap-1">
                    <label class="text-xs text-gray-500">การใช้รวม (Wh)</label>
                    <InputNumber
                      v-model="editForm.consumptionWh"
                      :minFractionDigits="0"
                      :maxFractionDigits="2"
                      class="w-full"
                    />
                  </div>
                  <div class="flex flex-col gap-1">
                    <label class="text-xs text-gray-500">ส่งแบตเตอรี่ (Wh)</label>
                    <InputNumber
                      v-model="editForm.toBatteryWh"
                      :minFractionDigits="0"
                      :maxFractionDigits="2"
                      class="w-full"
                    />
                  </div>
                  <div class="flex flex-col gap-1">
                    <label class="text-xs text-gray-500">ส่งกริด (Wh)</label>
                    <InputNumber
                      v-model="editForm.toGridWh"
                      :minFractionDigits="0"
                      :maxFractionDigits="2"
                      class="w-full"
                    />
                  </div>
                  <div class="flex flex-col gap-1">
                    <label class="text-xs text-gray-500">ส่งบ้าน (Wh)</label>
                    <InputNumber
                      v-model="editForm.toHomeWh"
                      :minFractionDigits="0"
                      :maxFractionDigits="2"
                      class="w-full"
                    />
                  </div>
                  <div class="flex flex-col gap-1">
                    <label class="text-xs text-gray-500">รับจากแบตเตอรี่ (Wh)</label>
                    <InputNumber
                      v-model="editForm.fromBatteryWh"
                      :minFractionDigits="0"
                      :maxFractionDigits="2"
                      class="w-full"
                    />
                  </div>
                  <div class="flex flex-col gap-1">
                    <label class="text-xs text-gray-500">รับจากกริด (Wh)</label>
                    <InputNumber
                      v-model="editForm.fromGridWh"
                      :minFractionDigits="0"
                      :maxFractionDigits="2"
                      class="w-full"
                    />
                  </div>
                  <div class="flex flex-col gap-1">
                    <label class="text-xs text-gray-500">รับจาก Solar (Wh)</label>
                    <InputNumber
                      v-model="editForm.fromSolarWh"
                      :minFractionDigits="0"
                      :maxFractionDigits="2"
                      class="w-full"
                    />
                  </div>
                </div>
              </div>

              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">หมายเหตุ</label>
                <InputText v-model="editForm.note" class="w-full" />
              </div>
            </div>
            <template #footer>
              <Button label="ลบ" icon="pi pi-trash" severity="danger" text @click="deleteRecord" />
              <Button label="ยกเลิก" severity="secondary" text @click="editVisible = false" />
              <Button
                label="บันทึก"
                icon="pi pi-save"
                severity="success"
                :loading="isSaving"
                @click="saveEdit"
              />
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
</style>
