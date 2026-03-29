<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

defineOptions({ name: 'TelephoneSystem' })

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
import Textarea from 'primevue/textarea'
import Dialog from 'primevue/dialog'

// 1. Interfaces
interface TelephoneRecord {
    docReceiveNumber: string
    docNumber: string
    billingCycle: Date | null
    phoneNumber: string
    providerName: string
    usageAmount: number | null
    vatAmount: number | null
    totalAmount: number | null
    recordedBy: string
}

interface FetchedTelephoneRecord {
    id: string
    docReceiveNumber: string
    docNumber: string
    billingCycle: string | null
    phoneNumber: string
    providerName: string
    usageAmount: number
    vatAmount: number
    totalAmount: number
    recordedBy: string
    createdAt: string
}

interface Department {
    id: string
    name: string
}

const authStore = useAuthStore()
const { isSuperAdmin, isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('telephone')

const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const departments = ref<Department[]>([])

const formData = ref<TelephoneRecord>({
    docReceiveNumber: '',
    docNumber: '',
    billingCycle: null,
    phoneNumber: '',
    providerName: '',
    usageAmount: null,
    vatAmount: null,
    totalAmount: null,
    recordedBy: ''
})

const isSubmitting = ref<boolean>(false)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')

const historyRecords = ref<FetchedTelephoneRecord[]>([])
const isLoadingHistory = ref<boolean>(true)
const isLoadingMore = ref<boolean>(false)
const hasMore = ref<boolean>(false)
const skip = ref<number>(0)
const take = 20

// 3. Auto-calculate VAT 7% and total
const computedVatAmount = computed<number>(() => {
    return (formData.value.usageAmount || 0) * 0.07
})

const computedTotalAmount = computed<number>(() => {
    return (formData.value.usageAmount || 0) + computedVatAmount.value
})

// Edit form computed
const editSubTotal = computed(() => editForm.value.usageAmount || 0)
const editVat = computed(() => editSubTotal.value * 0.07)
const editTotal = computed(() => editSubTotal.value + editVat.value)

// 4. Fetch history
const fetchHistory = async (loadMore = false): Promise<void> => {
    if (loadMore) isLoadingMore.value = true
    else { isLoadingHistory.value = true; historyRecords.value = []; skip.value = 0 }
    try {
        const params: Record<string, unknown> = { skip: loadMore ? skip.value : 0, take }
        const response = await api.get('/TelephoneRecord', { params })
        const records: FetchedTelephoneRecord[] = response.data
        if (loadMore) historyRecords.value.push(...records)
        else historyRecords.value = records
        hasMore.value = records.length >= take
        if (loadMore) skip.value += records.length
        else skip.value = records.length
    } catch (error: unknown) {
        toast.fromError(error, 'ไม่สามารถโหลดข้อมูลค่าโทรศัพท์ได้')
    } finally {
        isLoadingHistory.value = false
        isLoadingMore.value = false
    }
}

onMounted(async () => {
    try {
        const deptRes = await api.get('/Department')
        departments.value = deptRes.data
    } catch (error: unknown) {
        toast.fromError(error, 'ไม่สามารถโหลดรายชื่อหน่วยงานได้')
    }
    fetchHistory()
})

// 5. Submit
const submitForm = async (): Promise<void> => {
    successMessage.value = ''
    errorMessage.value = ''

    if (!formData.value.billingCycle) {
        errorMessage.value = 'กรุณากรอกข้อมูลที่จำเป็น (*) ให้ครบถ้วน'
        return
    }

    try {
        isSubmitting.value = true

        const payload = {
            docReceiveNumber: formData.value.docReceiveNumber,
            docNumber: formData.value.docNumber,
            billingCycle: formData.value.billingCycle.toISOString(),
            phoneNumber: formData.value.phoneNumber,
            providerName: formData.value.providerName,
            usageAmount: formData.value.usageAmount || 0,
            vatAmount: computedVatAmount.value,
            totalAmount: computedTotalAmount.value,
            recordedBy: authStore.user?.uid || '',
        }

        await api.post('/TelephoneRecord', payload)
        successMessage.value = 'บันทึกข้อมูลค่าโทรศัพท์สำเร็จ'

        formData.value = {
            docReceiveNumber: '', docNumber: '', billingCycle: null,
            phoneNumber: '', providerName: '', usageAmount: null,
            vatAmount: null, totalAmount: null, recordedBy: ''
        }
        await fetchHistory()
    } catch (error: unknown) {
        errorMessage.value = error instanceof Error ? `เกิดข้อผิดพลาด: ${error.message}` : 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ'
    } finally {
        isSubmitting.value = false
    }
}

// 6. Detail / Edit dialog state
const selectedRecord = ref<FetchedTelephoneRecord | null>(null)
const detailVisible = ref(false)
const editVisible = ref(false)
const isSaving = ref(false)

interface EditForm {
    docReceiveNumber: string
    docNumber: string
    billingCycle: Date | null
    phoneNumber: string
    providerName: string
    usageAmount: number | null
}

const editForm = ref<EditForm>({
    docReceiveNumber: '', docNumber: '', billingCycle: null,
    phoneNumber: '', providerName: '', usageAmount: null
})

const openDetail = (event: { data: FetchedTelephoneRecord }) => {
    selectedRecord.value = event.data
    detailVisible.value = true
}

const openEdit = () => {
    if (!selectedRecord.value) return
    const r = selectedRecord.value
    editForm.value = {
        docReceiveNumber: r.docReceiveNumber,
        docNumber: r.docNumber,
        billingCycle: r.billingCycle ? new Date(r.billingCycle) : null,
        phoneNumber: r.phoneNumber,
        providerName: r.providerName,
        usageAmount: r.usageAmount,
    }
    detailVisible.value = false
    editVisible.value = true
}

const saveEdit = async () => {
    if (!selectedRecord.value) return
    isSaving.value = true
    try {
        const payload = {
            docReceiveNumber: editForm.value.docReceiveNumber,
            docNumber: editForm.value.docNumber,
            billingCycle: editForm.value.billingCycle ? editForm.value.billingCycle.toISOString() : null,
            phoneNumber: editForm.value.phoneNumber,
            providerName: editForm.value.providerName,
            usageAmount: editForm.value.usageAmount || 0,
            vatAmount: editVat.value,
            totalAmount: editTotal.value,
        }
        await api.put(`/TelephoneRecord/${selectedRecord.value.id}`, payload)
        editVisible.value = false
        await fetchHistory()
        toast.success('แก้ไขข้อมูลสำเร็จ')
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
        await api.delete(`/TelephoneRecord/${selectedRecord.value.id}`)
        editVisible.value = false
        detailVisible.value = false
        selectedRecord.value = null
        await fetchHistory()
        toast.success('ลบข้อมูลสำเร็จ')
    } catch (e: unknown) {
        toast.fromError(e, 'เกิดข้อผิดพลาด กรุณาลองใหม่')
    }
}

// 7. Helpers
const getDeptName = (id: string): string => departments.value.find(x => x.id === id)?.name || id
const formatThaiMonth = (dateStr: string | null | undefined): string =>
    dateStr ? new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'long' }) : '-'
