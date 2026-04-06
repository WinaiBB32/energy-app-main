<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import {
    MAINTENANCE_ADMIN_BUILDING_PERMISSION,
    MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
    hasMaintenancePermission,
} from '@/config/maintenancePermissions'

import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Textarea from 'primevue/textarea'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import ToggleSwitch from 'primevue/toggleswitch'
import Message from 'primevue/message'

interface ServiceRequest {
    id: string
    workOrderNo?: string
    title: string
    description?: string
    status: string
    buildingName?: string
    locationDetail?: string
    isCentralAsset?: boolean
    supervisorReason?: string
    supervisorExternalAdvice?: string
    externalVendorName?: string
    externalScheduledAt?: string | null
    externalCompletedAt?: string | null
    externalResult?: string
    createdAt?: string | null
    updatedAt?: string | null
    closedAt?: string | null
    requesterName?: string
    requesterDepartmentCode?: string
    requesterDepartmentName?: string
    category?: string
    priority?: string
    adminOfficerName?: string
    closedByName?: string
}

interface DepartmentItem { id: string; code?: string }

// ─── Auth & permissions ───────────────────────────────────────────────────────
const authStore = useAuthStore()
const route = useRoute()
const router = useRouter()
const role = computed(() => (authStore.user?.role ?? '').toLowerCase())
const adminSystems = computed(() => authStore.userProfile?.adminSystems ?? [])
const departmentId = computed(() => (authStore.userProfile?.departmentId ?? authStore.user?.departmentId ?? '').toString())
const departmentCode = ref('')

const hasDeptPermission = computed(() => hasMaintenancePermission(adminSystems.value, MAINTENANCE_ADMIN_BUILDING_PERMISSION))
const hasCentralPermission = computed(() => hasMaintenancePermission(adminSystems.value, MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION))
const canAccess = computed(() =>
    ['superadmin', 'adminbuilding', 'admin'].includes(role.value) ||
    hasDeptPermission.value || hasCentralPermission.value,
)

// ─── Mode: list vs single timeline ───────────────────────────────────────────
// /maintenance/external-timeline?id=xxx  → timeline mode (single job)
// /maintenance/external-procurement       → list mode
const timelineJobId = computed(() => route.query.id as string | undefined)
const isTimelineMode = computed(() =>
    route.path.includes('external-timeline') || !!timelineJobId.value,
)

// ─── Data ─────────────────────────────────────────────────────────────────────
const loading = ref(false)
const saving = ref(false)
const requests = ref<ServiceRequest[]>([])
const errorMessage = ref('')
const activeFilter = ref<'all' | 'waiting' | 'scheduled' | 'in_progress' | 'resolved'>('all')

const normalizedDeptKeys = computed(() =>
    Array.from(new Set(
        [departmentId.value, departmentCode.value]
            .map((x) => (x || '').trim().toLowerCase())
            .filter(Boolean),
    )),
)

// งานที่หน้านี้มีสิทธิ์ดู (เฉพาะ status ที่เกี่ยวกับช่างนอก)
const EXTERNAL_STATUSES = [
    'waiting_department_external_procurement',
    'waiting_central_external_procurement',
    'external_scheduled',
    'external_in_progress',
    'resolved',
]

const allQueueItems = computed(() =>
    requests.value.filter((item) => {
        if (!EXTERNAL_STATUSES.includes(item.status)) return false

        const isDeptQueue = hasDeptPermission.value || ['admin', 'superadmin'].includes(role.value)
        const isCentralQueue = hasCentralPermission.value || ['adminbuilding', 'superadmin'].includes(role.value)

        const reqDept = (item.requesterDepartmentCode || '').trim().toLowerCase()
        const isOwnDept =
            role.value === 'superadmin' ||
            (reqDept.length > 0 && normalizedDeptKeys.value.includes(reqDept))

        const deptStatuses = ['waiting_department_external_procurement', 'external_scheduled', 'external_in_progress', 'resolved']
        const centralStatuses = ['waiting_central_external_procurement', 'external_scheduled', 'external_in_progress', 'resolved']

        const showDept = isDeptQueue && !item.isCentralAsset && isOwnDept && deptStatuses.includes(item.status)
        const showCentral = isCentralQueue && !!item.isCentralAsset && centralStatuses.includes(item.status)

        return showDept || showCentral
    }),
)

const filteredItems = computed(() => {
    const list = allQueueItems.value
    if (activeFilter.value === 'waiting') return list.filter((i) => i.status.startsWith('waiting_'))
    if (activeFilter.value === 'scheduled') return list.filter((i) => i.status === 'external_scheduled')
    if (activeFilter.value === 'in_progress') return list.filter((i) => i.status === 'external_in_progress')
    if (activeFilter.value === 'resolved') return list.filter((i) => i.status === 'resolved')
    return list
})

const stats = computed(() => ({
    waiting: allQueueItems.value.filter((i) => i.status.startsWith('waiting_')).length,
    scheduled: allQueueItems.value.filter((i) => i.status === 'external_scheduled').length,
    inProgress: allQueueItems.value.filter((i) => i.status === 'external_in_progress').length,
    resolved: allQueueItems.value.filter((i) => i.status === 'resolved').length,
}))

