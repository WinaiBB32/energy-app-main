<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import api from '@/services/api'

import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'

defineOptions({ name: 'ElectricitySystem' })

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

interface ElectricityRecord {
  docReceiveNumber: string
  docNumber: string
  buildingId: string
  billingCycle: Date | null
  peaUnitUsed: number | null
  peaAmount: number | null
  ftRate: number | null
}

interface FetchedElectricityRecord {
  id: string
  type: string
  docReceiveNumber: string
  docNumber: string
  buildingId: string
  billingCycle: string | null
  peaUnitUsed: number
  peaAmount: number
  ftRate: number
  recordedBy: string
  departmentId: string
  createdAt: string
}

interface Building {
  id: string
  name: string
}

const authStore = useAuthStore()

const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')
const buildings = ref<Building[]>([])

const formData = ref<ElectricityRecord>({
  docReceiveNumber: '',
  docNumber: '',
  buildingId: '',
  billingCycle: null,
  peaUnitUsed: null,
  peaAmount: null,
  ftRate: null,
})

const isSubmitting = ref<boolean>(false)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')

const historyRecords = ref<FetchedElectricityRecord[]>([])
const isLoadingHistory = ref<boolean>(true)
const skip = ref(0)
const hasMore = ref(true)
const PAGE_SIZE = 20

// --- Filters ---
const filterDateFrom = ref<Date | null>(null)
const filterDateTo = ref<Date | null>(null)
const filterBuildingId = ref('')

const clearFilters = () => {
  filterDateFrom.value = null
  filterDateTo.value = null
  filterBuildingId.value = ''
}

const fetchHistory = async (loadMore = false): Promise<void> => {
  if (!hasMore.value && loadMore) return
  isLoadingHistory.value = true

  try {
    const params: Record<string, unknown> = {
      skip: loadMore ? skip.value : 0,
      take: PAGE_SIZE,
    }

    if (filterBuildingId.value) {
      params.buildingId = filterBuildingId.value
    }
    if (filterDateFrom.value) {
      const from = new Date(filterDateFrom.value)
      from.setDate(1)
      from.setHours(0, 0, 0, 0)
      params.fromDate = from.toISOString()
    }
    if (filterDateTo.value) {
      const to = new Date(filterDateTo.value)
      to.setMonth(to.getMonth() + 1, 0)
      to.setHours(23, 59, 59, 999)
      params.toDate = to.toISOString()
    }

    const response = await api.get('/ElectricityBill', { params })
    const data = response.data
    const newRecords: FetchedElectricityRecord[] = Array.isArray(data) ? data : (data.items ?? [])
    const total: number = Array.isArray(data) ? newRecords.length : (data.total ?? newRecords.length)

    if (loadMore) {
      historyRecords.value.push(...newRecords)
    } else {
      historyRecords.value = newRecords
    }

    skip.value = historyRecords.value.length
    hasMore.value = historyRecords.value.length < total
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูลไฟฟ้าได้')
    hasMore.value = false
  } finally {
    isLoadingHistory.value = false
  }
}

const handleFilterChange = () => {
  skip.value = 0
  hasMore.value = true
  historyRecords.value = []
  fetchHistory(false)
}

watch([filterDateFrom, filterDateTo, filterBuildingId], handleFilterChange)

onMounted(async () => {
  handleFilterChange()
  try {
    const response = await api.get('/Building')
    buildings.value = response.data.map((b: { id: string; name: string }) => ({ id: b.id, name: b.name }))
  } catch (e) {
    toast.fromError(e, 'ไม่สามารถโหลดข้อมูลอาคารได้')
  }
})

const submitForm = async (): Promise<void> => {
  successMessage.value = ''
  errorMessage.value = ''

  if (
    !formData.value.buildingId ||
    !formData.value.billingCycle ||
    formData.value.peaUnitUsed === null ||
    formData.value.ftRate === null ||
    formData.value.peaAmount === null
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
      buildingId: formData.value.buildingId,
      billingCycle: formData.value.billingCycle ? formData.value.billingCycle.toISOString() : null,
      peaUnitUsed: formData.value.peaUnitUsed || 0,
      peaAmount: formData.value.peaAmount || 0,
      ftRate: formData.value.ftRate || 0,
      recordedBy: authStore.user?.uid || authStore.user?.email || 'unknown',
    }

    await api.post('/ElectricityBill', docData)
    successMessage.value = 'บันทึกข้อมูลบิลค่าไฟฟ้าสำเร็จ'
    formData.value = {
      docReceiveNumber: '',
      docNumber: '',
      buildingId: '',
      billingCycle: null,
      peaUnitUsed: null,
      peaAmount: null,
      ftRate: null,
    }
    handleFilterChange()
  } catch (error: unknown) {
    if (error instanceof Error) errorMessage.value = `เกิดข้อผิดพลาด: ${error.message}`
    else errorMessage.value = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ'
  } finally {
    isSubmitting.value = false
  }
}

