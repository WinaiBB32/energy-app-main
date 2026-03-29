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

defineOptions({ name: 'PostalSystem' })

const toast = useAppToast()
const authStore = useAuthStore()

// ─── Interfaces ─────────────────────────────────────────────────────────────
interface PostalRecord {
    departmentId: string
    recordMonth: Date | null
    normalMail: number | null
    registeredMail: number | null
    emsMail: number | null
}

interface FetchedPostalRecord {
    id: string
    departmentId: string
    recordMonth: string | null
    normalMail: number
    registeredMail: number
    emsMail: number
    totalMail: number
    recordedByName: string
    createdAt: string
}

interface Department {
    id: string
    name: string
}

// ─── State ──────────────────────────────────────────────────────────────────
const { isSuperAdmin, isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('postal')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')
const departments = ref<Department[]>([])

const formData = ref<PostalRecord>({
    departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
    recordMonth: null,
    normalMail: null,
    registeredMail: null,
    emsMail: null,
})

const isSubmitting = ref<boolean>(false)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')

// ─── History State (Pagination) ─────────────────────────────────────────────
const historyRecords = ref<FetchedPostalRecord[]>([])
const isLoadingHistory = ref<boolean>(true)
const isLoadingMore = ref<boolean>(false)
const hasMoreData = ref<boolean>(true)
const page = ref(0)
const FETCH_LIMIT = 15

// ─── Computed ───────────────────────────────────────────────────────────────
const computedTotalMail = computed<number>(() => {
    return (formData.value.normalMail || 0) +
        (formData.value.registeredMail || 0) +
        (formData.value.emsMail || 0)
})

const getDeptName = (id: string): string => departments.value.find((x) => x.id === id)?.name || id
const formatThaiMonth = (dateStr: string | null | undefined): string => {
    if (!dateStr) return '-'
    return new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'long' })
}

// ─── Data Fetching ──────────────────────────────────
onMounted(async () => {
    try {
        const { data } = await api.get('/Department');
        departments.value = data.items || [];
        await fetchHistory(true)
    } catch {
        toast.error('ไม่สามารถโหลดข้อมูลเริ่มต้นได้')
    } finally {
        isLoadingHistory.value = false
    }
})

// ─── ฟังก์ชันดึงประวัติ (รองรับ Load More) ───────────────
const fetchHistory = async (isFirstPage: boolean = false) => {
    if (isFirstPage) {
        isLoadingHistory.value = true
        page.value = 0;
        historyRecords.value = []
        hasMoreData.value = true
    } else {
        if (!hasMoreData.value) return
        isLoadingMore.value = true;
        page.value++;
    }

    try {
        const params = {
            skip: page.value * FETCH_LIMIT,
            take: FETCH_LIMIT,
        }
        const { data } = await api.get('/PostalRecord', { params });
        const newRecords = data.items || [];

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
        isLoadingMore.value = false;
    }
}

