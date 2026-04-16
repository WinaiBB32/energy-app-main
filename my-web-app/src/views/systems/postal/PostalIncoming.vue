<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import InputNumber from 'primevue/inputnumber'
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
import InputText from 'primevue/inputtext'
import Dialog from 'primevue/dialog'

defineOptions({ name: 'PostalIncomingSystem' })

const toast = useAppToast()
const authStore = useAuthStore()

interface IncomingPostalRecordForm {
  departmentId: string
  recordMonth: Date | null
  totalMail: number | null
}

interface FetchedPostalRecord {
  id: string
  departmentId: string
  recordMonth: string | null
  incomingNormalMail: number
  incomingRegisteredMail: number
  incomingEmsMail: number
  incomingTotalMail: number
  recordedByName: string
  createdAt: string
}

interface Department {
  id: string
  name: string
}

const { isSuperAdmin, isOfficer, isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('postal')
const canEdit = computed(() => isSuperAdmin.value || isOfficer.value || isAdmin)
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')
const departments = ref<Department[]>([])

const incomingFormData = ref<IncomingPostalRecordForm>({
  departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
  recordMonth: null,
  totalMail: null,
})

const isSubmitting = ref<boolean>(false)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')

const historyRecords = ref<FetchedPostalRecord[]>([])
const isLoadingHistory = ref<boolean>(true)
const isLoadingMore = ref<boolean>(false)
const hasMoreData = ref<boolean>(true)
const page = ref(0)
const FETCH_LIMIT = 15

// ไม่ต้องคำนวณรวม เพราะเหลือฟิลด์เดียว

const getDeptName = (id: string): string => departments.value.find((x) => x.id === id)?.name || id
const formatThaiDate = (dateStr: string | null | undefined): string => {
  if (!dateStr) return '-'
  const utcStr = /Z|[+-]\d{2}:\d{2}$/.test(dateStr) ? dateStr : dateStr + 'Z'
  return new Date(utcStr).toLocaleDateString('th-TH', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })
}

onMounted(async () => {
  try {
    const { data } = await api.get('/Department')
    departments.value = Array.isArray(data) ? data : data?.items || []
    await fetchHistory(true)
  } catch {
    toast.error('ไม่สามารถโหลดข้อมูลเริ่มต้นได้')
  } finally {
    isLoadingHistory.value = false
  }
})

const fetchHistory = async (isFirstPage: boolean = false) => {
  if (isFirstPage) {
    isLoadingHistory.value = true
    page.value = 0
    historyRecords.value = []
    hasMoreData.value = true
  } else {
    if (!hasMoreData.value) return
    isLoadingMore.value = true
    page.value++
  }

  try {
    const params = {
      skip: page.value * FETCH_LIMIT,
      take: FETCH_LIMIT,
      recordType: 'incoming',
    }
    const { data } = await api.get('/PostalRecord', { params })
    const newRecords = data.items || []

    if (isFirstPage) {
      historyRecords.value = newRecords
    } else {
      historyRecords.value.push(...newRecords)
    }

    hasMoreData.value = newRecords.length === FETCH_LIMIT
  } catch {
    toast.error('ไม่สามารถโหลดข้อมูลประวัติได้')
  } finally {
    isLoadingHistory.value = false
    isLoadingMore.value = false
  }
}

const submitIncomingForm = async (): Promise<void> => {
  successMessage.value = ''
  errorMessage.value = ''

  if (!incomingFormData.value.recordMonth || !incomingFormData.value.departmentId || incomingFormData.value.totalMail == null) {
    errorMessage.value = 'กรุณากรอกข้อมูลให้ครบถ้วน'
    return
  }

  try {
    isSubmitting.value = true

    const saveDeptId = isSuperAdmin.value
      ? incomingFormData.value.departmentId
      : currentUserDepartment.value


    const selectedMonth = incomingFormData.value.recordMonth!
    const recordMonthIso = new Date(
      Date.UTC(selectedMonth.getFullYear(), selectedMonth.getMonth(), selectedMonth.getDate())
    ).toISOString()

    const docData = {
      departmentId: saveDeptId,
      recordMonth: recordMonthIso,
      incomingTotalMail: incomingFormData.value.totalMail || 0,
      recordedByName: authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ',
      recordedBy: authStore.user?.uid || 'unknown',
    }

    await api.post('/PostalRecord', docData)

    successMessage.value = 'บันทึกสถิติไปรษณีย์เข้าหน่วยงานสำเร็จ'
    await fetchHistory(true)

    incomingFormData.value.totalMail = null
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? `เกิดข้อผิดพลาด: ${error.message}` : 'เกิดข้อผิดพลาด'
  } finally {
    isSubmitting.value = false
  }
}

// ─── Edit / Delete ────────────────────────────────────────────────────────────
const editVisible = ref(false)
const deleteConfirmVisible = ref(false)
const selectedRecord = ref<FetchedPostalRecord | null>(null)
const recordToDelete = ref<FetchedPostalRecord | null>(null)
const isUpdating = ref(false)

const editForm = ref({
  recordMonth: null as Date | null,
  incomingTotalMail: 0,
})

