<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import {
  collection,
  query,
  orderBy,
  where,
  limit,
  startAfter,
  getDocs,
  onSnapshot,
  doc,
  updateDoc,
  deleteDoc,
  writeBatch,
  serverTimestamp,
  Timestamp,
  type QueryDocumentSnapshot,
  type QueryConstraint,
} from 'firebase/firestore'
// Firebase Removed

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
  recordDate: Timestamp | null
  buildingId: string
  solarUnitProduced: number
  note: string
  recordedBy: string
  createdAt: Timestamp
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

const formData = ref<SolarRecord>({
  recordDate: null,
  buildingId: '',
  solarUnitProduced: null,
  note: '',
})
const isSubmitting = ref<boolean>(false)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')

const historyRecords = ref<FetchedSolarRecord[]>([])
const isLoadingHistory = ref<boolean>(true)
const isLoadingMore = ref<boolean>(false)
const hasMore = ref<boolean>(false)
const lastDoc = ref<QueryDocumentSnapshot | null>(null)
const PAGE_SIZE = 20
let unsubscribeBuildings: () => void

const fetchHistory = async (loadMore = false): Promise<void> => {
  if (loadMore) isLoadingMore.value = true
  else { isLoadingHistory.value = true; historyRecords.value = []; lastDoc.value = null }
  try {
    const constraints: QueryConstraint[] = [
      where('type', '==', 'SOLAR_PRODUCTION'),
      orderBy('createdAt', 'desc'),
      limit(PAGE_SIZE + 1),
    ]
    if (!isAdmin) constraints.unshift(where('departmentId', '==', currentUserDepartment.value))
    if (loadMore && lastDoc.value) constraints.push(startAfter(lastDoc.value))
    const snap = await getDocs(query(collection(db, 'energy_records'), ...constraints))
    const docs = snap.docs.slice(0, PAGE_SIZE)
    hasMore.value = snap.docs.length > PAGE_SIZE
    if (docs.length > 0) lastDoc.value = docs[docs.length - 1] ?? null
    const records = docs.map((d) => ({ id: d.id, ...d.data() } as FetchedSolarRecord))
    if (loadMore) historyRecords.value.push(...records)
    else historyRecords.value = records
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูล Solar ได้')
  } finally {
    isLoadingHistory.value = false
    isLoadingMore.value = false
  }
}

onMounted(() => {
  unsubscribeBuildings = onSnapshot(
    query(collection(db, 'buildings'), orderBy('createdAt', 'asc')),
    (snapshot) => {
      buildings.value = snapshot.docs.map((d) => ({ id: d.id, name: d.data().name as string }))
    }
  )
  fetchHistory()
})