const getBuildingName = (id: string): string => buildings.value.find((x) => x.id === id)?.name || id
const formatThaiMonth = (dateStr: string | null | undefined): string => {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'long', day: 'numeric' })
}
const formatCurrency = (val: number | null | undefined): string => {
  if (val === null || val === undefined) return '-'
  return new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val)
}

// Detail / Edit dialog
const selectedRecord = ref<FetchedElectricityRecord | null>(null)
const detailVisible = ref(false)
const editVisible = ref(false)
const isSaving = ref(false)

interface ElectricityEditForm {
  docReceiveNumber: string
  docNumber: string
  buildingId: string
  billingCycle: Date | null
  peaUnitUsed: number | null
  peaAmount: number | null
  ftRate: number | null
}

const editForm = ref<ElectricityEditForm>({
  docReceiveNumber: '', docNumber: '', buildingId: '', billingCycle: null,
  peaUnitUsed: null, peaAmount: null, ftRate: null,
})

const openDetail = (event: { data: FetchedElectricityRecord }) => {
  selectedRecord.value = event.data
  detailVisible.value = true
}

const openEdit = () => {
  if (!selectedRecord.value) return
  const r = selectedRecord.value
  editForm.value = {
    docReceiveNumber: r.docReceiveNumber,
    docNumber: r.docNumber,
    buildingId: r.buildingId,
    billingCycle: r.billingCycle ? new Date(r.billingCycle) : null,
    peaUnitUsed: r.peaUnitUsed,
    peaAmount: r.peaAmount,
    ftRate: r.ftRate,
  }
  detailVisible.value = false
  editVisible.value = true
}