const openEdit = (record: FetchedPostalRecord) => {
  selectedRecord.value = record
  editForm.value = {
    recordMonth: record.recordMonth ? new Date(
      /Z|[+-]\d{2}:\d{2}$/.test(record.recordMonth) ? record.recordMonth : record.recordMonth + 'Z'
    ) : null,
    incomingTotalMail: record.incomingTotalMail,
  }
  editVisible.value = true
}

const saveEdit = async () => {
  if (!selectedRecord.value || !editForm.value.recordMonth) {
    toast.error('กรุณาเลือกรอบเดือน'); return
  }
  isUpdating.value = true
  try {
    const m = editForm.value.recordMonth
    const recordMonthIso = new Date(Date.UTC(m.getFullYear(), m.getMonth(), m.getDate())).toISOString()
    await api.put(`/PostalRecord/${selectedRecord.value.id}`, {
      departmentId: selectedRecord.value.departmentId,
      recordMonth: recordMonthIso,
      incomingTotalMail: editForm.value.incomingTotalMail,
      normalMail: 0, normalMailUnitPrice: 0,
      registeredMail: 0, registeredMailUnitPrice: 0,
      emsMail: 0, emsMailUnitPrice: 0,
      incomingNormalMail: 0, incomingRegisteredMail: 0, incomingEmsMail: 0,
      totalMail: 0,
      recordedByName: selectedRecord.value.recordedByName,
      recordedBy: authStore.user?.uid || 'unknown',
    })
    const idx = historyRecords.value.findIndex(r => r.id === selectedRecord.value!.id)
    if (idx !== -1) {
      historyRecords.value[idx] = {
        ...historyRecords.value[idx]!,
        recordMonth: recordMonthIso,
        incomingTotalMail: editForm.value.incomingTotalMail,
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

const confirmDelete = (record: FetchedPostalRecord) => {
  recordToDelete.value = record
  deleteConfirmVisible.value = true
}

const deleteRecord = async () => {
  if (!recordToDelete.value) return
  try {
    await api.delete(`/PostalRecord/${recordToDelete.value.id}`)
    historyRecords.value = historyRecords.value.filter(r => r.id !== recordToDelete.value!.id)
    toast.success('ลบข้อมูลสำเร็จ')
  } catch (e: unknown) {
    toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่')
  } finally {
    deleteConfirmVisible.value = false
    recordToDelete.value = null
  }
}
</script>

<template>
  <div class="max-w-6xl mx-auto pb-10">
    <div class="mb-6 flex flex-col sm:flex-row justify-between sm:items-end gap-4">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          <i class="pi pi-inbox text-emerald-500 mr-2"></i>บันทึกไปรษณีย์เข้าหน่วยงาน
        </h2>
        <p class="text-gray-500 mt-1">ระบบไปรษณีย์เข้า แยกจากไปรษณีย์ออกโดยสมบูรณ์</p>
      </div>
    </div>

    <Tabs value="0" lazy>
      <TabList>
        <Tab value="0"><i class="pi pi-file-edit mr-2"></i>บันทึกข้อมูล</Tab>
        <Tab value="1"><i class="pi pi-history mr-2"></i>ประวัติไปรษณีย์เข้า</Tab>
      </TabList>

      <TabPanels>
        <TabPanel value="0">
          <Card class="shadow-sm border-none mt-2">
            <template #content>
              <form @submit.prevent="submitIncomingForm" class="flex flex-col gap-8">
                <Message v-if="successMessage" severity="success" :closable="true">{{ successMessage }}</Message>
                <Message v-if="errorMessage" severity="error" :closable="true">{{ errorMessage }}</Message>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-4 bg-gray-50 p-4 rounded-xl border border-gray-100">
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">วันที่รับเข้า <span class="text-red-500">*</span></label>
                    <DatePicker v-model="incomingFormData.recordMonth" dateFormat="dd/mm/yy" class="w-full" showIcon />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">หน่วยงานผู้รับ <span class="text-red-500">*</span></label>
                    <Select
                      v-if="isSuperAdmin"
                      v-model="incomingFormData.departmentId"
                      :options="departments"
                      optionLabel="name"
                      optionValue="id"
                      placeholder="-- เลือกหน่วยงาน --"
                      class="w-full"
                    />
                    <InputText v-else :value="getDeptName(currentUserDepartment)" disabled class="w-full bg-gray-100 font-bold text-gray-600" />
                  </div>
                </div>

                <div class="bg-emerald-50/40 p-5 rounded-xl border border-emerald-100">
                  <h3 class="font-bold text-emerald-800 border-b border-emerald-200 pb-2 mb-4 text-lg">
                    <i class="pi pi-inbox mr-2"></i>รายละเอียดไปรษณีย์เข้าหน่วยงาน
                  </h3>
                  <p class="text-xs text-emerald-700 mb-4">บันทึกจำนวนไปรษณีย์ที่หน่วยงานได้รับ</p>
                  <div class="flex flex-col gap-2 max-w-xs">
                    <label class="font-semibold text-sm text-gray-700">จำนวนไปรษณีย์ที่ได้รับ (ชิ้น) <span class="text-red-500">*</span></label>
                    <InputNumber v-model="incomingFormData.totalMail" :min="0" placeholder="0" class="w-full" />
                  </div>
                </div>

                <div class="flex justify-end mt-2 pt-4 border-t border-gray-100">
                  <Button type="submit" label="บันทึกไปรษณีย์เข้าหน่วยงาน" icon="pi pi-save" severity="success" :loading="isSubmitting" class="px-8 py-3 text-lg" />
                </div>
              </form>
            </template>
          </Card>
        </TabPanel>

        <TabPanel value="1">
          <Card class="shadow-sm border-none mt-2 overflow-hidden">
            <template #content>
              <DataTable :value="historyRecords" :loading="isLoadingHistory" stripedRows responsiveLayout="scroll" emptyMessage="ยังไม่มีข้อมูลไปรษณีย์เข้า">
                <Column v-if="isAdmin" header="หน่วยงาน">
                  <template #body="sp">
                    <div class="font-bold text-gray-700">{{ getDeptName(sp.data.departmentId) }}</div>
                    <div class="text-xs text-gray-500"><i class="pi pi-user mr-1"></i>{{ sp.data.recordedByName }}</div>
                  </template>
                </Column>

                <Column header="รอบเดือน">
                  <template #body="sp">
                    <div class="font-semibold text-gray-800"><i class="pi pi-calendar mr-2"></i>{{ formatThaiDate(sp.data.recordMonth) }}</div>
                  </template>
                </Column>

                <Column header="ธรรมดา" class="text-center">
                  <template #body="sp"><div>{{ sp.data.incomingNormalMail || 0 }} ชิ้น</div></template>
                </Column>

                <Column header="ลงทะเบียน" class="text-center">
                  <template #body="sp"><div>{{ sp.data.incomingRegisteredMail || 0 }} ชิ้น</div></template>
                </Column>

                <Column header="EMS" class="text-center">
                  <template #body="sp"><div class="text-rose-600 font-semibold">{{ sp.data.incomingEmsMail || 0 }} ชิ้น</div></template>
                </Column>

                <Column header="รวมทั้งหมด" class="text-center">
                  <template #body="sp"><div class="font-bold text-emerald-600 text-lg">{{ sp.data.incomingTotalMail || 0 }} ชิ้น</div></template>
                </Column>

                <Column v-if="canEdit" header="จัดการ" style="width: 90px">
                  <template #body="sp">
                    <div class="flex gap-1">
                      <Button icon="pi pi-pencil" text rounded severity="secondary" size="small"
                        v-tooltip.top="'แก้ไข'" @click="openEdit(sp.data)" />
                      <Button icon="pi pi-trash" text rounded severity="danger" size="small"
                        v-tooltip.top="'ลบ'" @click="confirmDelete(sp.data)" />
                    </div>
                  </template>
                </Column>
              </DataTable>

              <div v-if="hasMoreData" class="flex justify-center mt-6 mb-2">
                <Button label="โหลดข้อมูลเพิ่มเติม" icon="pi pi-refresh" severity="secondary" outlined size="small" @click="fetchHistory(false)" :loading="isLoadingMore" />
              </div>
              <div v-else-if="historyRecords.length > 0" class="text-center text-xs text-gray-400 mt-6 mb-2">แสดงข้อมูลครบทั้งหมดแล้ว</div>
            </template>
          </Card>
        </TabPanel>
      </TabPanels>
    </Tabs>

    <!-- Edit Dialog -->
    <Dialog v-model:visible="editVisible" modal header="แก้ไขข้อมูลไปรษณีย์เข้า" :style="{ width: '400px' }" :draggable="false">
      <div class="flex flex-col gap-4 mt-2">
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">รอบเดือน <span class="text-red-500">*</span></label>
          <DatePicker v-model="editForm.recordMonth" dateFormat="dd/mm/yy" class="w-full" showIcon />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">จำนวนไปรษณีย์เข้า (ชิ้น)</label>
          <InputNumber v-model="editForm.incomingTotalMail" :min="0" class="w-full" />
        </div>
      </div>
      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="editVisible = false" />
        <Button label="บันทึกการแก้ไข" icon="pi pi-check" severity="success" :loading="isUpdating" @click="saveEdit" />
      </template>
    </Dialog>

    <!-- Delete Confirm Dialog -->
    <Dialog v-model:visible="deleteConfirmVisible" modal header="ยืนยันการลบ" :style="{ width: '360px' }" :draggable="false">
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 bg-red-100 rounded-full flex items-center justify-center shrink-0">
          <i class="pi pi-exclamation-triangle text-red-500"></i>
        </div>
        <p class="text-gray-700">ต้องการลบข้อมูลไปรษณีย์เข้า รอบ <strong>{{ formatThaiDate(recordToDelete?.recordMonth) }}</strong> หรือไม่?</p>
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

:deep(.bg-transparent) {
  background-color: transparent !important;
  cursor: default;
  box-shadow: none;
}
</style>