onUnmounted(() => {
  if (unsubscribeBuildings) unsubscribeBuildings()
})

  const parseCSVRow = (str: string): string[] => {
    const result: string[] = []
    let current = ''
    let inQuotes = false
    for (let i = 0; i < str.length; i++) {
      const char = str[i]
      if (char === '"') {
        if (inQuotes && str[i + 1] === '"') {
          current += '"'; i++
        } else inQuotes = !inQuotes
      } else if (char === ',' && !inQuotes) {
        result.push(current.trim())
        current = ''
      } else current += char
    }
    result.push(current.trim())
    return result
  }

  const selectedFile = ref<File | null>(null)
  
  const handleFileSelect = (event: Event): void => {
    const target = event.target as HTMLInputElement
    if (target.files && target.files.length > 0) {
      selectedFile.value = target.files[0] || null
    }
  }

  const submitForm = async (): Promise<void> => {
    successMessage.value = ''
    errorMessage.value = ''
    if (!formData.value.buildingId) {
      errorMessage.value = 'กรุณาเลือกอาคาร/จุดติดตั้ง'
      return
    }
    if (!selectedFile.value) {
      errorMessage.value = 'กรุณาเลือกไฟล์ CSV'
      return
    }

    isSubmitting.value = true
    const reader = new FileReader()

    reader.onload = async (e: ProgressEvent<FileReader>) => {
      try {
        const text = e.target?.result
        if (typeof text !== 'string') throw new Error('ไม่สามารถอ่านไฟล์ได้')

        const rows = text.split('\n').map(r => r.trim()).filter(r => r.length > 0)
        if (rows.length < 2) throw new Error('ไฟล์ว่างเปล่า')

        const batch = writeBatch(db)
        let count = 0

        for (let i = 1; i < rows.length; i++) {
          const row = rows[i]
          if (!row) continue
          const cols = parseCSVRow(row)
          if (cols.length < 9) continue

          const rawDate = cols[0] || ''
          const parts = rawDate.split('/') // MM/DD/YYYY
          if (parts.length !== 3) continue
          
          const mm = parseInt(parts[0] || '1', 10)
          const dd = parseInt(parts[1] || '1', 10)
          let yyyy = parseInt((parts[2] || '').split(' ')[0] || '2000', 10) // in case time is appended
          
          if(yyyy < 2000) yyyy += 2000;
          
          const recordDate = new Date(yyyy, mm - 1, dd)

          // Columns definition
          // 0: Measurement Time
          // 1: Production (Wh)
          // 2: To Battery (Wh)
          // 3: To Grid (Wh)
          // 4: To Home (Wh)
          // 5: Consumption (Wh)
          // 6: From Battery (Wh)
          // 7: From Grid (Wh)
          // 8: From Solar (Wh)

          const productionWh = Number((cols[1] || '').replace(/,/g, '')) || 0
          const toBatteryWh = Number((cols[2] || '').replace(/,/g, '')) || 0
          const toGridWh = Number((cols[3] || '').replace(/,/g, '')) || 0
          const toHomeWh = Number((cols[4] || '').replace(/,/g, '')) || 0
          const consumptionWh = Number((cols[5] || '').replace(/,/g, '')) || 0
          const fromBatteryWh = Number((cols[6] || '').replace(/,/g, '')) || 0
          const fromGridWh = Number((cols[7] || '').replace(/,/g, '')) || 0
          const fromSolarWh = Number((cols[8] || '').replace(/,/g, '')) || 0

          const docData = {
            type: 'SOLAR_PRODUCTION',
            departmentId: currentUserDepartment.value,
            recordDate: Timestamp.fromDate(recordDate),
            buildingId: formData.value.buildingId,
            
            // Core Dashboard Value (kWh)
            solarUnitProduced: productionWh / 1000,
            
            // Full Dataset Detail
            productionWh,
            toBatteryWh,
            toGridWh,
            toHomeWh,
            consumptionWh,
            fromBatteryWh,
            fromGridWh,
            fromSolarWh,
            
            note: formData.value.note || selectedFile.value?.name || 'นำเข้าข้อมูล CSV',
            recordedBy: authStore.user?.uid || 'unknown',
            createdAt: serverTimestamp(),
          }

          const docRef = doc(collection(db, 'energy_records'))
          batch.set(docRef, docData)
          count++
        }
        
        if (count === 0) {
          throw new Error('ไม่พบข้อมูลรูปแบบที่ถูกต้องในไฟล์')
        }

        await batch.commit()
        successMessage.value = `นำเข้าข้อมูล Solar สำเร็จจำนวน ${count} รายการ`
        
        // Reset file
        selectedFile.value = null
        const fileInput = document.getElementById('solarCsv') as HTMLInputElement
        if (fileInput) fileInput.value = ''
        formData.value.note = ''

      } catch (err: unknown) {
        errorMessage.value = err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการอ่านไฟล์'
      } finally {
        isSubmitting.value = false
      }
    }
    reader.readAsText(selectedFile.value)
  }

const getBuildingName = (id: string): string => buildings.value.find((x) => x.id === id)?.name || id
const formatThaiDate = (ts: Timestamp | null | undefined): string => {
  if (!ts) return '-'
  return ts
    .toDate()
    .toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' })
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
  note: string
}

const editForm = ref<SolarEditForm>({
  buildingId: '', recordDate: null, solarUnitProduced: null, note: '',
})

const openDetail = (event: { data: FetchedSolarRecord }) => {
  selectedRecord.value = event.data
  detailVisible.value = true
}

const openEdit = () => {
  if (!selectedRecord.value) return
  const r = selectedRecord.value
  editForm.value = {
    buildingId: r.buildingId,
    recordDate: r.recordDate ? r.recordDate.toDate() : null,
    solarUnitProduced: r.solarUnitProduced,
    note: r.note,
  }
  detailVisible.value = false
  editVisible.value = true
}