// ─── บันทึกข้อมูล ───────────────────────────
const submitForm = async (): Promise<void> => {
    successMessage.value = ''
    errorMessage.value = ''

    if (!formData.value.recordMonth || !formData.value.departmentId) {
        errorMessage.value = 'กรุณากรอกข้อมูลรอบเดือน และหน่วยงานให้ครบถ้วน'
        return
    }

    try {
        isSubmitting.value = true
        
        const saveDeptId = isSuperAdmin.value ? formData.value.departmentId : currentUserDepartment.value

        const docData = {
            ...formData.value,
            departmentId: saveDeptId,
            totalMail: computedTotalMail.value,
            recordedByName: authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ',
            recordedBy: authStore.user?.uid || 'unknown',
        }

        await api.post('/PostalRecord', docData)

        successMessage.value = 'บันทึกสถิติไปรษณีย์สำเร็จ'

        await fetchHistory(true)

        formData.value.normalMail = null
        formData.value.registeredMail = null
        formData.value.emsMail = null

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
                    <i class="pi pi-envelope text-blue-500 mr-2"></i>บันทึกสถิติงานไปรษณีย์
                </h2>
                <p class="text-gray-500 mt-1">บันทึกจำนวนการจัดส่งไปรษณีย์ ธรรมดา/ลงทะเบียน/EMS ประจำเดือน</p>
            </div>
        </div>

        <Tabs value="0" lazy>
            <TabList>
                <Tab value="0"><i class="pi pi-file-edit mr-2"></i>บันทึกข้อมูล</Tab>
                <Tab value="1"><i class="pi pi-history mr-2"></i>ประวัติย้อนหลัง</Tab>
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

                                <div
                                    class="grid grid-cols-1 md:grid-cols-2 gap-4 bg-gray-50 p-4 rounded-xl border border-gray-100">
                                    <div class="flex flex-col gap-2">
                                        <label class="font-semibold text-sm text-gray-700">รอบเดือนที่จัดส่ง <span
                                                class="text-red-500">*</span></label>
                                        <DatePicker v-model="formData.recordMonth" view="month" dateFormat="MM yy"
                                            class="w-full" showIcon />
                                    </div>
                                    <div class="flex flex-col gap-2">
                                        <label class="font-semibold text-sm text-gray-700">หน่วยงานผู้จัดส่ง <span
                                                class="text-red-500">*</span></label>
                                        <Select v-if="isSuperAdmin" v-model="formData.departmentId"
                                            :options="departments" optionLabel="name" optionValue="id"
                                            placeholder="-- เลือกหน่วยงาน --" class="w-full" />
                                        <InputText v-else :value="getDeptName(currentUserDepartment)" disabled
                                            class="w-full bg-gray-100 font-bold text-gray-600" />
                                    </div>
                                </div>

                                <div class="bg-blue-50/40 p-5 rounded-xl border border-blue-100">
                                    <h3 class="font-bold text-blue-800 border-b border-blue-200 pb-2 mb-4 text-lg">
                                        <i class="pi pi-box mr-2"></i>รายละเอียดจำนวนการจัดส่ง
                                    </h3>
                                    <div class="grid grid-cols-1 sm:grid-cols-3 gap-6 items-end">
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">ไปรษณีย์ธรรมดา
                                                (ชิ้น)</label>
                                            <InputNumber v-model="formData.normalMail" :min="0" placeholder="0"
                                                class="w-full" />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">ไปรษณีย์ลงทะเบียน
                                                (ชิ้น)</label>
                                            <InputNumber v-model="formData.registeredMail" :min="0" placeholder="0"
                                                class="w-full" />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">ไปรษณีย์ EMS
                                                (ชิ้น)</label>
                                            <InputNumber v-model="formData.emsMail" :min="0" placeholder="0"
                                                class="w-full" />
                                        </div>

                                        <div
                                            class="flex flex-col gap-2 sm:col-start-3 bg-blue-100 p-3 rounded-lg shadow-sm border border-blue-200 mt-2">
                                            <label class="font-bold text-sm text-blue-800">รวมทั้งหมด (ชิ้น)</label>
                                            <InputNumber :modelValue="computedTotalMail" readonly class="w-full"
                                                inputClass="bg-transparent border-none text-right font-black text-blue-700 text-xl p-0" />
                                        </div>
                                    </div>
                                </div>

                                <div class="flex justify-end mt-2 pt-4 border-t border-gray-100">
                                    <Button type="submit" label="บันทึกสถิติไปรษณีย์" icon="pi pi-save" severity="info"
                                        :loading="isSubmitting" class="px-8 py-3 text-lg" />
                                </div>
                            </form>
                        </template>
                    </Card>
                </TabPanel>

                <TabPanel value="1">
                    <Card class="shadow-sm border-none mt-2 overflow-hidden">
                        <template #content>
                            <DataTable :value="historyRecords" :loading="isLoadingHistory" stripedRows
                                responsiveLayout="scroll" emptyMessage="ยังไม่มีข้อมูล">

                                <Column v-if="isAdmin" header="หน่วยงาน">
                                    <template #body="sp">
                                        <div class="font-bold text-gray-700">{{ getDeptName(sp.data.departmentId) }}
                                        </div>
                                        <div class="text-xs text-gray-500"><i class="pi pi-user mr-1"></i>{{
                                            sp.data.recordedByName }}</div>
                                    </template>
                                </Column>

                                <Column header="รอบเดือน">
                                    <template #body="sp">
                                        <div class="font-semibold text-gray-800"><i class="pi pi-calendar mr-2"></i>{{
                                            formatThaiMonth(sp.data.recordMonth) }}</div>
                                    </template>
                                </Column>

                                <Column header="ธรรมดา" class="text-center">
                                    <template #body="sp">{{ sp.data.normalMail }}</template>
                                </Column>
                                <Column header="ลงทะเบียน" class="text-center">
                                    <template #body="sp">{{ sp.data.registeredMail }}</template>
                                </Column>
                                <Column header="EMS" class="text-center">
                                    <template #body="sp">
                                        <span class="text-rose-600 font-semibold">{{ sp.data.emsMail }}</span>
                                    </template>
                                </Column>

                                <Column header="รวมทั้งหมด" class="text-center">
                                    <template #body="sp">
                                        <span class="font-bold text-blue-600 text-lg">{{ sp.data.totalMail }}</span>
                                    </template>
                                </Column>
                            </DataTable>

                            <div v-if="hasMoreData" class="flex justify-center mt-6 mb-2">
                                <Button label="โหลดข้อมูลเพิ่มเติม" icon="pi pi-refresh" severity="secondary" outlined
                                    size="small" @click="fetchHistory(false)" :loading="isLoadingHistory" />
                            </div>
                            <div v-else-if="historyRecords.length > 0"
                                class="text-center text-xs text-gray-400 mt-6 mb-2">
                                แสดงข้อมูลครบทั้งหมดแล้ว
                            </div>
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