// single job สำหรับ timeline mode
const timelineJob = computed(() =>
    timelineJobId.value
        ? requests.value.find((r) => r.id === timelineJobId.value) ?? null
        : null,
)

// ─── Form / Dialog ────────────────────────────────────────────────────────────
const dialogVisible = ref(false)
const selectedRequest = ref<ServiceRequest | null>(null)

// form fields ตาม action step
const form = ref({
    // step 1: รับงาน (waiting → scheduled)
    vendorName: '',
    scheduledAt: null as Date | null,
    // step 2: ซ่อมเสร็จ (in_progress → resolved / closed)
    completedAt: null as Date | null,
    result: '',
    closeAfterComplete: false,
})

// action ที่จะทำ
type ActionMode = 'accept'        // รับงาน (waiting → scheduled)
               | 'start'         // เริ่มซ่อม (scheduled → in_progress)
               | 'complete'      // ซ่อมเสร็จ (in_progress → resolved/closed)
               | 'close'         // ปิดงาน (resolved → closed)
               | 'edit'          // แก้ไขข้อมูลช่าง

const actionMode = ref<ActionMode>('accept')

const actionLabel = computed(() => ({
    accept: 'รับงาน — นัดช่างภายนอก',
    start: 'ยืนยันช่างเริ่มซ่อมแล้ว',
    complete: 'บันทึกงานซ่อมเสร็จ',
    close: 'ปิดงาน',
    edit: 'แก้ไขข้อมูลช่าง',
}[actionMode.value]))

const openDialog = (item: ServiceRequest, mode: ActionMode) => {
    selectedRequest.value = item
    actionMode.value = mode
    errorMessage.value = ''
    form.value = {
        vendorName: item.externalVendorName || '',
        scheduledAt: item.externalScheduledAt ? new Date(item.externalScheduledAt) : null,
        completedAt: item.externalCompletedAt ? new Date(item.externalCompletedAt) : null,
        result: item.externalResult || '',
        closeAfterComplete: false,
    }
    dialogVisible.value = true
}

// ─── Status helpers ───────────────────────────────────────────────────────────
interface StatusInfo { label: string; color: string; icon: string; dot: string; barColor: string }

const statusInfo = (value: string): StatusInfo => {
    if (value === 'waiting_department_external_procurement')
        return { label: 'รอจัดจ้าง (กอง)', color: 'text-red-600 bg-red-50 border-red-200', icon: 'pi-clock', dot: 'bg-red-400', barColor: 'bg-red-400' }
    if (value === 'waiting_central_external_procurement')
        return { label: 'รอจัดจ้าง (ส่วนกลาง)', color: 'text-orange-600 bg-orange-50 border-orange-200', icon: 'pi-clock', dot: 'bg-orange-400', barColor: 'bg-orange-400' }
    if (value === 'external_scheduled')
        return { label: 'นัดช่างแล้ว', color: 'text-blue-600 bg-blue-50 border-blue-200', icon: 'pi-calendar', dot: 'bg-blue-400', barColor: 'bg-blue-400' }
    if (value === 'external_in_progress')
        return { label: 'ช่างกำลังซ่อม', color: 'text-indigo-600 bg-indigo-50 border-indigo-200', icon: 'pi-wrench', dot: 'bg-indigo-400', barColor: 'bg-indigo-400' }
    if (value === 'resolved')
        return { label: 'ซ่อมเสร็จ รอปิดงาน', color: 'text-emerald-600 bg-emerald-50 border-emerald-200', icon: 'pi-check-circle', dot: 'bg-emerald-400', barColor: 'bg-emerald-400' }
    if (value === 'closed')
        return { label: 'ปิดงานแล้ว', color: 'text-gray-500 bg-gray-50 border-gray-200', icon: 'pi-lock', dot: 'bg-gray-400', barColor: 'bg-gray-300' }
    return { label: value, color: 'text-gray-500 bg-gray-50 border-gray-200', icon: 'pi-circle', dot: 'bg-gray-400', barColor: 'bg-gray-300' }
}

const formatDate = (value: string | null | undefined): string => {
    if (!value) return '-'
    const hasTimezone = /([zZ]|[+\-]\d{2}:\d{2})$/.test(value)
    const normalized = hasTimezone ? value : `${value}Z`
    return new Date(normalized).toLocaleString('th-TH', {
        year: 'numeric', month: 'short', day: 'numeric',
        hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Bangkok',
    })
}

// ─── Data loading ─────────────────────────────────────────────────────────────
const loadQueue = async () => {
    loading.value = true
    errorMessage.value = ''
    try {
        const res = await api.get('/ServiceRequest', { params: { take: 500 } })
        requests.value = (res.data.items ?? []) as ServiceRequest[]
    } catch {
        errorMessage.value = 'ไม่สามารถโหลดข้อมูลได้'
    } finally {
        loading.value = false
    }
}

const loadDeptCode = async () => {
    if (!departmentId.value) return
    try {
        const res = await api.get('/Department/all')
        const depts = (res.data ?? []) as DepartmentItem[]
        departmentCode.value = depts.find((d) => d.id === departmentId.value)?.code ?? ''
    } catch { /* ไม่แสดง error */ }
}

