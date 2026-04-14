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

defineOptions({ name: 'PostalOutgoingSystem' })

const toast = useAppToast()
const authStore = useAuthStore()

interface OutgoingPostalRecordForm {
  departmentId: string
  recordMonth: Date | null
  normalMail: number | null
  normalMailUnitPrice: number | null
  registeredMail: number | null
  registeredMailUnitPrice: number | null
  emsMail: number | null
  emsMailUnitPrice: number | null
}

interface FetchedPostalRecord {
  id: string
  departmentId: string
  recordMonth: string | null
  normalMail: number
  normalMailUnitPrice: number
  registeredMail: number
  registeredMailUnitPrice: number
  emsMail: number
  emsMailUnitPrice: number
  totalMail: number
  recordedByName: string
  createdAt: string
}

interface Department {
  id: string
  name: string
}

const { isSuperAdmin, isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('postal')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')
const departments = ref<Department[]>([])

const outgoingFormData = ref<OutgoingPostalRecordForm>({
  departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
  recordMonth: null,
  normalMail: null,
  normalMailUnitPrice: null,
  registeredMail: null,
  registeredMailUnitPrice: null,
  emsMail: null,
  emsMailUnitPrice: null,
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

const computedTotalMail = computed<number>(() => {
  return (
    (outgoingFormData.value.normalMail || 0) +
    (outgoingFormData.value.registeredMail || 0) +
    (outgoingFormData.value.emsMail || 0)
  )
})

const computedNormalCost = computed<number>(() => outgoingFormData.value.normalMailUnitPrice || 0)
const computedRegisteredCost = computed<number>(() => outgoingFormData.value.registeredMailUnitPrice || 0)
const computedEmsCost = computed<number>(() => outgoingFormData.value.emsMailUnitPrice || 0)
const computedTotalCost = computed<number>(
  () => computedNormalCost.value + computedRegisteredCost.value + computedEmsCost.value,
)
const formatMoney = (value: number): string =>
  value.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

const getDeptName = (id: string): string => departments.value.find((x) => x.id === id)?.name || id
const formatThaiMonth = (dateStr: string | null | undefined): string => {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'long' })
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
      recordType: 'outgoing',
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

const submitOutgoingForm = async (): Promise<void> => {
  successMessage.value = ''
  errorMessage.value = ''

  if (!outgoingFormData.value.recordMonth || !outgoingFormData.value.departmentId) {
    errorMessage.value = 'กรุณากรอกข้อมูลรอบเดือน และหน่วยงานให้ครบถ้วน'
    return
  }

  try {
    isSubmitting.value = true

    const saveDeptId = isSuperAdmin.value
      ? outgoingFormData.value.departmentId
      : currentUserDepartment.value

    const docData = {
      ...outgoingFormData.value,
      departmentId: saveDeptId,
      incomingNormalMail: 0,
      incomingRegisteredMail: 0,
      incomingEmsMail: 0,
      incomingTotalMail: 0,
      normalMailUnitPrice: outgoingFormData.value.normalMailUnitPrice || 0,
      registeredMailUnitPrice: outgoingFormData.value.registeredMailUnitPrice || 0,
      emsMailUnitPrice: outgoingFormData.value.emsMailUnitPrice || 0,
      totalMail: computedTotalMail.value,
      recordedByName: authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ',
      recordedBy: authStore.user?.uid || 'unknown',
    }

    await api.post('/PostalRecord', docData)

    successMessage.value = 'บันทึกสถิติไปรษณีย์ออกสำเร็จ'
    await fetchHistory(true)

    outgoingFormData.value.normalMail = null
    outgoingFormData.value.normalMailUnitPrice = null
    outgoingFormData.value.registeredMail = null
    outgoingFormData.value.registeredMailUnitPrice = null
    outgoingFormData.value.emsMail = null
    outgoingFormData.value.emsMailUnitPrice = null
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? `เกิดข้อผิดพลาด: ${error.message}` : 'เกิดข้อผิดพลาด'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="max-w-6xl mx-auto pb-10">
    <div class="mb-6 flex flex-col sm:flex-row justify-between sm:items-end gap-4">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          <i class="pi pi-send text-blue-500 mr-2"></i>บันทึกสถิติไปรษณีย์ออก
        </h2>
        <p class="text-gray-500 mt-1">ระบบไปรษณีย์ออก แยกจากไปรษณีย์เข้าหน่วยงาน</p>
      </div>
    </div>

    <Tabs value="0" lazy>
      <TabList>
        <Tab value="0"><i class="pi pi-file-edit mr-2"></i>บันทึกข้อมูล</Tab>
        <Tab value="1"><i class="pi pi-history mr-2"></i>ประวัติไปรษณีย์ออก</Tab>
      </TabList>

      <TabPanels>
        <TabPanel value="0">
          <Card class="shadow-sm border-none mt-2">
            <template #content>
              <form @submit.prevent="submitOutgoingForm" class="flex flex-col gap-8">
                <Message v-if="successMessage" severity="success" :closable="true">{{ successMessage }}</Message>
                <Message v-if="errorMessage" severity="error" :closable="true">{{ errorMessage }}</Message>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-4 bg-gray-50 p-4 rounded-xl border border-gray-100">
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">รอบเดือนที่จัดส่ง <span class="text-red-500">*</span></label>
                    <DatePicker v-model="outgoingFormData.recordMonth" view="month" dateFormat="MM yy" class="w-full" showIcon />
                  </div>
                  <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">หน่วยงานผู้จัดส่ง <span class="text-red-500">*</span></label>
                    <Select
                      v-if="isSuperAdmin"
                      v-model="outgoingFormData.departmentId"
                      :options="departments"
                      optionLabel="name"
                      optionValue="id"
                      placeholder="-- เลือกหน่วยงาน --"
                      class="w-full"
                    />
                    <InputText v-else :value="getDeptName(currentUserDepartment)" disabled class="w-full bg-gray-100 font-bold text-gray-600" />
                  </div>
                </div>

                <div class="bg-blue-50/40 p-5 rounded-xl border border-blue-100">
                  <h3 class="font-bold text-blue-800 border-b border-blue-200 pb-2 mb-4 text-lg">
                    <i class="pi pi-box mr-2"></i>รายละเอียดจำนวนการจัดส่ง
                  </h3>
                  <div class="mb-4">
                    <p class="text-xs text-blue-700">กรอกข้อมูลแยกตามประเภท: จำนวน (ชิ้น) และราคา (รวมทั้งหมด)</p>
                  </div>

                  <div class="grid grid-cols-1 xl:grid-cols-12 gap-4">
                    <div class="xl:col-span-8 space-y-3">
                      <div class="hidden md:grid md:grid-cols-12 gap-3 text-xs font-bold text-gray-500 px-2">
                        <div class="md:col-span-4">ประเภทไปรษณีย์</div>
                        <div class="md:col-span-3 text-right">จำนวน (ชิ้น)</div>
                        <div class="md:col-span-3 text-right">ราคา (รวมทั้งหมด)</div>
                        <div class="md:col-span-2 text-right">รวมเงิน (บาท)</div>
                      </div>

                      <div class="grid grid-cols-12 gap-3 items-center bg-white rounded-lg border border-blue-100 p-3">
                        <div class="col-span-12 md:col-span-4"><p class="font-semibold text-gray-700">ไปรษณีย์ธรรมดา</p></div>
                        <div class="col-span-6 md:col-span-3"><InputNumber v-model="outgoingFormData.normalMail" :min="0" placeholder="0" class="w-full" /></div>
                        <div class="col-span-6 md:col-span-3">
                          <InputNumber v-model="outgoingFormData.normalMailUnitPrice" :min="0" :minFractionDigits="2" :maxFractionDigits="2" placeholder="0.00" class="w-full" />
                        </div>
                        <div class="col-span-12 md:col-span-2 text-right text-sm font-bold text-blue-700">{{ formatMoney(computedNormalCost) }}</div>
                      </div>

                      <div class="grid grid-cols-12 gap-3 items-center bg-white rounded-lg border border-blue-100 p-3">
                        <div class="col-span-12 md:col-span-4"><p class="font-semibold text-gray-700">ไปรษณีย์ลงทะเบียน</p></div>
                        <div class="col-span-6 md:col-span-3"><InputNumber v-model="outgoingFormData.registeredMail" :min="0" placeholder="0" class="w-full" /></div>
                        <div class="col-span-6 md:col-span-3">
                          <InputNumber v-model="outgoingFormData.registeredMailUnitPrice" :min="0" :minFractionDigits="2" :maxFractionDigits="2" placeholder="0.00" class="w-full" />
                        </div>
                        <div class="col-span-12 md:col-span-2 text-right text-sm font-bold text-blue-700">{{ formatMoney(computedRegisteredCost) }}</div>
                      </div>

                      <div class="grid grid-cols-12 gap-3 items-center bg-white rounded-lg border border-blue-100 p-3">
                        <div class="col-span-12 md:col-span-4"><p class="font-semibold text-gray-700">ไปรษณีย์ EMS</p></div>
                        <div class="col-span-6 md:col-span-3"><InputNumber v-model="outgoingFormData.emsMail" :min="0" placeholder="0" class="w-full" /></div>
                        <div class="col-span-6 md:col-span-3">
                          <InputNumber v-model="outgoingFormData.emsMailUnitPrice" :min="0" :minFractionDigits="2" :maxFractionDigits="2" placeholder="0.00" class="w-full" />
                        </div>
                        <div class="col-span-12 md:col-span-2 text-right text-sm font-bold text-blue-700">{{ formatMoney(computedEmsCost) }}</div>
                      </div>
                    </div>

                    <div class="xl:col-span-4">
                      <div class="bg-blue-100 p-4 rounded-lg border border-blue-200 xl:sticky xl:top-4">
                        <h4 class="font-bold text-blue-800 mb-3">สรุปข้อมูลรายการนี้</h4>
                        <div class="space-y-3">
                          <div>
                            <label class="font-semibold text-sm text-blue-800">รวมทั้งหมด (ชิ้น)</label>
                            <InputNumber :modelValue="computedTotalMail" readonly class="w-full" inputClass="bg-transparent border-none text-right font-black text-blue-700 text-xl p-0" />
                          </div>
                          <div>
                            <label class="font-semibold text-sm text-blue-800">รวมค่าใช้จ่าย (บาท)</label>
                            <InputNumber :modelValue="computedTotalCost" readonly :minFractionDigits="2" :maxFractionDigits="2" class="w-full" inputClass="bg-transparent border-none text-right font-black text-blue-700 text-xl p-0" />
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                <div class="flex justify-end mt-2 pt-4 border-t border-gray-100">
                  <Button type="submit" label="บันทึกสถิติไปรษณีย์ออก" icon="pi pi-save" severity="info" :loading="isSubmitting" class="px-8 py-3 text-lg" />
                </div>
              </form>
            </template>
          </Card>
        </TabPanel>

        <TabPanel value="1">
          <Card class="shadow-sm border-none mt-2 overflow-hidden">
            <template #content>
              <DataTable :value="historyRecords" :loading="isLoadingHistory" stripedRows responsiveLayout="scroll" emptyMessage="ยังไม่มีข้อมูลไปรษณีย์ออก">
                <Column v-if="isAdmin" header="หน่วยงาน">
                  <template #body="sp">
                    <div class="font-bold text-gray-700">{{ getDeptName(sp.data.departmentId) }}</div>
                    <div class="text-xs text-gray-500"><i class="pi pi-user mr-1"></i>{{ sp.data.recordedByName }}</div>
                  </template>
                </Column>

                <Column header="รอบเดือน">
                  <template #body="sp">
                    <div class="font-semibold text-gray-800"><i class="pi pi-calendar mr-2"></i>{{ formatThaiMonth(sp.data.recordMonth) }}</div>
                  </template>
                </Column>

                <Column header="ธรรมดา" class="text-center">
                  <template #body="sp">
                    <div>{{ sp.data.normalMail }} ชิ้น</div>
                    <div class="text-xs text-gray-500">ราคา: {{ (sp.data.normalMailUnitPrice || 0).toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} บาท</div>
                  </template>
                </Column>

                <Column header="ลงทะเบียน" class="text-center">
                  <template #body="sp">
                    <div>{{ sp.data.registeredMail }} ชิ้น</div>
                    <div class="text-xs text-gray-500">ราคา: {{ (sp.data.registeredMailUnitPrice || 0).toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} บาท</div>
                  </template>
                </Column>

                <Column header="EMS" class="text-center">
                  <template #body="sp">
                    <div class="text-rose-600 font-semibold">{{ sp.data.emsMail }} ชิ้น</div>
                    <div class="text-xs text-gray-500">ราคา: {{ (sp.data.emsMailUnitPrice || 0).toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} บาท</div>
                  </template>
                </Column>

                <Column header="รวมทั้งหมด" class="text-center">
                  <template #body="sp">
                    <div class="font-bold text-blue-600 text-lg">{{ sp.data.totalMail }} ชิ้น</div>
                    <div class="text-xs text-gray-600">
                      {{ ((sp.data.normalMailUnitPrice || 0) + (sp.data.registeredMailUnitPrice || 0) + (sp.data.emsMailUnitPrice || 0)).toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} บาท
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
