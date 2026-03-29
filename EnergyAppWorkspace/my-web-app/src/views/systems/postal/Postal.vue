<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'
import { toMonthKey, batchUpdateSummary } from '@/utils/monthlySummary'
import {
    collection, query, where, getDocs, doc, writeBatch, serverTimestamp, orderBy, limit, startAfter, Timestamp,
    type QueryConstraint, type QueryDocumentSnapshot, type DocumentData
} from 'firebase/firestore'
import { db } from '@/firebase/config'

defineOptions({ name: 'PostalSystem' })

const toast = useAppToast()
const authStore = useAuthStore()

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
    recordMonth: Timestamp | null
    normalMail: number
    registeredMail: number
    emsMail: number
    totalMail: number
    recordedByName: string
    createdAt: Timestamp
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
const lastVisibleDoc = ref<QueryDocumentSnapshot<DocumentData> | null>(null)
const hasMoreData = ref<boolean>(true)
const FETCH_LIMIT = 15

// ─── Computed ───────────────────────────────────────────────────────────────
const computedTotalMail = computed<number>(() => {
    return (formData.value.normalMail || 0) +
        (formData.value.registeredMail || 0) +
        (formData.value.emsMail || 0)
})

const getDeptName = (id: string): string => departments.value.find((x) => x.id === id)?.name || id
const formatThaiMonth = (ts: Timestamp | null | undefined): string => {
    if (!ts) return '-'
    return ts.toDate().toLocaleDateString('th-TH', { year: 'numeric', month: 'long' })
}

// ─── ดึงข้อมูลตอนเปิดหน้า (One-time fetch) ──────────────────────────────────
onMounted(async () => {
    try {
        // โหลดรายชื่อหน่วยงาน
        const deptSnap = await getDocs(query(collection(db, 'departments'), orderBy('name')))
        departments.value = deptSnap.docs.map((d) => ({ id: d.id, name: d.data().name as string }))

        // โหลดประวัติหน้าแรก
        await fetchHistory(true)
    } catch {
        toast.error('ไม่สามารถโหลดข้อมูลเริ่มต้นได้')
    } finally {
        isLoadingHistory.value = false
    }
})

// ─── ฟังก์ชันดึงประวัติ (รองรับ Load More ลดการใช้ Read Quota) ───────────────
const fetchHistory = async (isFirstPage: boolean = false) => {
    if (isFirstPage) {
        isLoadingHistory.value = true
        historyRecords.value = []
        lastVisibleDoc.value = null
        hasMoreData.value = true
    }

    if (!hasMoreData.value) return

    try {
        const postalRef = collection(db, 'postal_records')

        const constraints: QueryConstraint[] = []
        if (!isAdmin) {
            constraints.push(where('departmentId', '==', currentUserDepartment.value))
        }
        constraints.push(orderBy('createdAt', 'desc'))
        if (lastVisibleDoc.value) constraints.push(startAfter(lastVisibleDoc.value))
        constraints.push(limit(FETCH_LIMIT))

        const q = query(postalRef, ...constraints)
        const snapshot = await getDocs(q)

        if (snapshot.empty) {
            hasMoreData.value = false
            return
        }

        lastVisibleDoc.value = snapshot.docs[snapshot.docs.length - 1] ?? null

        const newRecords: FetchedPostalRecord[] = []
        snapshot.forEach((doc) => {
            newRecords.push({ id: doc.id, ...doc.data() } as FetchedPostalRecord)
        })

        historyRecords.value = [...historyRecords.value, ...newRecords]

        if (snapshot.docs.length < FETCH_LIMIT) {
            hasMoreData.value = false
        }
    } catch {
        toast.error('ไม่สามารถโหลดข้อมูลประวัติได้')
    } finally {
        isLoadingHistory.value = false
    }
}

// ─── บันทึกข้อมูล (Batch + Increment Aggregation) ───────────────────────────
const submitForm = async (): Promise<void> => {
    successMessage.value = ''
    errorMessage.value = ''

    if (!formData.value.recordMonth || !formData.value.departmentId) {
        errorMessage.value = 'กรุณากรอกข้อมูลรอบเดือน และหน่วยงานให้ครบถ้วน'
        return
    }

    try {
        isSubmitting.value = true
        const batch = writeBatch(db)

        // 1. เตรียมข้อมูล Record ใหม่
        const saveDeptId = isSuperAdmin.value ? formData.value.departmentId : currentUserDepartment.value
        const dateObj = formData.value.recordMonth

        const nMail = formData.value.normalMail || 0
        const rMail = formData.value.registeredMail || 0
        const eMail = formData.value.emsMail || 0
        const tMail = computedTotalMail.value

        const newRecordRef = doc(collection(db, 'postal_records'))
        const docData = {
            departmentId: saveDeptId,
            recordMonth: Timestamp.fromDate(dateObj),
            normalMail: nMail,
            registeredMail: rMail,
            emsMail: eMail,
            totalMail: tMail,
            recordedByName: authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ',
            recordedByUid: authStore.user?.uid || 'unknown',
            createdAt: serverTimestamp(),
        }
        batch.set(newRecordRef, docData)

        // 2. อัปเดต Summary สำหรับ Dashboard ทันที (Aggregation)
        const monthKey = toMonthKey(dateObj)
        if (monthKey) {
            batchUpdateSummary(batch, monthKey, 'postal', {
                normalMail: nMail,
                registeredMail: rMail,
                emsMail: eMail,
                totalAmount: tMail, // ใน postal เราเก็บ totalMail ในฟิลด์ totalAmount ของ summary เพื่อความง่าย
                count: 1
            })
        }

        // 3. Commit คำสั่งทั้งหมดพร้อมกัน
        await batch.commit()

        successMessage.value = 'บันทึกสถิติไปรษณีย์สำเร็จ'

        // โหลดประวัติใหม่เพื่อแสดงข้อมูลที่พึ่งบันทึก
        await fetchHistory(true)

        // เคลียร์ฟอร์มบางส่วน
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