onMounted(async () => {
    if (!canAccess.value) return
    await loadDeptCode()
    await loadQueue()
})

watch(timelineJobId, async (newId) => {
    if (newId && requests.value.length === 0) await loadQueue()
})

// ─── Save ─────────────────────────────────────────────────────────────────────
const saveAction = async () => {
    if (!selectedRequest.value) return

    // validate
    if ((actionMode.value === 'accept' || actionMode.value === 'edit') && !form.value.vendorName.trim()) {
        errorMessage.value = 'กรุณาระบุชื่อช่าง / บริษัทภายนอก'
        return
    }
    if (actionMode.value === 'accept' && !form.value.scheduledAt) {
        errorMessage.value = 'กรุณาระบุวันนัดหมาย'
        return
    }
    if (actionMode.value === 'complete' && !form.value.completedAt) {
        errorMessage.value = 'กรุณาระบุวันที่ซ่อมเสร็จ'
        return
    }

    saving.value = true
    errorMessage.value = ''
    try {
        // map action → payload
        let payload: Record<string, unknown> = {}

        if (actionMode.value === 'accept' || actionMode.value === 'edit') {
            payload = {
                vendorName: form.value.vendorName,
                scheduledAt: form.value.scheduledAt?.toISOString() ?? null,
                completedAt: null,
                result: form.value.result,
                closeAfterComplete: false,
            }
        } else if (actionMode.value === 'start') {
            payload = {
                vendorName: selectedRequest.value.externalVendorName || '',
                scheduledAt: null,
                completedAt: null,
                result: form.value.result,
                closeAfterComplete: false,
            }
        } else if (actionMode.value === 'complete') {
            payload = {
                vendorName: selectedRequest.value.externalVendorName || form.value.vendorName,
                scheduledAt: null,
                completedAt: form.value.completedAt?.toISOString() ?? null,
                result: form.value.result,
                closeAfterComplete: form.value.closeAfterComplete,
            }
        } else if (actionMode.value === 'close') {
            payload = {
                vendorName: selectedRequest.value.externalVendorName || '',
                scheduledAt: null,
                completedAt: new Date().toISOString(),
                result: form.value.result || selectedRequest.value.externalResult || '',
                closeAfterComplete: true,
            }
        }

        await api.put(`/ServiceRequest/${selectedRequest.value.id}/external-progress`, payload)
        dialogVisible.value = false
        await loadQueue()
    } catch {
        errorMessage.value = 'ไม่สามารถบันทึกข้อมูลได้'
    } finally {
        saving.value = false
    }
}
</script>