const formatCurrency = (val: number | null | undefined): string =>
    val != null ? new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val) : '-'
</script>

<template>
    <div class="max-w-6xl mx-auto pb-10">
        <div class="mb-6 flex flex-col sm:flex-row sm:justify-between sm:items-end gap-4">
            <div>
                <h2 class="text-2xl font-bold text-gray-800"><i
                        class="pi pi-phone text-green-500 mr-2"></i>บันทึกค่าโทรศัพท์และอินเทอร์เน็ต</h2>
                <p class="text-gray-500 mt-1">บันทึกข้อมูลใบแจ้งหนี้ ค่าบริการโทรศัพท์และอินเทอร์เน็ต</p>
            </div>
        </div>

        <Tabs value="0" lazy>
            <TabList>
                <Tab value="0"><i class="pi pi-file-edit mr-2"></i>บันทึกข้อมูล</Tab>
                <Tab value="1">
                    <i class="pi pi-history mr-2"></i>ประวัติย้อนหลัง
                    <span v-if="historyRecords.length > 0"
                        class="ml-2 bg-green-100 text-green-600 px-2 py-0.5 rounded-full text-xs">{{
                            historyRecords.length }}</span>
                </Tab>
            </TabList>

            <TabPanels>
                <TabPanel value="0">
                    <Card class="shadow-sm border-none mt-2">
                        <template #content>
                            <form @submit.prevent="submitForm" class="flex flex-col gap-8">
                                <Message v-if="successMessage" severity="success" :closable="true">{{ successMessage }}
                                </Message>
                                <Message v-if="errorMessage" severity="error" :closable="true">{{ errorMessage }}
                                </Message>

                                <div>
                                    <h3 class="font-bold text-gray-700 border-b pb-2 mb-4 text-lg"><i
                                            class="pi pi-file mr-2 text-green-500"></i>ข้อมูลเอกสารและการบริการ</h3>
                                    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">รอบบิล (เดือน/ปี) <span
                                                    class="text-red-500">*</span></label>
                                            <DatePicker v-model="formData.billingCycle" view="month" dateFormat="MM yy"
                                                class="w-full" showIcon />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">ชื่อผู้ให้บริการ</label>
                                            <InputText v-model="formData.providerName" placeholder="เช่น TOT, True, AIS"
                                                class="w-full" />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">หมายเลขโทรศัพท์</label>
                                            <InputText v-model="formData.phoneNumber" placeholder="เช่น 02-xxx-xxxx"
                                                class="w-full" />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">เลขที่รับหนังสือ</label>
                                            <InputText v-model="formData.docReceiveNumber" class="w-full" />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">เลขที่หนังสือ</label>
                                            <InputText v-model="formData.docNumber" class="w-full" />
                                        </div>
                                    </div>
                                </div>

                                <div class="bg-green-50/40 p-5 rounded-xl border border-green-100">
                                    <h3 class="font-bold text-green-800 border-b border-green-200 pb-2 mb-4 text-lg"><i
                                            class="pi pi-calculator mr-2"></i>รายละเอียดค่าบริการ</h3>
                                    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 items-end">
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">ค่าใช้บริการ (ก่อน VAT)</label>
                                            <InputNumber v-model="formData.usageAmount" mode="currency" currency="THB"
                                                locale="th-TH" class="w-full" />
                                        </div>

                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-500">ภาษีมูลค่าเพิ่ม 7%
                                                (คำนวณอัตโนมัติ)</label>
                                            <InputNumber :modelValue="computedVatAmount" mode="currency" currency="THB"
                                                locale="th-TH" readonly class="w-full"
                                                inputClass="bg-gray-50 text-gray-500 font-semibold" />
                                        </div>

                                        <div
                                            class="flex flex-col gap-2 bg-green-100 p-3 rounded-lg shadow-sm border border-green-200">
                                            <label
                                                class="font-bold text-sm text-green-800">รวมค่าใช้บริการรอบปัจจุบัน</label>
                                            <InputNumber :modelValue="computedTotalAmount" mode="currency"
                                                currency="THB" locale="th-TH" readonly class="w-full"
                                                inputClass="bg-transparent border-none text-right font-bold text-green-700 text-xl p-0" />
                                        </div>
                                    </div>
                                </div>

                                <div class="flex justify-end mt-2 pt-4 border-t border-gray-100">
                                    <Button type="submit" label="บันทึกบิลค่าโทรศัพท์" icon="pi pi-save"
                                        severity="success" :loading="isSubmitting" class="px-8 py-3 text-lg" />
                                </div>
                            </form>
                        </template>
                    </Card>
                </TabPanel>

                <TabPanel value="1">
                    <Card class="shadow-sm border-none mt-2 overflow-hidden">
                        <template #content>
                            <DataTable :value="historyRecords" :loading="isLoadingHistory" paginator :rows="10"
                                stripedRows responsiveLayout="scroll" emptyMessage="ยังไม่มีข้อมูล"
                                selectionMode="single" @row-click="openDetail" class="cursor-pointer">

                                <Column v-if="isAdmin" header="ผู้บันทึก">
                                    <template #body="sp">
                                        <div class="text-xs text-gray-500"><i class="pi pi-user mr-1"></i>{{
                                            sp.data.recordedBy }}</div>
                                    </template>
                                </Column>

                                <Column header="รอบบิล / ผู้ให้บริการ">
                                    <template #body="sp">
                                        <div class="font-semibold text-gray-800 tracking-wider">{{ sp.data.providerName || '-' }}
                                        </div>
                                        <div class="text-xs text-gray-500 mt-1"><i class="pi pi-calendar mr-1"></i>{{
                                            formatThaiMonth(sp.data.billingCycle) }}</div>
                                    </template>
                                </Column>

                                <Column header="หมายเลขโทรศัพท์">
                                    <template #body="sp">
                                        <div class="text-sm text-gray-700">{{ sp.data.phoneNumber || '-' }}</div>
                                    </template>
                                </Column>

                                <Column header="ยอดรวมสุทธิ (รวม VAT)">
                                    <template #body="sp">
                                        <div class="font-bold text-green-600 text-lg">{{
                                            formatCurrency(sp.data.totalAmount) }}</div>
                                    </template>
                                </Column>

                                <Column header="" style="width: 4rem">
                                    <template #body>
                                        <i class="pi pi-chevron-right text-gray-400"></i>
                                    </template>
                                </Column>

                            </DataTable>
                            <div class="flex justify-center mt-4">
                                <Button v-if="hasMore" label="โหลดเพิ่มเติม" icon="pi pi-chevron-down"
                                    severity="secondary" outlined :loading="isLoadingMore"
                                    @click="fetchHistory(true)" />
                                <p v-else-if="historyRecords.length > 0" class="text-xs text-gray-400 py-2">
                                    แสดงทั้งหมด {{ historyRecords.length }} รายการ
                                </p>
                            </div>
                        </template>
                    </Card>

                    <!-- Detail Dialog -->
                    <Dialog v-model:visible="detailVisible" modal header="รายละเอียดบิลค่าโทรศัพท์"
                        :style="{ width: '560px' }" :draggable="false">
                        <div v-if="selectedRecord" class="flex flex-col gap-5">
                            <div class="grid grid-cols-2 gap-3 text-sm">
                                <div class="bg-gray-50 rounded-lg p-3">
                                    <p class="text-gray-500 text-xs mb-1">ผู้ให้บริการ</p>
                                    <p class="font-semibold text-gray-800">{{ selectedRecord.providerName || '-' }}</p>
                                </div>
                                <div class="bg-gray-50 rounded-lg p-3">
                                    <p class="text-gray-500 text-xs mb-1">หมายเลขโทรศัพท์</p>
                                    <p class="font-semibold text-gray-800">{{ selectedRecord.phoneNumber || '-' }}</p>
                                </div>
                                <div class="bg-gray-50 rounded-lg p-3">
                                    <p class="text-gray-500 text-xs mb-1">รอบบิล</p>
                                    <p class="font-semibold text-gray-800">{{
                                        formatThaiMonth(selectedRecord.billingCycle) }}</p>
                                </div>
                                <div class="bg-gray-50 rounded-lg p-3">
                                    <p class="text-gray-500 text-xs mb-1">บันทึกโดย</p>
                                    <p class="font-semibold text-gray-800">{{ selectedRecord.recordedBy }}</p>
                                </div>
                                <div v-if="selectedRecord.docReceiveNumber" class="bg-gray-50 rounded-lg p-3">
                                    <p class="text-gray-500 text-xs mb-1">เลขที่รับหนังสือ</p>
                                    <p class="font-semibold text-gray-800">{{ selectedRecord.docReceiveNumber }}</p>
                                </div>
                                <div v-if="selectedRecord.docNumber" class="bg-gray-50 rounded-lg p-3">
                                    <p class="text-gray-500 text-xs mb-1">เลขที่หนังสือ</p>
                                    <p class="font-semibold text-gray-800">{{ selectedRecord.docNumber }}</p>
                                </div>
                            </div>

                            <div class="bg-green-50 rounded-xl p-4 border border-green-100">
                                <p class="font-bold text-green-800 mb-3"><i
                                        class="pi pi-calculator mr-2"></i>รายละเอียดค่าบริการ</p>
                                <div class="flex flex-col gap-2 text-sm">
                                    <div class="flex justify-between">
                                        <span class="text-gray-600">ค่าใช้บริการ</span>
                                        <span class="font-semibold">{{ formatCurrency(selectedRecord.usageAmount) }}</span>
                                    </div>
                                    <div class="flex justify-between border-t border-green-200 pt-2 mt-1">
                                        <span class="text-gray-500">VAT 7%</span>
                                        <span class="text-gray-500">{{ formatCurrency(selectedRecord.vatAmount)
                                            }}</span>
                                    </div>
                                    <div class="flex justify-between">
                                        <span class="font-bold text-green-800">รวมสุทธิ</span>
                                        <span class="font-bold text-green-700 text-lg">{{
                                            formatCurrency(selectedRecord.totalAmount)
                                            }}</span>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <template #footer>
                            <Button label="ปิด" severity="secondary" text @click="detailVisible = false" />
                            <Button v-if="isAdmin" label="แก้ไข" icon="pi pi-pencil" severity="warning"
                                @click="openEdit" />
                        </template>
                    </Dialog>

                    <!-- Edit Dialog -->
                    <Dialog v-model:visible="editVisible" modal header="แก้ไขบิลค่าโทรศัพท์" :style="{ width: '620px' }"
                        :draggable="false">
                        <div class="flex flex-col gap-4">
                            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                                <div class="flex flex-col gap-2">
                                    <label class="text-sm font-semibold text-gray-700">รอบบิล <span
                                            class="text-red-500">*</span></label>
                                    <DatePicker v-model="editForm.billingCycle" view="month" dateFormat="MM yy"
                                        class="w-full" showIcon />
                                </div>
                                <div class="flex flex-col gap-2">
                                    <label class="text-sm font-semibold text-gray-700">ชื่อผู้ให้บริการ</label>
                                    <InputText v-model="editForm.providerName" class="w-full" />
                                </div>
                                <div class="flex flex-col gap-2">
                                    <label class="text-sm font-semibold text-gray-700">หมายเลขโทรศัพท์</label>
                                    <InputText v-model="editForm.phoneNumber" class="w-full" />
                                </div>
                                <div class="flex flex-col gap-2">
                                    <label class="text-sm font-semibold text-gray-700">เลขที่รับหนังสือ</label>
                                    <InputText v-model="editForm.docReceiveNumber" class="w-full" />
                                </div>
                                <div class="flex flex-col gap-2">
                                    <label class="text-sm font-semibold text-gray-700">เลขที่หนังสือ</label>
                                    <InputText v-model="editForm.docNumber" class="w-full" />
                                </div>
                            </div>

                            <div class="bg-green-50/40 p-4 rounded-xl border border-green-100">
                                <p class="font-bold text-green-800 mb-3 text-sm"><i
                                        class="pi pi-calculator mr-2"></i>รายละเอียดค่าบริการ</p>
                                <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                                    <div class="flex flex-col gap-2">
                                        <label class="text-xs font-semibold text-gray-700">ค่าใช้บริการ (ก่อน VAT)</label>
                                        <InputNumber v-model="editForm.usageAmount" mode="currency" currency="THB"
                                            locale="th-TH" class="w-full" />
                                    </div>
                                </div>
                                <div class="flex justify-between items-center mt-4 pt-3 border-t border-green-200">
                                    <span class="text-sm text-gray-500">VAT 7%: <strong>{{ formatCurrency(editVat)
                                            }}</strong></span>
                                    <span class="font-bold text-green-700">รวมสุทธิ: {{ formatCurrency(editTotal)
                                        }}</span>
                                </div>
                            </div>
                        </div>

                        <template #footer>
                            <Button v-if="isAdmin" label="ลบ" icon="pi pi-trash" severity="danger" text
                                @click="deleteRecord" />
                            <Button label="ยกเลิก" severity="secondary" text @click="editVisible = false" />
                            <Button label="บันทึก" icon="pi pi-save" severity="success" :loading="isSaving"
                                @click="saveEdit" />
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

:deep(.bg-transparent) {
    background-color: transparent !important;
    cursor: default;
    box-shadow: none;
}
</style>