const saveEdit = async () => {
  if (!selectedRecord.value) return
  isSaving.value = true
  try {
    const updatedData = {
      docReceiveNumber: editForm.value.docReceiveNumber,
      docNumber: editForm.value.docNumber,
      buildingId: editForm.value.buildingId,
      billingCycle: editForm.value.billingCycle ? editForm.value.billingCycle.toISOString() : null,
      peaUnitUsed: editForm.value.peaUnitUsed || 0,
      peaAmount: editForm.value.peaAmount || 0,
      ftRate: editForm.value.ftRate || 0,
    }
    await api.put(`/ElectricityBill/${selectedRecord.value.id}`, updatedData)

    const index = historyRecords.value.findIndex(r => r.id === selectedRecord.value!.id)
    if (index !== -1) {
      const item = historyRecords.value[index]
      if (item) {
        historyRecords.value[index] = { ...item, ...updatedData }
      }
    }

    toast.success('บันทึกข้อมูลสำเร็จ')
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
  isSaving.value = true
  try {
    await api.delete(`/ElectricityBill/${selectedRecord.value.id}`)
    historyRecords.value = historyRecords.value.filter(r => r.id !== selectedRecord.value!.id)
    skip.value = historyRecords.value.length
    toast.success('ลบข้อมูลสำเร็จ')
    editVisible.value = false
    detailVisible.value = false
    selectedRecord.value = null
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่')
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div class="max-w-6xl mx-auto pb-10">
    <div class="mb-6">
      <h2 class="text-2xl font-bold text-gray-800">บันทึกบิลค่าไฟฟ้า</h2>
      <p class="text-gray-500 mt-1">บันทึกข้อมูลและยอดชำระจากบิลค่าไฟฟ้าประจำเดือน</p>
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
              <form @submit.prevent="submitForm" class="flex flex-col gap-6">
                <Message v-if="successMessage" severity="success" :closable="true">{{
                  successMessage
                  }}</Message>
                <Message v-if="errorMessage" severity="error" :closable="true">{{
                  errorMessage
                  }}</Message>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">เลขที่รับหน่วยงาน</label>
                    <InputText v-model="formData.docReceiveNumber" placeholder="เช่น ร.123/2567" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">เลขที่หนังสือ</label>
                    <InputText v-model="formData.docNumber" placeholder="เช่น 456/2567" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">เลือกอาคาร/มิเตอร์ <span
                        class="text-red-500">*</span></label><Select v-model="formData.buildingId" :options="buildings"
                      optionLabel="name" optionValue="id" placeholder="-- กรุณาเลือกอาคาร --" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">วันที่ <span
                        class="text-red-500">*</span></label>
                    <DatePicker v-model="formData.billingCycle" dateFormat="dd/mm/yy" showIcon
                      placeholder="-- เลือกวันที่ --" class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">หน่วยไฟฟ้า (Unit) <span class="text-red-500">*</span></label>
                    <InputNumber v-model="formData.peaUnitUsed" :minFractionDigits="0" :maxFractionDigits="2"
                      placeholder="0.00" class="w-full" />
                  </div>

                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">ค่า Ft (บาท/หน่วย) <span class="text-red-500">*</span></label>
                    <InputNumber v-model="formData.ftRate" :minFractionDigits="0" :maxFractionDigits="4"
                      placeholder="0.0000" class="w-full" />
                  </div>

                  <div class="flex flex-col gap-2 md:col-span-2">
                    <label class="font-semibold text-sm text-gray-700">จำนวนเงินค่าไฟฟ้ารวม (บาท) <span
                        class="text-red-500">*</span></label>
                    <InputNumber v-model="formData.peaAmount" mode="currency" currency="THB" locale="th-TH"
                      placeholder="฿ 0.00" class="w-full md:w-1/2" />
                  </div>
                </div>
                <div class="flex justify-end mt-4 border-t pt-6">
                  <Button type="submit" label="บันทึกบิลค่าไฟ" icon="pi pi-save" :loading="isSubmitting" class="px-8" />
                </div>
              </form>
            </template>
          </Card>
        </TabPanel>

        <TabPanel value="1">
          <Card class="shadow-sm border-none mt-2 overflow-hidden">
            <template #content>
              <!-- Filters -->
              <div class="mb-4">
                <h3 class="font-bold text-gray-700 border-b pb-2 mb-4">
                  <i class="pi pi-filter mr-2 text-blue-500"></i>ตัวกรองข้อมูล
                </h3>
                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 items-end">
                  <div class="flex flex-col gap-2">
                    <label class="text-sm font-semibold text-gray-700">รอบบิลเริ่มต้น</label>
                    <DatePicker v-model="filterDateFrom" view="month" dateFormat="MM yy" showIcon class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="text-sm font-semibold text-gray-700">รอบบิลสิ้นสุด</label>
                    <DatePicker v-model="filterDateTo" view="month" dateFormat="MM yy" showIcon class="w-full" />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="text-sm font-semibold text-gray-700">อาคาร</label>
                    <Select v-model="filterBuildingId" :options="[{ id: '', name: 'ทั้งหมด' }, ...buildings]"
                      optionLabel="name" optionValue="id" class="w-full" />
                  </div>
                  <div class="flex items-end">
                    <Button label="ล้างตัวกรอง" icon="pi pi-times" severity="secondary" text @click="clearFilters" />
                  </div>
                </div>
              </div>

              <DataTable :value="historyRecords" :loading="isLoadingHistory" stripedRows responsiveLayout="scroll"
                emptyMessage="ยังไม่มีข้อมูล" selectionMode="single" @row-click="openDetail" class="cursor-pointer">
                <Column header="อาคาร/มิเตอร์">
                  <template #body="sp">
                    <span class="font-semibold">{{ getBuildingName(sp.data.buildingId) }}</span>
                  </template>
                </Column>
                <Column header="วันที่">
                  <template #body="sp">{{ formatThaiMonth(sp.data.billingCycle) }}</template>
                </Column>
                <Column header="รายละเอียด">
                  <template #body="sp">
                    <div class="text-gray-600 text-sm">เลข: {{ sp.data.docNumber || '-' }}</div>
                    <div class="text-xs text-orange-500 mt-1" v-if="sp.data.ftRate">
                      Ft: {{ sp.data.ftRate }} บ./หน่วย
                    </div>
                  </template>
                </Column>
                <Column header="ยอดรวม">
                  <template #body="sp">
                    <div class="font-bold text-blue-600">{{ formatCurrency(sp.data.peaAmount) }}</div>
                    <div class="text-xs text-gray-500">{{ sp.data.peaUnitUsed || 0 }} kWh</div>
                  </template>
                </Column>
                <Column header="" style="width: 3rem">
                  <template #body><i class="pi pi-chevron-right text-gray-400"></i></template>
                </Column>
              </DataTable>
              <div class="flex justify-center mt-4">
                <Button v-if="hasMore" label="โหลดเพิ่มเติม" icon="pi pi-chevron-down" severity="secondary" outlined
                  :loading="isLoadingHistory" @click="fetchHistory(true)" />
                <p v-else-if="historyRecords.length > 0" class="text-xs text-gray-400 py-2">
                  แสดงทั้งหมด {{ historyRecords.length }} รายการ
                </p>
              </div>
            </template>
          </Card>

          <!-- Detail Dialog -->
          <Dialog v-model:visible="detailVisible" modal header="รายละเอียดบิลค่าไฟฟ้า" :style="{ width: '520px' }"
            :draggable="false">
            <div v-if="selectedRecord" class="flex flex-col gap-4">
              <div class="grid grid-cols-2 gap-3 text-sm">
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">อาคาร/มิเตอร์</p>
                  <p class="font-semibold text-gray-800">{{ getBuildingName(selectedRecord.buildingId) }}</p>
                </div>
                <div class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">วันที่</p>
                  <p class="font-semibold text-gray-800">{{ formatThaiMonth(selectedRecord.billingCycle) }}</p>
                </div>
                <div v-if="selectedRecord.docReceiveNumber" class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">เลขที่รับหน่วยงาน</p>
                  <p class="font-semibold text-gray-800">{{ selectedRecord.docReceiveNumber }}</p>
                </div>
                <div v-if="selectedRecord.docNumber" class="bg-gray-50 rounded-lg p-3">
                  <p class="text-gray-500 text-xs mb-1">เลขที่หนังสือ</p>
                  <p class="font-semibold text-gray-800">{{ selectedRecord.docNumber }}</p>
                </div>
              </div>
              <div class="bg-blue-50 rounded-xl p-4 border border-blue-100">
                <p class="font-bold text-blue-800 mb-3"><i class="pi pi-bolt mr-2"></i>รายละเอียดการใช้ไฟฟ้า</p>
                <div class="flex flex-col gap-2 text-sm">
                  <div class="flex justify-between">
                    <span class="text-gray-600">หน่วยที่ใช้</span>
                    <span class="font-semibold">{{ selectedRecord.peaUnitUsed || 0 }} kWh</span>
                  </div>
                  <div v-if="selectedRecord.ftRate" class="flex justify-between">
                    <span class="text-gray-600">ค่า Ft</span>
                    <span class="font-semibold">{{ selectedRecord.ftRate }} บ./หน่วย</span>
                  </div>
                  <div class="flex justify-between border-t border-blue-200 pt-2 mt-1">
                    <span class="font-bold text-blue-800">ยอดรวม</span>
                    <span class="font-bold text-blue-700 text-lg">{{ formatCurrency(selectedRecord.peaAmount) }}</span>
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
          <Dialog v-model:visible="editVisible" modal header="แก้ไขบิลค่าไฟฟ้า" :style="{ width: '580px' }"
            :draggable="false">
            <div class="flex flex-col gap-4">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">อาคาร/มิเตอร์</label>
                  <Select v-model="editForm.buildingId" :options="buildings" optionLabel="name" optionValue="id"
                    class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">วันที่</label>
                  <DatePicker v-model="editForm.billingCycle" dateFormat="dd/mm/yy" class="w-full" showIcon />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">เลขที่รับหน่วยงาน</label>
                  <InputText v-model="editForm.docReceiveNumber" class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">เลขที่หนังสือ</label>
                  <InputText v-model="editForm.docNumber" class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">หน่วยไฟฟ้า (Unit)</label>
                  <InputNumber v-model="editForm.peaUnitUsed" :minFractionDigits="0" :maxFractionDigits="2"
                    class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                  <label class="text-sm font-semibold text-gray-700">ค่า Ft (บาท/หน่วย)</label>
                  <InputNumber v-model="editForm.ftRate" :minFractionDigits="0" :maxFractionDigits="4" class="w-full" />
                </div>
                <div class="flex flex-col gap-2 sm:col-span-2">
                  <label class="text-sm font-semibold text-gray-700">จำนวนเงินค่าไฟฟ้ารวม (บาท)</label>
                  <InputNumber v-model="editForm.peaAmount" mode="currency" currency="THB" locale="th-TH"
                    class="w-full sm:w-1/2" />
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
</style>
