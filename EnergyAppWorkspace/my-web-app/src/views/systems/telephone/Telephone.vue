<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
// Firebase Removed
import { toMonthKey, batchUpdateSummary } from '@/utils/monthlySummary'
// Firebase Removed
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

// 1. Interfaces ปรับฟิลด์ตามที่กำหนดเป๊ะๆ
interface TelephoneRecord {
    departmentId: string
    docReceiveNumber: string
    docNumber: string
    billingCycle: Date | null
    customerId: string
    adslCost: number | null
    sipTrunkCost: number | null
    otherCost: number | null
    note: string
}

interface FetchedTelephoneRecord extends Omit<TelephoneRecord, 'billingCycle'> {
    id: string
    billingCycle: Timestamp | null
    vatAmount: number // บันทึกลง DB
    totalAmount: number // บันทึกลง DB
    recordedByName: string
    recordedByUid: string
    createdAt: Timestamp
}

interface Department {
    id: string
    name: string
}

const authStore = useAuthStore()
const { isSuperAdmin, isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('telephone')

// 2. ดึงสิทธิ์และหน่วยงานจากระบบจริง
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const departments = ref<Department[]>([])

const formData = ref<TelephoneRecord>({
    departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
    docReceiveNumber: '',
    docNumber: '',
    billingCycle: null,
    customerId: '',
    adslCost: null,
    sipTrunkCost: null,
    otherCost: null,
    note: ''
})

const isSubmitting = ref<boolean>(false)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')

const historyRecords = ref<FetchedTelephoneRecord[]>([])
const isLoadingHistory = ref<boolean>(true)
const isLoadingMore = ref<boolean>(false)
const hasMore = ref<boolean>(false)
const lastDoc = ref<QueryDocumentSnapshot | null>(null)
const PAGE_SIZE = 20

let unsubscribeDepts: () => void

// 3. ระบบคำนวณ ออโต้ (รวมยอด + VAT 7%)
const computedSubTotal = computed<number>(() => {
    return (formData.value.adslCost || 0) +
        (formData.value.sipTrunkCost || 0) +
        (formData.value.otherCost || 0)
})

// ภาษีมูลค่าเพิ่ม 7% คิดให้ออโต้
const computedVatAmount = computed<number>(() => {
    return computedSubTotal.value * 0.07
})

// รวมค่าใช้บริการรอบปัจจุบัน คิดให้ออโต้
const computedTotalAmount = computed<number>(() => {
    return computedSubTotal.value + computedVatAmount.value
})

// 4. ดึงข้อมูลประวัติ และ รายชื่อหน่วยงาน
const fetchHistory = async (loadMore = false): Promise<void> => {
    if (loadMore) isLoadingMore.value = true
    else { isLoadingHistory.value = true; historyRecords.value = []; lastDoc.value = null }
    try {
        const constraints: QueryConstraint[] = [orderBy('createdAt', 'desc'), limit(PAGE_SIZE + 1)]
        if (!isAdmin) constraints.unshift(where('departmentId', '==', currentUserDepartment.value))
        if (loadMore && lastDoc.value) constraints.push(startAfter(lastDoc.value))
        const snap = await getDocs(query(collection(db, 'telephone_records'), ...constraints))
        const docs = snap.docs.slice(0, PAGE_SIZE)
        hasMore.value = snap.docs.length > PAGE_SIZE
        if (docs.length > 0) lastDoc.value = docs[docs.length - 1] ?? null
        const records = docs.map(d => ({ id: d.id, ...d.data() } as FetchedTelephoneRecord))
        if (loadMore) historyRecords.value.push(...records)
        else historyRecords.value = records
    } catch (error: unknown) {
        toast.fromError(error, 'ไม่สามารถโหลดข้อมูลค่าโทรศัพท์ได้')
    } finally {
        isLoadingHistory.value = false
        isLoadingMore.value = false
    }
}

onMounted(() => {
    const deptQuery = query(collection(db, 'departments'), orderBy('name'))
    unsubscribeDepts = onSnapshot(deptQuery, (snapshot) => {
        const depts: Department[] = []
        snapshot.forEach(doc => depts.push({ id: doc.id, name: doc.data().name }))
        departments.value = depts
    })
    fetchHistory()
})

onUnmounted(() => {
    if (unsubscribeDepts) unsubscribeDepts()
})

// 5. บันทึกข้อมูล
const submitForm = async (): Promise<void> => {
    successMessage.value = ''
    errorMessage.value = ''

    if (!formData.value.billingCycle || !formData.value.customerId) {
        errorMessage.value = 'กรุณากรอกข้อมูลที่จำเป็น (*) ให้ครบถ้วน'
        return
    }

    try {
        isSubmitting.value = true

        const saveDepartmentId = isSuperAdmin.value ? formData.value.departmentId : currentUserDepartment.value

        const docData = {
            departmentId: saveDepartmentId,
            docReceiveNumber: formData.value.docReceiveNumber,
            docNumber: formData.value.docNumber,
            billingCycle: formData.value.billingCycle,
            customerId: formData.value.customerId,
            adslCost: formData.value.adslCost || 0,
            sipTrunkCost: formData.value.sipTrunkCost || 0,
            otherCost: formData.value.otherCost || 0,
            vatAmount: computedVatAmount.value,
            totalAmount: computedTotalAmount.value,
            note: formData.value.note,
            recordedByName: authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ',
            recordedByUid: authStore.user?.uid || 'unknown',
            createdAt: serverTimestamp()
        }
        const newDocRef = doc(collection(db, 'telephone_records'))
        const batch = writeBatch(db)
        batch.set(newDocRef, docData)
        const monthKey = toMonthKey(formData.value.billingCycle)
        if (monthKey) {
            batchUpdateSummary(batch, monthKey, 'telephone', {
                totalAmount: computedTotalAmount.value,
                count: 1,
            })
        }
        await batch.commit()
        successMessage.value = 'บันทึกข้อมูลค่าโทรศัพท์สำเร็จ'

        // เคลียร์ฟอร์ม
        formData.value = {
            departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
            docReceiveNumber: '', docNumber: '', billingCycle: null,
            customerId: '', adslCost: null, sipTrunkCost: null, otherCost: null, note: ''
        }
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
    departmentId: string
    docReceiveNumber: string
    docNumber: string
    billingCycle: Date | null
    customerId: string
    adslCost: number | null
    sipTrunkCost: number | null
    otherCost: number | null
    note: string
}

const editForm = ref<EditForm>({
    departmentId: '', docReceiveNumber: '', docNumber: '', billingCycle: null,
    customerId: '', adslCost: null, sipTrunkCost: null, otherCost: null, note: ''
})


// ใช้เพื่อแสดง preview ใน edit dialog เท่านั้น — ไม่ได้ส่งค่าเหล่านี้ไป backend
const editSubTotal = computed(() => (editForm.value.adslCost || 0) + (editForm.value.sipTrunkCost || 0) + (editForm.value.otherCost || 0))
const editVat = computed(() => editSubTotal.value * 0.07)
const editTotal = computed(() => editSubTotal.value + editVat.value)

const openDetail = (event: { data: FetchedTelephoneRecord }) => {
    selectedRecord.value = event.data
    detailVisible.value = true
}

const openEdit = () => {
    if (!selectedRecord.value) return
    const r = selectedRecord.value
    editForm.value = {
        departmentId: r.departmentId,
        docReceiveNumber: r.docReceiveNumber,
        docNumber: r.docNumber,
        billingCycle: r.billingCycle ? r.billingCycle.toDate() : null,
        customerId: r.customerId,
        adslCost: r.adslCost,
        sipTrunkCost: r.sipTrunkCost,
        otherCost: r.otherCost,
        note: r.note
    }
    detailVisible.value = false
    editVisible.value = true
}

const saveEdit = async () => {
    if (!selectedRecord.value) return
    isSaving.value = true
    try {
        await updateDoc(doc(db, 'telephone_records', selectedRecord.value.id), {
            departmentId: editForm.value.departmentId,
            docReceiveNumber: editForm.value.docReceiveNumber,
            docNumber: editForm.value.docNumber,
            billingCycle: editForm.value.billingCycle,
            customerId: editForm.value.customerId,
            adslCost: editForm.value.adslCost || 0,
            sipTrunkCost: editForm.value.sipTrunkCost || 0,
            otherCost: editForm.value.otherCost || 0,
            vatAmount: editVat.value,
            totalAmount: editTotal.value,
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
        const record = selectedRecord.value
        const batch = writeBatch(db)
        batch.delete(doc(db, 'telephone_records', record.id))
        const monthKey = toMonthKey(record.billingCycle)
        if (monthKey) {
            batchUpdateSummary(batch, monthKey, 'telephone', {
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

// 7. Helpers
const getDeptName = (id: string): string => departments.value.find(x => x.id === id)?.name || id
const formatThaiMonth = (ts: Timestamp | null | undefined): string => ts ? ts.toDate().toLocaleDateString('th-TH', { year: 'numeric', month: 'long' }) : '-'
const formatCurrency = (val: number | null | undefined): string => val != null ? new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val) : '-'
</script>

<template>
    <div class="max-w-6xl mx-auto pb-10">
        <div class="mb-6 flex flex-col sm:flex-row sm:justify-between sm:items-end gap-4">
            <div>
                <h2 class="text-2xl font-bold text-gray-800"><i
                        class="pi pi-phone text-green-500 mr-2"></i>บันทึกค่าโทรศัพท์และอินเทอร์เน็ต</h2>
                <p class="text-gray-500 mt-1">บันทึกข้อมูลใบแจ้งหนี้ ค่าบริการ ADSL และ SIP Trunk</p>
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
                                            class="pi pi-file mr-2 text-green-500"></i>ข้อมูลเอกสารและหน่วยงาน</h3>
                                    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">หน่วยงานรับผิดชอบ <span
                                                    class="text-red-500">*</span></label>
                                            <Select v-if="isSuperAdmin" v-model="formData.departmentId"
                                                :options="departments" optionLabel="name" optionValue="id"
                                                placeholder="-- เลือกหน่วยงาน --" class="w-full" />
                                            <InputText v-else :value="getDeptName(currentUserDepartment)" disabled
                                                class="w-full bg-gray-100 font-bold" />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">รหัสลูกค้า <span
                                                    class="text-red-500">*</span></label>
                                            <InputText v-model="formData.customerId" placeholder="เช่น CUST-12345"
                                                class="w-full font-bold" />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">รอบบิล (เดือน/ปี) <span
                                                    class="text-red-500">*</span></label>
                                            <DatePicker v-model="formData.billingCycle" view="month" dateFormat="MM yy"
                                                class="w-full" showIcon />
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
                                            <label class="font-semibold text-sm text-gray-700">ค่าบริการ ADSL 1
                                                Port</label>
                                            <InputNumber v-model="formData.adslCost" mode="currency" currency="THB"
                                                locale="th-TH" class="w-full" />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">ค่าบริการ SIP Trunk 1
                                                Trunk</label>
                                            <InputNumber v-model="formData.sipTrunkCost" mode="currency" currency="THB"
                                                locale="th-TH" class="w-full" />
                                        </div>
                                        <div class="flex flex-col gap-2">
                                            <label class="font-semibold text-sm text-gray-700">บริการอื่นๆ</label>
                                            <InputNumber v-model="formData.otherCost" mode="currency" currency="THB"
                                                locale="th-TH" class="w-full" />
                                        </div>

                                        <div class="flex flex-col gap-2 lg:col-start-2">
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

                                <div class="flex flex-col gap-2">
                                    <label class="font-semibold text-sm text-gray-700">หมายเหตุ</label>
                                    <Textarea v-model="formData.note" rows="2" placeholder="รายละเอียดเพิ่มเติม..."
                                        class="w-full" />
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

                                <Column v-if="isAdmin" header="หน่วยงาน">
                                    <template #body="sp">
                                        <div class="font-bold text-gray-700">{{ getDeptName(sp.data.departmentId) }}
                                        </div>
                                        <div class="text-xs text-gray-500"><i class="pi pi-user mr-1"></i>{{
                                            sp.data.recordedByName }}</div>
                                    </template>
                                </Column>

                                <Column header="รอบบิล / รหัสลูกค้า">
                                    <template #body="sp">
                                        <div class="font-semibold text-gray-800 tracking-wider">{{ sp.data.customerId }}
                                        </div>
                                        <div class="text-xs text-gray-500 mt-1"><i class="pi pi-calendar mr-1"></i>{{
                                            formatThaiMonth(sp.data.billingCycle) }}</div>
                                    </template>
                                </Column>

                                <Column header="รายละเอียดค่าบริการ">
                                    <template #body="sp">
                                        <div class="text-xs text-gray-600 mb-1">ADSL: {{
                                            formatCurrency(sp.data.adslCost) }}</div>
                                        <div class="text-xs text-gray-600">SIP Trunk: {{
                                            formatCurrency(sp.data.sipTrunkCost) }}</div>
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
                                    <p class="text-gray-500 text-xs mb-1">หน่วยงาน</p>
                                    <p class="font-semibold text-gray-800">{{ getDeptName(selectedRecord.departmentId)
                                        }}</p>
                                </div>
                                <div class="bg-gray-50 rounded-lg p-3">
                                    <p class="text-gray-500 text-xs mb-1">รหัสลูกค้า</p>
                                    <p class="font-semibold text-gray-800">{{ selectedRecord.customerId }}</p>
                                </div>
                                <div class="bg-gray-50 rounded-lg p-3">
                                    <p class="text-gray-500 text-xs mb-1">รอบบิล</p>
                                    <p class="font-semibold text-gray-800">{{
                                        formatThaiMonth(selectedRecord.billingCycle) }}</p>
                                </div>
                                <div class="bg-gray-50 rounded-lg p-3">
                                    <p class="text-gray-500 text-xs mb-1">บันทึกโดย</p>
                                    <p class="font-semibold text-gray-800">{{ selectedRecord.recordedByName }}</p>
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
                                        <span class="text-gray-600">ค่าบริการ ADSL</span>
                                        <span class="font-semibold">{{ formatCurrency(selectedRecord.adslCost) }}</span>
                                    </div>
                                    <div class="flex justify-between">
                                        <span class="text-gray-600">ค่าบริการ SIP Trunk</span>
                                        <span class="font-semibold">{{ formatCurrency(selectedRecord.sipTrunkCost)
                                            }}</span>
                                    </div>
                                    <div class="flex justify-between">
                                        <span class="text-gray-600">บริการอื่นๆ</span>
                                        <span class="font-semibold">{{ formatCurrency(selectedRecord.otherCost)
                                            }}</span>
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

                            <div v-if="selectedRecord.note"
                                class="bg-amber-50 rounded-lg p-3 text-sm border border-amber-100">
                                <p class="text-gray-500 text-xs mb-1">หมายเหตุ</p>
                                <p class="text-gray-700">{{ selectedRecord.note }}</p>
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
                                    <label class="text-sm font-semibold text-gray-700">หน่วยงาน</label>
                                    <Select v-if="isSuperAdmin" v-model="editForm.departmentId" :options="departments"
                                        optionLabel="name" optionValue="id" placeholder="-- เลือกหน่วยงาน --"
                                        class="w-full" />
                                    <InputText v-else :value="getDeptName(editForm.departmentId)" disabled
                                        class="w-full bg-gray-100" />
                                </div>
                                <div class="flex flex-col gap-2">
                                    <label class="text-sm font-semibold text-gray-700">รหัสลูกค้า <span
                                            class="text-red-500">*</span></label>
                                    <InputText v-model="editForm.customerId" class="w-full" />
                                </div>
                                <div class="flex flex-col gap-2">
                                    <label class="text-sm font-semibold text-gray-700">รอบบิล <span
                                            class="text-red-500">*</span></label>
                                    <DatePicker v-model="editForm.billingCycle" view="month" dateFormat="MM yy"
                                        class="w-full" showIcon />
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
                                        <label class="text-xs font-semibold text-gray-700">ค่าบริการ ADSL</label>
                                        <InputNumber v-model="editForm.adslCost" mode="currency" currency="THB"
                                            locale="th-TH" class="w-full" />
                                    </div>
                                    <div class="flex flex-col gap-2">
                                        <label class="text-xs font-semibold text-gray-700">ค่าบริการ SIP Trunk</label>
                                        <InputNumber v-model="editForm.sipTrunkCost" mode="currency" currency="THB"
                                            locale="th-TH" class="w-full" />
                                    </div>
                                    <div class="flex flex-col gap-2">
                                        <label class="text-xs font-semibold text-gray-700">บริการอื่นๆ</label>
                                        <InputNumber v-model="editForm.otherCost" mode="currency" currency="THB"
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

                            <div class="flex flex-col gap-2">
                                <label class="text-sm font-semibold text-gray-700">หมายเหตุ</label>
                                <Textarea v-model="editForm.note" rows="2" class="w-full" />
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