const saveEdit = async () => {
  if (!selectedRecord.value) return
  isSaving.value = true
  try {
    await updateDoc(doc(db, 'energy_records', selectedRecord.value.id), {
      buildingId: editForm.value.buildingId,
      recordDate: editForm.value.recordDate,
      solarUnitProduced: editForm.value.solarUnitProduced || 0,
      note: editForm.value.note,
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
    await deleteDoc(doc(db, 'energy_records', selectedRecord.value.id))
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
        <Tab value="0" v-if="isAdmin"><i class="pi pi-sun mr-2"></i>บันทึกข้อมูล</Tab>
        <Tab value="1">
          <i class="pi pi-history mr-2"></i>ประวัติย้อนหลัง
          <span
            v-if="historyRecords.length > 0"
            class="ml-2 bg-green-100 text-green-600 px-2 py-0.5 rounded-full text-xs"
            >{{ historyRecords.length }}</span
          >
        </Tab>
      </TabList>

      <TabPanels>
        <TabPanel value="0">
          <Card class="shadow-sm border-none mt-2">
            <template #content>
              <form @submit.prevent="submitForm" class="flex flex-col gap-6">
                <Message v-if="successMessage" severity="success" :closable="true">{{
                  successMessage
                }}</Message>
                <Message v-if="errorMessage" severity="error" :closable="true">{{
                  errorMessage
                }}</Message>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700"
                      >เลือกอาคาร/จุดติดตั้ง <span class="text-red-500">*</span></label
                    ><Select
                      v-model="formData.buildingId"
                      :options="buildings"
                      optionLabel="name"
                      optionValue="id"
                      placeholder="-- กรุณาเลือกอาคาร --"
                      class="w-full"
                    />
                  </div>

                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">ไฟล์ข้อมูล Solar (.csv) <span class="text-red-500">*</span></label>
                    <div class="border-2 border-dashed border-gray-300 rounded-xl p-6 text-center hover:bg-gray-50 transition-colors">
                      <i class="pi pi-cloud-upload text-4xl text-gray-400 mb-3"></i>
                      <input type="file" id="solarCsv" accept=".csv" @change="handleFileSelect"
                        class="block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100 cursor-pointer" />
                    </div>
                  </div>

                  <div class="flex flex-col gap-2 md:col-span-2">
                    <label class="font-semibold text-sm text-gray-700">หมายเหตุเพิ่มเติม</label>
                    <InputText
                      v-model="formData.note"
                      placeholder="เช่น ชื่อผู้รับผิดชอบ, ข้อมูลนำเข้าจากระบบ X"
                      class="w-full"
                    />
                  </div>
                </div>
                <div class="flex justify-end mt-4 border-t pt-6">
                  <Button
                    type="submit"
                    label="บันทึกข้อมูล Solar"
                    icon="pi pi-save"
                    severity="success"
                    :loading="isSubmitting"
                    class="px-8"
                  />
                </div>
              </form>
            </template>
          </Card>
        </TabPanel>

        <TabPanel value="1">
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
                    <span class="font-bold text-green-600">{{ sp.data.solarUnitProduced || 0 }} kWh</span>
                  </template>
                </Column>
                <Column header="" style="width: 3rem">
                  <template #body><i class="pi pi-chevron-right text-gray-400"></i></template>
                </Column>
              </DataTable>
              <div class="flex justify-center mt-4">
                <Button v-if="hasMore" label="โหลดเพิ่มเติม" icon="pi pi-chevron-down"
                  severity="secondary" outlined :loading="isLoadingMore" @click="fetchHistory(true)" />
                <p v-else-if="historyRecords.length > 0" class="text-xs text-gray-400 py-2">
                  แสดงทั้งหมด {{ historyRecords.length }} รายการ
                </p>
              </div>
            </template>
          </Card>

          <!-- Detail Dialog -->
          <Dialog v-model:visible="detailVisible" modal header="รายละเอียดข้อมูล Solar"
            :style="{ width: '480px' }" :draggable="false">
            <div v-if="selectedRecord" class="flex flex-col gap-4">
              <div class="grid grid-cols-2 gap-3 text-sm">
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">จุดติดตั้ง</p>
                  <p class="font-semibold text-gray-800">{{ getBuildingName(selectedRecord.buildingId) }}</p>
                </div>
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">วันที่จด</p>
                  <p class="font-semibold text-gray-800">{{ formatThaiDate(selectedRecord.recordDate) }}</p>
                </div>
              </div>
              <div class="bg-green-50 rounded-xl p-4 border border-green-100 text-center">
                <p class="text-gray-500 text-sm mb-1">พลังงานที่ผลิตได้</p>
                <p class="font-bold text-green-700 text-3xl">{{ selectedRecord.solarUnitProduced || 0 }} <span class="text-lg">kWh</span></p>
              </div>
              <div v-if="selectedRecord.note" class="bg-amber-50 rounded-lg p-3 border border-amber-100 text-sm">
                <p class="text-gray-500 text-xs mb-1">หมายเหตุ</p>
                <p class="text-gray-700 italic">{{ selectedRecord.note }}</p>
              </div>
            </div>
            <template #footer>
              <Button label="ปิด" severity="secondary" text @click="detailVisible = false" />
              <Button v-if="isAdmin" label="แก้ไข" icon="pi pi-pencil" severity="warning" @click="openEdit" />
            </template>
          </Dialog>

          <!-- Edit Dialog -->
          <Dialog v-model:visible="editVisible" modal header="แก้ไขข้อมูล Solar"
            :style="{ width: '480px' }" :draggable="false">
            <div class="flex flex-col gap-4">
              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">จุดติดตั้ง</label>
                <Select v-model="editForm.buildingId" :options="buildings" optionLabel="name" optionValue="id" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">วันที่จด</label>
                <DatePicker v-model="editForm.recordDate" dateFormat="dd MM yy" class="w-full" showIcon />
              </div>
              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">พลังงานที่ผลิตได้ (kWh)</label>
                <InputNumber v-model="editForm.solarUnitProduced" :minFractionDigits="0" :maxFractionDigits="2" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">หมายเหตุ</label>
                <InputText v-model="editForm.note" class="w-full" />
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
</style>