<template>
    <div class="space-y-6">

        <!-- No permission -->
        <Message v-if="!canAccess" severity="warn" :closable="false">
            หน้านี้สำหรับเจ้าหน้าที่รับเรื่องจ้างช่างนอก และผู้ดูแลระบบเท่านั้น
        </Message>

        <template v-else>

            <!-- ─── TIMELINE MODE: แสดงรายละเอียดงานเดียว ─── -->
            <template v-if="isTimelineMode">
                <div class="flex items-center gap-3 mb-2">
                    <button @click="router.push('/maintenance/external-procurement')"
                        class="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-800 transition-colors">
                        <i class="pi pi-arrow-left text-xs"></i> กลับรายการ
                    </button>
                </div>

                <div v-if="loading" class="bg-white rounded-2xl border p-8 text-center text-gray-400">
                    <i class="pi pi-spin pi-spinner text-2xl mb-2 block"></i> กำลังโหลด...
                </div>

                <div v-else-if="!timelineJob"
                    class="bg-white rounded-2xl border border-dashed border-gray-200 p-12 text-center">
                    <i class="pi pi-search text-3xl text-gray-300 mb-3 block"></i>
                    <p class="text-gray-500">ไม่พบงานที่ระบุ</p>
                </div>

                <div v-else class="space-y-4">
                    <!-- Job header card -->
                    <div class="bg-white rounded-2xl border border-gray-200 shadow-sm overflow-hidden">
                        <div class="h-1.5" :class="statusInfo(timelineJob.status).barColor"></div>
                        <div class="p-5 space-y-4">
                            <div class="flex flex-col sm:flex-row sm:items-start gap-4">
                                <div class="flex-1 min-w-0">
                                    <div class="flex flex-wrap items-center gap-2 mb-1.5">
                                        <span class="text-xs font-mono font-bold text-gray-400">{{ timelineJob.workOrderNo }}</span>
                                        <span class="inline-flex items-center gap-1 text-xs font-semibold px-2 py-0.5 rounded-full border"
                                            :class="statusInfo(timelineJob.status).color">
                                            <span class="w-1.5 h-1.5 rounded-full" :class="statusInfo(timelineJob.status).dot"></span>
                                            {{ statusInfo(timelineJob.status).label }}
                                        </span>
                                        <span class="text-[10px] font-semibold px-2 py-0.5 rounded-full"
                                            :class="timelineJob.isCentralAsset ? 'bg-purple-100 text-purple-700' : 'bg-sky-100 text-sky-700'">
                                            {{ timelineJob.isCentralAsset ? 'ทรัพย์สินส่วนกลาง' : 'ทรัพย์สินหน่วยงาน' }}
                                        </span>
                                    </div>
                                    <h2 class="text-xl font-bold text-gray-800">{{ timelineJob.title }}</h2>
                                    <p v-if="timelineJob.description" class="text-sm text-gray-500 mt-1">{{ timelineJob.description }}</p>
                                    <div class="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm text-gray-500">
                                        <span v-if="timelineJob.buildingName" class="flex items-center gap-1">
                                            <i class="pi pi-building text-xs text-gray-400"></i>
                                            {{ timelineJob.buildingName }}
                                            <span v-if="timelineJob.locationDetail" class="text-gray-400">· {{ timelineJob.locationDetail }}</span>
                                        </span>
                                        <span v-if="timelineJob.requesterDepartmentName" class="flex items-center gap-1">
                                            <i class="pi pi-sitemap text-xs text-gray-400"></i>
                                            {{ timelineJob.requesterDepartmentName }}
                                        </span>
                                    </div>
                                </div>
                                <!-- Action button for timeline mode -->
                                <div class="shrink-0" v-if="timelineJob.status !== 'closed'">
                                    <Button
                                        v-if="timelineJob.status.startsWith('waiting_')"
                                        label="รับงาน" icon="pi pi-plus" severity="danger" size="small"
                                        @click="openDialog(timelineJob, 'accept')" />
                                    <Button
                                        v-else-if="timelineJob.status === 'external_scheduled'"
                                        label="ยืนยันเริ่มซ่อม" icon="pi pi-wrench" severity="warn" size="small"
                                        @click="openDialog(timelineJob, 'start')" />
                                    <Button
                                        v-else-if="timelineJob.status === 'external_in_progress'"
                                        label="บันทึกงานเสร็จ" icon="pi pi-check" severity="success" size="small"
                                        @click="openDialog(timelineJob, 'complete')" />
                                    <Button
                                        v-else-if="timelineJob.status === 'resolved'"
                                        label="ปิดงาน" icon="pi pi-lock" size="small"
                                        @click="openDialog(timelineJob, 'close')" />
                                </div>
                            </div>

                            <!-- Timeline progress steps -->
                            <div class="pt-4 border-t border-gray-100">
                                <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-3">ความคืบหน้า</p>
                                <div class="flex items-center gap-0">
                                    <template v-for="(step, i) in [
                                        { key: 'waiting', label: 'รับเรื่อง', icon: 'pi-inbox' },
                                        { key: 'scheduled', label: 'นัดช่าง', icon: 'pi-calendar' },
                                        { key: 'in_progress', label: 'กำลังซ่อม', icon: 'pi-wrench' },
                                        { key: 'resolved', label: 'ซ่อมเสร็จ', icon: 'pi-check-circle' },
                                        { key: 'closed', label: 'ปิดงาน', icon: 'pi-lock' },
                                    ]" :key="step.key">
                                        <div class="flex flex-col items-center">
                                            <div class="w-8 h-8 rounded-full flex items-center justify-center border-2 text-xs"
                                                :class="
                                                    (step.key === 'waiting' && timelineJob.status.startsWith('waiting_')) ||
                                                    (step.key === 'scheduled' && timelineJob.status === 'external_scheduled') ||
                                                    (step.key === 'in_progress' && timelineJob.status === 'external_in_progress') ||
                                                    (step.key === 'resolved' && timelineJob.status === 'resolved') ||
                                                    (step.key === 'closed' && timelineJob.status === 'closed')
                                                        ? 'bg-indigo-500 border-indigo-500 text-white'
                                                    : (step.key === 'scheduled' && ['external_in_progress','resolved','closed'].includes(timelineJob.status)) ||
                                                      (step.key === 'in_progress' && ['resolved','closed'].includes(timelineJob.status)) ||
                                                      (step.key === 'resolved' && timelineJob.status === 'closed') ||
                                                      (step.key === 'waiting' && !timelineJob.status.startsWith('waiting_'))
                                                        ? 'bg-emerald-100 border-emerald-400 text-emerald-600'
                                                        : 'bg-gray-100 border-gray-300 text-gray-400'
                                                ">
                                                <i :class="['pi text-xs', step.icon]"></i>
                                            </div>
                                            <span class="text-[10px] text-gray-500 mt-1 text-center w-14 leading-tight">{{ step.label }}</span>
                                        </div>
                                        <div v-if="i < 4" class="h-0.5 w-8 mb-4"
                                            :class="
                                                (step.key === 'waiting' && !timelineJob.status.startsWith('waiting_')) ||
                                                (step.key === 'scheduled' && ['external_in_progress','resolved','closed'].includes(timelineJob.status)) ||
                                                (step.key === 'in_progress' && ['resolved','closed'].includes(timelineJob.status)) ||
                                                (step.key === 'resolved' && timelineJob.status === 'closed')
                                                    ? 'bg-emerald-300' : 'bg-gray-200'
                                            ">
                                        </div>
                                    </template>
                                </div>
                            </div>

                            <!-- Detail rows -->
                            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2 border-t border-gray-100">
                                <div class="space-y-2 text-sm">
                                    <div v-if="timelineJob.supervisorReason || timelineJob.supervisorExternalAdvice"
                                        class="bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
                                        <p class="text-xs font-semibold text-amber-700 mb-0.5">หมายเหตุหัวหน้าช่าง</p>
                                        <p class="text-amber-800">{{ timelineJob.supervisorReason || timelineJob.supervisorExternalAdvice }}</p>
                                    </div>
                                    <div v-if="timelineJob.externalVendorName"
                                        class="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-lg px-3 py-2">
                                        <i class="pi pi-user text-slate-500 text-xs"></i>
                                        <div>
                                            <p class="text-xs text-gray-400">ช่าง / บริษัทภายนอก</p>
                                            <p class="font-medium text-slate-700">{{ timelineJob.externalVendorName }}</p>
                                        </div>
                                    </div>
                                </div>
                                <div class="space-y-2 text-sm">
                                    <div v-if="timelineJob.externalScheduledAt"
                                        class="flex items-center gap-2 bg-blue-50 border border-blue-200 rounded-lg px-3 py-2">
                                        <i class="pi pi-calendar-clock text-blue-500 text-xs"></i>
                                        <div>
                                            <p class="text-xs text-gray-400">วันนัดหมาย</p>
                                            <p class="font-medium text-blue-700">{{ formatDate(timelineJob.externalScheduledAt) }}</p>
                                        </div>
                                    </div>
                                    <div v-if="timelineJob.externalCompletedAt"
                                        class="flex items-center gap-2 bg-emerald-50 border border-emerald-200 rounded-lg px-3 py-2">
                                        <i class="pi pi-check text-emerald-500 text-xs"></i>
                                        <div>
                                            <p class="text-xs text-gray-400">วันที่ซ่อมเสร็จ</p>
                                            <p class="font-medium text-emerald-700">{{ formatDate(timelineJob.externalCompletedAt) }}</p>
                                        </div>
                                    </div>
                                    <div v-if="timelineJob.externalResult"
                                        class="bg-gray-50 border border-gray-200 rounded-lg px-3 py-2">
                                        <p class="text-xs text-gray-400 mb-0.5">ผลการซ่อม</p>
                                        <p class="text-gray-700">{{ timelineJob.externalResult }}</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </template>

            <!-- ─── LIST MODE: หน้ารับเรื่องจ้างช่างนอก ─── -->
            <template v-else>
                <div class="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
                    <div>
                        <h1 class="text-2xl font-bold text-gray-900">รับเรื่องจ้างช่างนอก</h1>
                        <p class="text-sm text-gray-500 mt-1">จัดการใบงานที่ต้องว่าจ้างช่างภายนอก</p>
                    </div>
                    <Button icon="pi pi-refresh" label="รีเฟรช" outlined size="small" :loading="loading" @click="loadQueue" />
                </div>

                <Message v-if="errorMessage" severity="error" :closable="true" @close="errorMessage = ''">{{ errorMessage }}</Message>

                <!-- Stats cards -->
                <div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
                    <button v-for="stat in [
                        { key: 'waiting',     label: 'รอจัดจ้าง',   count: stats.waiting,     activeColor: 'border-red-300 ring-2 ring-red-200',     iconBg: 'bg-red-100',     icon: 'pi-clock',        textColor: 'text-red-600' },
                        { key: 'scheduled',   label: 'นัดช่างแล้ว', count: stats.scheduled,   activeColor: 'border-blue-300 ring-2 ring-blue-200',   iconBg: 'bg-blue-100',    icon: 'pi-calendar',     textColor: 'text-blue-600' },
                        { key: 'in_progress', label: 'กำลังซ่อม',   count: stats.inProgress,  activeColor: 'border-indigo-300 ring-2 ring-indigo-200',iconBg: 'bg-indigo-100',  icon: 'pi-wrench',       textColor: 'text-indigo-600' },
                        { key: 'resolved',    label: 'รอปิดงาน',    count: stats.resolved,    activeColor: 'border-emerald-300 ring-2 ring-emerald-200',iconBg: 'bg-emerald-100',icon: 'pi-check-circle', textColor: 'text-emerald-600' },
                    ]" :key="stat.key"
                        @click="activeFilter = (activeFilter === stat.key ? 'all' : stat.key) as typeof activeFilter"
                        class="bg-white rounded-xl border p-4 flex items-center gap-3 text-left transition-all hover:shadow-md"
                        :class="activeFilter === stat.key ? stat.activeColor : 'border-gray-200'">
                        <div class="w-10 h-10 rounded-full flex items-center justify-center shrink-0" :class="stat.iconBg">
                            <i class="pi" :class="[stat.icon, stat.textColor]"></i>
                        </div>
                        <div>
                            <p class="text-2xl font-bold" :class="stat.textColor">{{ stat.count }}</p>
                            <p class="text-xs text-gray-500">{{ stat.label }}</p>
                        </div>
                    </button>
                </div>

                <!-- Filter pill -->
                <div v-if="activeFilter !== 'all'" class="flex items-center gap-2 text-sm text-gray-500">
                    <span>กรองอยู่:</span>
                    <span class="inline-flex items-center gap-1.5 bg-gray-100 text-gray-700 rounded-full px-3 py-1 font-medium">
                        {{ activeFilter === 'waiting' ? 'รอจัดจ้าง' : activeFilter === 'scheduled' ? 'นัดช่างแล้ว' : activeFilter === 'in_progress' ? 'กำลังซ่อม' : 'รอปิดงาน' }}
                        <button @click="activeFilter = 'all'" class="text-gray-400 hover:text-gray-700 ml-1">
                            <i class="pi pi-times text-xs"></i>
                        </button>
                    </span>
                </div>

                <!-- Loading -->
                <div v-if="loading" class="space-y-3">
                    <div v-for="n in 3" :key="n" class="bg-white rounded-2xl border border-gray-100 p-5 animate-pulse">
                        <div class="flex gap-4">
                            <div class="w-11 h-11 bg-gray-200 rounded-xl shrink-0"></div>
                            <div class="flex-1 space-y-2">
                                <div class="h-4 bg-gray-200 rounded w-1/3"></div>
                                <div class="h-3 bg-gray-100 rounded w-2/3"></div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Empty -->
                <div v-else-if="filteredItems.length === 0"
                    class="bg-white rounded-2xl border border-dashed border-gray-200 p-12 text-center">
                    <div class="w-16 h-16 bg-gray-100 rounded-full flex items-center justify-center mx-auto mb-4">
                        <i class="pi pi-inbox text-2xl text-gray-400"></i>
                    </div>
                    <p class="text-gray-500 font-medium">ไม่มีงานในคิว</p>
                    <p class="text-sm text-gray-400 mt-1">งานจ้างช่างภายนอกจะปรากฏที่นี่เมื่อหัวหน้าช่างอนุมัติ</p>
                </div>

                <!-- Work order cards -->
                <div v-else class="space-y-3">
                    <div v-for="item in filteredItems" :key="item.id"
                        class="bg-white rounded-2xl border border-gray-200 shadow-sm hover:shadow-md transition-shadow overflow-hidden">
                        <div class="h-1 w-full" :class="statusInfo(item.status).barColor"></div>
                        <div class="p-5">
                            <div class="flex flex-col sm:flex-row sm:items-start gap-4">
                                <!-- Icon -->
                                <div class="w-11 h-11 rounded-xl flex items-center justify-center shrink-0"
                                    :class="item.status.startsWith('waiting_') ? 'bg-red-50' : item.status === 'external_scheduled' ? 'bg-blue-50' : item.status === 'external_in_progress' ? 'bg-indigo-50' : 'bg-emerald-50'">
                                    <i class="pi text-lg"
                                        :class="[statusInfo(item.status).icon,
                                                 item.status.startsWith('waiting_') ? 'text-red-500'
                                               : item.status === 'external_scheduled' ? 'text-blue-500'
                                               : item.status === 'external_in_progress' ? 'text-indigo-500'
                                               : 'text-emerald-500']"></i>
                                </div>

                                <!-- Content -->
                                <div class="flex-1 min-w-0">
                                    <div class="flex flex-wrap items-center gap-2 mb-1">
                                        <span class="text-xs font-mono font-bold text-gray-400">{{ item.workOrderNo || '#' + item.id.slice(0,6).toUpperCase() }}</span>
                                        <span class="inline-flex items-center gap-1 text-xs font-semibold px-2 py-0.5 rounded-full border"
                                            :class="statusInfo(item.status).color">
                                            <span class="w-1.5 h-1.5 rounded-full" :class="statusInfo(item.status).dot"></span>
                                            {{ statusInfo(item.status).label }}
                                        </span>
                                        <span class="text-[10px] font-semibold px-2 py-0.5 rounded-full"
                                            :class="item.isCentralAsset ? 'bg-purple-100 text-purple-700' : 'bg-sky-100 text-sky-700'">
                                            {{ item.isCentralAsset ? 'ส่วนกลาง' : 'หน่วยงาน' }}
                                        </span>
                                    </div>
                                    <h3 class="font-bold text-gray-800">{{ item.title }}</h3>
                                    <div class="mt-1.5 flex flex-wrap gap-x-4 gap-y-0.5 text-xs text-gray-500">
                                        <span v-if="item.buildingName" class="flex items-center gap-1">
                                            <i class="pi pi-building"></i> {{ item.buildingName }}<span v-if="item.locationDetail"> · {{ item.locationDetail }}</span>
                                        </span>
                                        <span v-if="item.requesterDepartmentName" class="flex items-center gap-1">
                                            <i class="pi pi-sitemap"></i> {{ item.requesterDepartmentName }}
                                        </span>
                                    </div>
                                    <div v-if="item.supervisorReason || item.supervisorExternalAdvice"
                                        class="mt-2 bg-amber-50 border border-amber-200 rounded-lg px-3 py-1.5 text-xs text-amber-800">
                                        <i class="pi pi-comment mr-1"></i>
                                        <span class="font-semibold">หัวหน้าช่าง:</span> {{ item.supervisorReason || item.supervisorExternalAdvice }}
                                    </div>
                                    <div v-if="item.externalVendorName || item.externalScheduledAt" class="mt-2 flex flex-wrap gap-2">
                                        <span v-if="item.externalVendorName"
                                            class="flex items-center gap-1 text-xs bg-slate-50 border border-slate-200 rounded-lg px-2 py-1">
                                            <i class="pi pi-user text-slate-400"></i>
                                            <span class="font-medium text-slate-700">{{ item.externalVendorName }}</span>
                                        </span>
                                        <span v-if="item.externalScheduledAt"
                                            class="flex items-center gap-1 text-xs bg-blue-50 border border-blue-200 rounded-lg px-2 py-1 text-blue-700">
                                            <i class="pi pi-calendar-clock"></i> {{ formatDate(item.externalScheduledAt) }}
                                        </span>
                                        <span v-if="item.externalCompletedAt"
                                            class="flex items-center gap-1 text-xs bg-emerald-50 border border-emerald-200 rounded-lg px-2 py-1 text-emerald-700">
                                            <i class="pi pi-check"></i> เสร็จ: {{ formatDate(item.externalCompletedAt) }}
                                        </span>
                                    </div>
                                </div>

                                <!-- Actions -->
                                <div class="shrink-0 flex flex-col items-end gap-2">
                                    <Button v-if="item.status.startsWith('waiting_')"
                                        label="รับงาน" icon="pi pi-plus" severity="danger" size="small"
                                        @click="openDialog(item, 'accept')" />
                                    <Button v-else-if="item.status === 'external_scheduled'"
                                        label="เริ่มซ่อม" icon="pi pi-wrench" severity="warn" size="small"
                                        @click="openDialog(item, 'start')" />
                                    <Button v-else-if="item.status === 'external_in_progress'"
                                        label="บันทึกเสร็จ" icon="pi pi-check" severity="success" size="small"
                                        @click="openDialog(item, 'complete')" />
                                    <Button v-else-if="item.status === 'resolved'"
                                        label="ปิดงาน" icon="pi pi-lock" size="small"
                                        @click="openDialog(item, 'close')" />
                                    <Button v-if="item.status !== 'resolved'"
                                        label="แก้ไข" icon="pi pi-pencil" text size="small" severity="secondary"
                                        @click="openDialog(item, 'edit')" />
                                    <button
                                        @click="router.push({ path: '/maintenance/external-timeline', query: { id: item.id } })"
                                        class="text-xs text-indigo-500 hover:text-indigo-700 flex items-center gap-1">
                                        <i class="pi pi-history text-xs"></i> Timeline
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </template>

            <!-- ─── Dialog: บันทึกตาม action ─── -->
            <Dialog v-model:visible="dialogVisible" modal :style="{ width: '44rem' }" :breakpoints="{ '640px': '95vw' }">
                <template #header>
                    <div class="flex items-center gap-3">
                        <div class="w-10 h-10 rounded-xl bg-orange-100 flex items-center justify-center shrink-0">
                            <i class="pi pi-briefcase text-orange-600"></i>
                        </div>
                        <div>
                            <p class="font-bold text-gray-800">{{ actionLabel }}</p>
                            <p class="text-xs text-gray-400 mt-0.5">
                                {{ selectedRequest?.workOrderNo }}
                                <span v-if="selectedRequest?.title"> — {{ selectedRequest.title }}</span>
                            </p>
                        </div>
                    </div>
                </template>

                <div class="space-y-5 py-1">

                    <!-- Current status -->
                    <div v-if="selectedRequest"
                        class="flex items-center gap-2 rounded-xl px-4 py-3 border text-sm font-medium"
                        :class="statusInfo(selectedRequest.status).color">
                        <i class="pi" :class="statusInfo(selectedRequest.status).icon"></i>
                        สถานะปัจจุบัน: {{ statusInfo(selectedRequest.status).label }}
                    </div>

                    <!-- ACCEPT / EDIT: ชื่อช่าง + วันนัด -->
                    <template v-if="actionMode === 'accept' || actionMode === 'edit'">
                        <div class="space-y-1.5">
                            <label class="text-sm font-semibold text-gray-700">
                                ชื่อบริษัท / ช่างภายนอก <span class="text-red-500">*</span>
                            </label>
                            <InputText v-model="form.vendorName" class="w-full"
                                placeholder="เช่น บริษัท ABC จำกัด หรือ นาย สมชาย ช่างไฟ" />
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-sm font-semibold text-gray-700">
                                วัน-เวลา นัดหมาย <span class="text-red-500">*</span>
                            </label>
                            <DatePicker v-model="form.scheduledAt" showTime hourFormat="24" dateFormat="dd/mm/yy"
                                class="w-full" showIcon placeholder="เลือกวันที่นัดหมายช่าง" />
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-sm font-semibold text-gray-700">หมายเหตุ (ไม่บังคับ)</label>
                            <Textarea v-model="form.result" rows="2" class="w-full" placeholder="รายละเอียดเพิ่มเติม..." />
                        </div>
                        <div class="rounded-xl bg-blue-50 border border-blue-200 px-4 py-3 text-sm text-blue-800">
                            <i class="pi pi-info-circle mr-1.5"></i>
                            หลังบันทึก status จะเปลี่ยนเป็น <strong>นัดช่างแล้ว</strong>
                        </div>
                    </template>

                    <!-- START: ยืนยันเริ่มซ่อม -->
                    <template v-else-if="actionMode === 'start'">
                        <div class="rounded-xl bg-slate-50 border border-slate-200 px-4 py-3 space-y-1">
                            <p class="text-sm font-semibold text-slate-700">ช่าง / บริษัทที่นัดไว้</p>
                            <p class="text-base font-bold text-slate-800">{{ selectedRequest?.externalVendorName || '-' }}</p>
                            <p class="text-xs text-slate-500">วันนัด: {{ formatDate(selectedRequest?.externalScheduledAt) }}</p>
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-sm font-semibold text-gray-700">บันทึกเพิ่มเติม (ไม่บังคับ)</label>
                            <Textarea v-model="form.result" rows="2" class="w-full" placeholder="ข้อสังเกต เช่น ช่างมาถึงเวลา 09.00 น." />
                        </div>
                        <div class="rounded-xl bg-amber-50 border border-amber-200 px-4 py-3 text-sm text-amber-800">
                            <i class="pi pi-info-circle mr-1.5"></i>
                            หลังบันทึก status จะเปลี่ยนเป็น <strong>ช่างกำลังซ่อม</strong>
                        </div>
                    </template>

                    <!-- COMPLETE: ซ่อมเสร็จ -->
                    <template v-else-if="actionMode === 'complete'">
                        <div class="rounded-xl bg-slate-50 border border-slate-200 px-4 py-3">
                            <p class="text-xs text-gray-400 mb-0.5">ช่าง / บริษัทที่ดำเนินการ</p>
                            <p class="font-bold text-slate-800">{{ selectedRequest?.externalVendorName || '-' }}</p>
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-sm font-semibold text-gray-700">
                                วัน-เวลา ซ่อมเสร็จ <span class="text-red-500">*</span>
                            </label>
                            <DatePicker v-model="form.completedAt" showTime hourFormat="24" dateFormat="dd/mm/yy"
                                class="w-full" showIcon placeholder="ระบุวันที่ซ่อมเสร็จ" />
                        </div>
                        <div class="space-y-1.5">
                            <label class="text-sm font-semibold text-gray-700">
                                ผลการซ่อม / ค่าใช้จ่าย
                            </label>
                            <Textarea v-model="form.result" rows="3" class="w-full"
                                placeholder="เช่น ซ่อมเสร็จ ปกติ ค่าแรง 2,500 บาท ค่าอะไหล่ 800 บาท รวม 3,300 บาท" />
                        </div>
                        <div class="flex items-start gap-3 rounded-xl border border-gray-200 bg-gray-50 px-4 py-3">
                            <ToggleSwitch v-model="form.closeAfterComplete" class="shrink-0 mt-0.5" />
                            <div>
                                <p class="text-sm font-semibold text-gray-700">ปิดงานทันที</p>
                                <p class="text-xs text-gray-400 mt-0.5">เปิดเมื่อซ่อมเสร็จสมบูรณ์ ตรวจรับงานแล้ว ต้องการปิด Ticket พร้อมกัน</p>
                            </div>
                        </div>
                    </template>

                    <!-- CLOSE: ปิดงาน -->
                    <template v-else-if="actionMode === 'close'">
                        <div class="space-y-2">
                            <div class="rounded-xl bg-emerald-50 border border-emerald-200 px-4 py-3">
                                <p class="text-xs text-gray-400 mb-0.5">ผลการซ่อมที่บันทึกไว้</p>
                                <p class="text-sm text-gray-700">{{ selectedRequest?.externalResult || '(ไม่มีบันทึก)' }}</p>
                            </div>
                            <div class="space-y-1.5">
                                <label class="text-sm font-semibold text-gray-700">หมายเหตุปิดงาน (ไม่บังคับ)</label>
                                <Textarea v-model="form.result" rows="2" class="w-full" placeholder="หมายเหตุเพิ่มเติมสำหรับการปิดงาน..." />
                            </div>
                        </div>
                        <div class="rounded-xl bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-800">
                            <i class="pi pi-exclamation-triangle mr-1.5"></i>
                            หลังปิดงานแล้ว <strong>ไม่สามารถแก้ไขได้</strong> กรุณาตรวจสอบข้อมูลก่อนกด
                        </div>
                    </template>

                    <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>
                </div>

                <template #footer>
                    <div class="flex gap-2 justify-end w-full">
                        <Button label="ยกเลิก" text severity="secondary" @click="dialogVisible = false" />
                        <Button
                            :label="actionMode === 'accept' ? 'ยืนยันรับงาน'
                                  : actionMode === 'start' ? 'ยืนยันเริ่มซ่อม'
                                  : actionMode === 'complete' ? 'บันทึกงานเสร็จ'
                                  : actionMode === 'close' ? 'ปิดงาน'
                                  : 'บันทึก'"
                            :icon="actionMode === 'close' ? 'pi pi-lock' : 'pi pi-check'"
                            :severity="actionMode === 'close' ? 'danger' : 'primary'"
                            :loading="saving"
                            @click="saveAction" />
                    </div>
                </template>
            </Dialog>

        </template>
    </div>
</template>
