<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/services/api'
import {
    joinRequestGroup,
    leaveRequestGroup,
    offRealtimeEvent,
    onRealtimeEvent,
    startRealtimeConnection,
} from '@/services/realtime'
import { useAuthStore } from '@/stores/auth'
import { usePermissions } from '@/composables/usePermissions'
import {
    MAINTENANCE_ADMIN_BUILDING_PERMISSION,
    MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
    MAINTENANCE_SUPERVISOR_PERMISSION,
    MAINTENANCE_TECHNICIAN_PERMISSION,
    hasMaintenancePermission,
} from '@/config/maintenancePermissions'

import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import Message from 'primevue/message'

interface ServiceRequest {
    id: string
    workOrderNo?: string
    title: string
    description: string
    category: string
    priority: string
    status: string
    assetNumber?: string
    isCentralAsset?: boolean
    buildingName?: string
    locationDetail?: string
    extension: string
    requesterName: string
    requesterEmail: string
    requesterUid?: string
    technicianUid?: string
    technicianName?: string
    technicianDiagnosis?: string
    technicianAction?: string
    escalationReason?: string
    supervisorReason?: string
    supervisorRepairPlan?: string
    supervisorExternalAdvice?: string
    externalVendorName?: string
    externalResult?: string
    assignedTo?: string
    note?: string
    createdAt: string | null
    updatedAt?: string | null
}

interface ChatMessage {
    id: string
    text: string
    senderName: string
    senderEmail: string
    senderId: string
    senderRole: string
    isRead: boolean
    readAt: string | null
    readById: string
    createdAt: string | null
}

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const requestId = route.params.id as string

const request = ref<ServiceRequest | null>(null)
const messages = ref<ChatMessage[]>([])
const loadingRequest = ref(true)
const sending = ref(false)
const messageText = ref('')
const errorMessage = ref('')
const successMessage = ref('')
const chatBottom = ref<HTMLElement | null>(null)

const completeVisible = ref(false)
const escalateVisible = ref(false)
const reviewVisible = ref(false)
const externalVisible = ref(false)
const requesterEditVisible = ref(false)
const requesterCancelVisible = ref(false)
const actionLoading = ref(false)
const isPrinting = ref(false)
const printGeneratedAt = ref('')

const completeForm = ref({ diagnosis: '', action: '', note: '' })
const escalateForm = ref({ diagnosis: '', escalationReason: '' })
const reviewForm = ref({ canRepairInHouse: true, reason: '', repairPlan: '', externalAdvice: '' })
const externalForm = ref({
    vendorName: '',
    scheduledAt: null as Date | null,
    completedAt: null as Date | null,
    result: '',
    closeAfterComplete: false,
})
const requesterEditForm = ref({
    title: '',
    description: '',
    buildingName: '',
    locationDetail: '',
    extension: '',
})
const requesterCancelReason = ref('')

const currentUser = computed(() => authStore.user)
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('maintenance')
const roleText = computed(() => (currentUser.value?.role ?? '').trim().toLowerCase())
const currentUid = computed(() => currentUser.value?.uid ?? currentUser.value?.id ?? '')
const maintenanceAdminSystems = computed(() => authStore.userProfile?.adminSystems ?? [])
const canByMaintenancePermission = (permission: string): boolean =>
    hasMaintenancePermission(maintenanceAdminSystems.value, permission)

const canTechnicianAction = computed(() =>
    ['superadmin', 'technician'].includes(roleText.value) ||
    canByMaintenancePermission(MAINTENANCE_TECHNICIAN_PERMISSION),
)
const canSupervisorAction = computed(() =>
    isAdmin ||
    ['superadmin', 'supervisor', 'admin'].includes(roleText.value) ||
    canByMaintenancePermission(MAINTENANCE_SUPERVISOR_PERMISSION),
)
const canDepartmentExternalAction = computed(() =>
    ['superadmin', 'admin'].includes(roleText.value) ||
    canByMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_PERMISSION),
)
const canCentralExternalAction = computed(() =>
    ['superadmin', 'adminbuilding'].includes(roleText.value) ||
    canByMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION),
)
const canAdminBuildingAction = computed(() => canDepartmentExternalAction.value || canCentralExternalAction.value)
const isCentralAssetValue = computed(() => {
    const raw = request.value?.isCentralAsset as unknown
    return raw === true || raw === 'true' || raw === 1 || raw === '1'
})
const isRequester = computed(() => {
    if (!request.value || !currentUser.value) return false
    if (request.value.requesterUid && request.value.requesterUid === currentUid.value) return true
    return request.value.requesterEmail === currentUser.value.email
})
const canRequesterActions = computed(() =>
    isRequester.value &&
    !canTechnicianAction.value &&
    !canSupervisorAction.value &&
    !canAdminBuildingAction.value &&
    !isAdmin,
)
const canRequesterEditWorkOrder = computed(() => {
    if (!canRequesterActions.value || !request.value) return false
    return ['new', 'assigned'].includes(request.value.status)
})
const showCentralAssetExternalNotice = computed(() => {
    if (!canRequesterActions.value || !request.value) return false
    return request.value.status === 'waiting_central_external_procurement' && isCentralAssetValue.value
})
const showRequesterLockedEditNotice = computed(() => {
    if (!canRequesterActions.value) return false
    return !canRequesterEditWorkOrder.value
})
const showRequesterCentralCloseNotice = computed(() => {
    if (!canRequesterActions.value) return false
    return isCentralAssetValue.value && request.value?.status !== 'new' && request.value?.status !== 'assigned'
})
const showRequesterExternalHandledByAdminNotice = computed(() => {
    if (!canRequesterActions.value || !request.value) return false
    return [
        'waiting_department_external_procurement',
        'waiting_central_external_procurement',
        'external_scheduled',
        'external_in_progress',
        'resolved',
    ].includes(request.value.status)
})
const canStartWork = computed(() => {
    if (!request.value) return false
    if (!['new', 'assigned', 'returned_to_technician'].includes(request.value.status)) return false
    if (!canTechnicianAction.value) return false
    if (!request.value.technicianUid) return true
    return request.value.technicianUid === currentUid.value || roleText.value === 'superadmin'
})
const canEscalateToSupervisor = computed(() => request.value?.status === 'in_progress')
const canCloseCompleted = computed(() =>
    request.value?.status === 'in_progress' || request.value?.status === 'returned_to_technician'
)

async function printWorkOrder() {
    if (!request.value) return

    printGeneratedAt.value = formatDate(new Date().toISOString())

    isPrinting.value = true
    await nextTick()

    const handleAfterPrint = () => {
        isPrinting.value = false
    }

    window.addEventListener('afterprint', handleAfterPrint, { once: true })
    window.print()
}

function parseBackendDate(dateStr: string | null | undefined): Date | null {
    if (!dateStr) return null
    const hasTimezone = /([zZ]|[+\-]\d{2}:\d{2})$/.test(dateStr)
    const normalized = hasTimezone ? dateStr : `${dateStr}Z`
    const parsed = new Date(normalized)
    return Number.isNaN(parsed.getTime()) ? null : parsed
}

const timelineItems = computed(() => {
    if (!request.value) return []

    const events: Array<{ label: string; time: string; state: 'done' | 'current' }> = [
        {
            label: `แจ้งซ่อม (${statusTag('new').label})`,
            time: formatDate(request.value.createdAt),
            state: 'done',
        },
    ]

    const timelineMessages = messages.value
        .filter((m) => m.senderRole === 'system' && m.text.startsWith('[timeline]'))
        .sort((a, b) => {
            const aTime = parseBackendDate(a.createdAt)?.getTime() ?? 0
            const bTime = parseBackendDate(b.createdAt)?.getTime() ?? 0
            return aTime - bTime
        })

    timelineMessages.forEach((msg) => {
        events.push({
            label: msg.text.replace('[timeline]', '').trim(),
            time: formatDate(msg.createdAt),
            state: 'done',
        })
    })

    const currentLabel = `สถานะปัจจุบัน: ${statusTag(request.value.status).label}`
    const lastLabel = events[events.length - 1]?.label || ''
    if (lastLabel !== currentLabel) {
        events.push({
            label: currentLabel,
            time: formatDate(request.value.updatedAt || request.value.createdAt),
            state: 'current',
        })
    } else {
        const lastEvent = events[events.length - 1]
        if (lastEvent) lastEvent.state = 'current'
    }

    return events
})

const canChat = computed(() => {
    if (!currentUser.value || !request.value) return false
    if (isAdmin) return true
    if (request.value.requesterEmail === currentUser.value.email) return true
    if (request.value.technicianUid && request.value.technicianUid === currentUid.value) return true
    return false
})

const unreadCount = computed(() =>
    messages.value.filter((m) => !m.isRead && m.senderId !== currentUid.value).length,
)

async function loadRequest() {
    try {
        const res = await api.get(`/ServiceRequest/${requestId}`)
        request.value = res.data
        loadingRequest.value = false
    } catch {
        loadingRequest.value = false
        errorMessage.value = 'ไม่พบรายละเอียดใบงาน หรือคุณไม่มีสิทธิ์เข้าถึงใบงานนี้'
    }
}

function openRequesterEdit() {
    if (!request.value) return
    requesterEditForm.value = {
        title: request.value.title,
        description: request.value.description,
        buildingName: request.value.buildingName || '',
        locationDetail: request.value.locationDetail || '',
        extension: request.value.extension || '',
    }
    requesterEditVisible.value = true
}

async function loadMessages() {
    try {
        const res = await api.get(`/ServiceRequest/${requestId}/messages`)
        messages.value = res.data
        scrollToBottom()
    } catch {
        // ignore
    }
}

async function markMessagesRead() {
    try {
        await api.put(`/ServiceRequest/${requestId}/messages/read`)
    } catch {
        // ignore
    }
}

async function refreshChat() {
    await loadMessages()
    if (unreadCount.value > 0) {
        await markMessagesRead()
        await loadMessages()
    }
}

const handleRealtimeChatEvent = async (payload: unknown) => {
    const data = payload as { requestId?: string }
    if (data.requestId !== requestId) return
    await refreshChat()
}

const handleRealtimeTimelineEvent = async (payload: unknown) => {
    const data = payload as { requestId?: string }
    if (data.requestId !== requestId) return
    await loadRequest()
    await loadMessages()
}

onMounted(async () => {
    await loadRequest()
    await refreshChat()

    try {
        await startRealtimeConnection()
        await joinRequestGroup(requestId)
        onRealtimeEvent('ChatMessageCreated', handleRealtimeChatEvent)
        onRealtimeEvent('RequestTimelineChanged', handleRealtimeTimelineEvent)
        onRealtimeEvent('MessagesMarkedRead', handleRealtimeChatEvent)
    } catch {
        // หากเชื่อม realtime ไม่สำเร็จ ผู้ใช้ยังสามารถรีเฟรชหน้าเพื่อดึงข้อมูลล่าสุดได้
    }
})

onUnmounted(() => {
    offRealtimeEvent('ChatMessageCreated', handleRealtimeChatEvent)
    offRealtimeEvent('RequestTimelineChanged', handleRealtimeTimelineEvent)
    offRealtimeEvent('MessagesMarkedRead', handleRealtimeChatEvent)
    leaveRequestGroup(requestId)
})

function formatDate(dateStr: string | null | undefined): string {
    const parsed = parseBackendDate(dateStr)
    if (!parsed) return ''
    return parsed.toLocaleString('th-TH', {
        year: 'numeric', month: 'short', day: 'numeric',
        hour: '2-digit', minute: '2-digit',
        timeZone: 'Asia/Bangkok',
    })
}

function formatTime(dateStr: string | null | undefined): string {
    const parsed = parseBackendDate(dateStr)
    if (!parsed) return ''
    return parsed.toLocaleTimeString('th-TH', {
        hour: '2-digit',
        minute: '2-digit',
        timeZone: 'Asia/Bangkok',
    })
}

function categoryLabel(val: string): string {
    const map: Record<string, string> = {
        electrical: 'งานระบบไฟฟ้า',
        plumbing: 'งานระบบประปา',
        building: 'งานโครงสร้าง/อาคาร',
        hvac: 'งานระบบปรับอากาศ',
        furniture: 'งานเฟอร์นิเจอร์/ครุภัณฑ์',
        other: 'อื่น ๆ',
    }
    return map[val] ?? val
}

function priorityTag(val: string): { severity: 'info' | 'warn' | 'danger'; label: string } {
    if (val === 'low') return { severity: 'info', label: 'ต่ำ' }
    if (val === 'medium') return { severity: 'info', label: 'ปานกลาง' }
    if (val === 'urgent') return { severity: 'warn', label: 'เร่งด่วน' }
    if (val === 'critical') return { severity: 'danger', label: 'วิกฤต' }
    return { severity: 'info', label: val }
}

function statusTag(val: string): { severity: 'secondary' | 'info' | 'success' | 'danger'; label: string } {
    if (val === 'new') return { severity: 'secondary', label: 'รอช่างอาคารประเมิน' }
    if (val === 'assigned') return { severity: 'info', label: 'มอบหมายแล้ว' }
    if (val === 'in_progress') return { severity: 'info', label: 'กำลังดำเนินการ' }
    if (val === 'need_supervisor_review') return { severity: 'danger', label: 'รอหัวหน้าพิจารณา' }
    if (val === 'returned_to_technician') return { severity: 'info', label: 'ส่งกลับให้ช่าง' }
    if (val === 'waiting_department_external_procurement') return { severity: 'danger', label: 'รอหน่วยงาน/กอง จัดจ้างช่างภายนอก' }
    if (val === 'waiting_central_external_procurement') return { severity: 'danger', label: 'รอธุรการส่วนกลาง จัดจ้างช่างภายนอก' }
    if (val === 'external_scheduled') return { severity: 'info', label: 'นัดช่างภายนอกแล้ว' }
    if (val === 'external_in_progress') return { severity: 'info', label: 'ช่างภายนอกกำลังซ่อม' }
    if (val === 'resolved') return { severity: 'success', label: 'ซ่อมเสร็จ' }
    if (val === 'closed') return { severity: 'success', label: 'ปิดงาน' }
    return { severity: 'secondary', label: val }
}

function formatPersonName(name: string | null | undefined, email: string | null | undefined): string {
    const candidate = (name || '').trim()
    if (candidate && !candidate.includes('@')) return candidate

    const emailSource = (email || candidate).trim()
    if (!emailSource) return '-'

    const localPart = emailSource.split('@')[0] || emailSource
    return localPart.replace(/[._-]+/g, ' ')
}

function isMine(msg: ChatMessage): boolean {
    return msg.senderId === currentUid.value
}

function scrollToBottom() {
    nextTick(() => {
        if (chatBottom.value) chatBottom.value.scrollIntoView({ behavior: 'smooth' })
    })
}

async function sendMessage() {
    const text = messageText.value.trim()
    if (!text || !currentUser.value) return
    sending.value = true
    try {
        await api.post(`/ServiceRequest/${requestId}/messages`, { text })
        messageText.value = ''
        await refreshChat()
    } catch (e) {
        errorMessage.value = `ส่งข้อความไม่สำเร็จ: ${String(e)}`
    } finally {
        sending.value = false
    }
}

function handleEnter(e: KeyboardEvent) {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault()
        sendMessage()
    }
}

function goBack() {
    const prev = router.options.history.state.back as string | undefined
    if (prev && prev.startsWith('/maintenance') && !prev.includes('/service/')) {
        router.push(prev)
    } else if (canSupervisorAction.value && !canTechnicianAction.value) {
        router.push('/maintenance/supervisor-review')
    } else if (canAdminBuildingAction.value && !canTechnicianAction.value) {
        router.push('/maintenance/external-procurement')
    } else {
        router.push('/maintenance/service')
    }
}

async function startWork() {
    if (!request.value) return
    actionLoading.value = true
    try {
        await api.put(`/ServiceRequest/${request.value.id}/technician-start`, { note: '' })
        successMessage.value = 'รับงานเรียบร้อยแล้ว'
        await loadRequest()
    } catch {
        errorMessage.value = 'ไม่สามารถรับงานได้'
    } finally {
        actionLoading.value = false
    }
}

async function submitComplete() {
    if (!request.value) return
    if (!completeForm.value.diagnosis.trim() || !completeForm.value.action.trim()) {
        errorMessage.value = 'กรุณากรอกผลวิเคราะห์และวิธีซ่อมให้ครบ'
        return
    }

    actionLoading.value = true
    try {
        await api.put(`/ServiceRequest/${request.value.id}/technician-complete`, {
            diagnosis: completeForm.value.diagnosis,
            action: completeForm.value.action,
            note: completeForm.value.note,
        })
        completeVisible.value = false
        successMessage.value = 'บันทึกผลซ่อมเรียบร้อยแล้ว'
        await loadRequest()
    } catch {
        errorMessage.value = 'บันทึกผลซ่อมไม่สำเร็จ'
    } finally {
        actionLoading.value = false
    }
}

async function submitEscalate() {
    if (!request.value) return
    if (!escalateForm.value.escalationReason.trim()) {
        errorMessage.value = 'กรุณาระบุเหตุผลที่ต้องส่งหัวหน้าพิจารณา'
        return
    }

    actionLoading.value = true
    try {
        await api.put(`/ServiceRequest/${request.value.id}/technician-escalate`, {
            diagnosis: escalateForm.value.diagnosis,
            escalationReason: escalateForm.value.escalationReason,
        })
        escalateVisible.value = false
        successMessage.value = 'ส่งหัวหน้าช่างตรวจสอบเรียบร้อยแล้ว'
        await loadRequest()
    } catch {
        errorMessage.value = 'ส่งเรื่องไม่สำเร็จ'
    } finally {
        actionLoading.value = false
    }
}

async function submitReview() {
    if (!request.value) return
    if (!reviewForm.value.reason.trim()) {
        errorMessage.value = 'กรุณาระบุเหตุผลการพิจารณา'
        return
    }

    actionLoading.value = true
    try {
        await api.put(`/ServiceRequest/${request.value.id}/supervisor-review`, {
            canRepairInHouse: reviewForm.value.canRepairInHouse,
            reason: reviewForm.value.reason,
            repairPlan: reviewForm.value.repairPlan,
            externalAdvice: reviewForm.value.externalAdvice,
        })
        reviewVisible.value = false
        successMessage.value = 'บันทึกผลพิจารณาเรียบร้อยแล้ว'
        await loadRequest()
    } catch {
        errorMessage.value = 'บันทึกผลพิจารณาไม่สำเร็จ'
    } finally {
        actionLoading.value = false
    }
}

async function submitExternalProgress() {
    if (!request.value) return
    if (!externalForm.value.vendorName.trim()) {
        errorMessage.value = 'กรุณาระบุชื่อช่าง/บริษัทภายนอก'
        return
    }

    actionLoading.value = true
    try {
        await api.put(`/ServiceRequest/${request.value.id}/external-progress`, {
            vendorName: externalForm.value.vendorName,
            scheduledAt: externalForm.value.scheduledAt ? externalForm.value.scheduledAt.toISOString() : null,
            completedAt: externalForm.value.completedAt ? externalForm.value.completedAt.toISOString() : null,
            result: externalForm.value.result,
            closeAfterComplete: externalForm.value.closeAfterComplete,
        })
        externalVisible.value = false
        successMessage.value = 'อัปเดตงานช่างนอกเรียบร้อยแล้ว'
        await loadRequest()
    } catch {
        errorMessage.value = 'อัปเดตงานช่างนอกไม่สำเร็จ'
    } finally {
        actionLoading.value = false
    }
}

async function submitRequesterEdit() {
    if (!request.value) return
    if (!canRequesterEditWorkOrder.value) {
        errorMessage.value = 'ช่างรับงานแล้ว ผู้แจ้งงานไม่สามารถแก้ไขใบงานได้'
        return
    }
    if (!requesterEditForm.value.title.trim() || !requesterEditForm.value.description.trim()) {
        errorMessage.value = 'กรุณากรอกหัวข้อและรายละเอียดงานให้ครบ'
        return
    }

    actionLoading.value = true
    try {
        await api.put(`/ServiceRequest/${request.value.id}/requester-edit`, {
            title: requesterEditForm.value.title,
            description: requesterEditForm.value.description,
            buildingName: requesterEditForm.value.buildingName,
            locationDetail: requesterEditForm.value.locationDetail,
            extension: requesterEditForm.value.extension,
        })
        requesterEditVisible.value = false
        successMessage.value = 'แก้ไขใบงานเรียบร้อยแล้ว'
        await loadRequest()
    } catch {
        errorMessage.value = 'ไม่สามารถแก้ไขใบงานได้'
    } finally {
        actionLoading.value = false
    }
}

async function cancelByRequester() {
    if (!request.value) return
    if (!canRequesterEditWorkOrder.value) {
        errorMessage.value = 'ยกเลิกใบงานได้เฉพาะก่อนช่างเริ่มรับงาน'
        return
    }
    actionLoading.value = true
    try {
        await api.put(`/ServiceRequest/${request.value.id}/requester-cancel`, {
            reason: requesterCancelReason.value || '-',
        })
        requesterCancelVisible.value = false
        requesterCancelReason.value = ''
        successMessage.value = 'ยกเลิกใบงานเรียบร้อยแล้ว'
        await loadRequest()
    } catch {
        errorMessage.value = 'ไม่สามารถยกเลิกใบงานได้'
    } finally {
        actionLoading.value = false
    }
}
</script>

<template>
    <div class="flex flex-col h-full -m-6">
        <div v-if="loadingRequest && !isPrinting" class="flex items-center justify-center h-64 text-gray-400">
            <i class="pi pi-spin pi-spinner mr-2"></i> กำลังโหลด...
        </div>

        <div v-if="isPrinting && request" class="print-sheet">
            <div class="print-header">
                <h1>ใบงานแจ้งซ่อม</h1>
                <p>เลขใบงาน: {{ request.workOrderNo || '-' }}</p>
                <p>พิมพ์เมื่อ: {{ printGeneratedAt || '-' }}</p>
            </div>

            <div class="print-section">
                <h2>ข้อมูลใบงาน</h2>
                <div class="print-grid">
                    <div><strong>หัวข้อ:</strong> {{ request.title }}</div>
                    <div><strong>สถานะ:</strong> {{ statusTag(request.status).label }}</div>
                    <div><strong>ผู้แจ้ง:</strong> {{ formatPersonName(request.requesterName, request.requesterEmail) }}
                    </div>
                    <div><strong>วันที่แจ้ง:</strong> {{ formatDate(request.createdAt) }}</div>
                    <div><strong>หมวดงาน:</strong> {{ categoryLabel(request.category) }}</div>
                    <div><strong>ความสำคัญ:</strong> {{ priorityTag(request.priority).label }}</div>
                    <div><strong>อาคาร:</strong> {{ request.buildingName || '-' }}</div>
                    <div><strong>จุดที่พบปัญหา:</strong> {{ request.locationDetail || '-' }}</div>
                    <div><strong>เบอร์ติดต่อ:</strong> {{ request.extension || '-' }}</div>
                    <div><strong>เลขรหัสทรัพย์สิน:</strong> {{ request.assetNumber || '-' }}</div>
                    <div><strong>ประเภททรัพย์สิน:</strong> {{ request.isCentralAsset ? 'ทรัพย์สินส่วนกลาง' :
                        'ทรัพย์สินของกอง/หน่วยงาน' }}</div>
                </div>
                <div class="print-block">
                    <strong>รายละเอียดปัญหา</strong>
                    <p>{{ request.description }}</p>
                </div>
                <div class="print-block">
                    <strong>บันทึกเพิ่มเติม</strong>
                    <p>{{ request.note || '-' }}</p>
                </div>
                <div class="print-block">
                    <strong>ไทม์ไลน์ดำเนินการ</strong>
                    <div class="print-timeline">
                        <div v-for="(step, index) in timelineItems" :key="`print-${index}-${step.label}-${step.time}`"
                            class="print-timeline-item">
                            <div class="print-timeline-time">{{ step.time || '-' }}</div>
                            <div class="print-timeline-label">{{ step.label }}</div>
                        </div>
                    </div>
                </div>
            </div>

        </div>

        <template v-else-if="request && !isPrinting">
            <div class="bg-white border-b border-gray-100 px-6 py-4 shrink-0">
                <div class="flex items-center gap-3">
                    <Button icon="pi pi-arrow-left" text rounded severity="secondary" @click="goBack()" />
                    <div>
                        <div class="flex items-center gap-2 flex-wrap">
                            <h1 class="text-lg font-bold text-gray-900">{{ request.workOrderNo || 'ใบงานซ่อม' }}: {{
                                request.title }}</h1>
                            <Tag :severity="priorityTag(request.priority).severity"
                                :value="priorityTag(request.priority).label" />
                            <Tag :severity="statusTag(request.status).severity"
                                :value="statusTag(request.status).label" />
                            <Tag v-if="unreadCount > 0" severity="danger" :value="`มีข้อความใหม่ ${unreadCount}`" />
                        </div>
                        <div class="mt-2">
                            <Button label="พิมพ์ใบงาน" icon="pi pi-print" size="small" outlined
                                @click="printWorkOrder" />
                        </div>
                        <p class="text-xs text-gray-400 mt-1">
                            แจ้งโดย <strong>{{ formatPersonName(request.requesterName, request.requesterEmail)
                                }}</strong> · {{ formatDate(request.createdAt) }}
                            <span v-if="request.technicianName"> · ช่าง: <strong>{{
                                formatPersonName(request.technicianName, '') }}</strong></span>
                        </p>
                    </div>
                </div>
                <Message v-if="successMessage" severity="success" :closable="false" class="mt-3">{{ successMessage }}
                </Message>
                <Message v-if="errorMessage" severity="error" :closable="false" class="mt-3">{{ errorMessage }}
                </Message>
            </div>

            <div class="flex-1 overflow-hidden px-6 py-4 bg-gray-50/50">
                <div class="grid grid-cols-1 xl:grid-cols-5 gap-4 h-full">
                    <div class="xl:col-span-2 flex flex-col gap-4 overflow-y-auto pr-1">
                        <div class="bg-white border border-gray-200 rounded-xl p-4">
                            <h2 class="font-bold text-gray-900 mb-3">รายละเอียดงาน</h2>
                            <div class="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
                                <div>
                                    <p class="text-gray-500">หมวดงาน</p>
                                    <p class="font-semibold text-gray-900">{{ categoryLabel(request.category) }}</p>
                                </div>
                                <div>
                                    <p class="text-gray-500">เบอร์ติดต่อ</p>
                                    <p class="font-semibold text-gray-900">{{ request.extension || '-' }}</p>
                                </div>
                                <div>
                                    <p class="text-gray-500">อาคาร</p>
                                    <p class="font-semibold text-gray-900">{{ request.buildingName || '-' }}</p>
                                </div>
                                <div>
                                    <p class="text-gray-500">จุดที่พบปัญหา</p>
                                    <p class="font-semibold text-gray-900">{{ request.locationDetail || '-' }}</p>
                                </div>
                                <div class="md:col-span-2">
                                    <p class="text-gray-500">รายละเอียดปัญหา</p>
                                    <p class="text-gray-800 whitespace-pre-line">{{ request.description }}</p>
                                </div>
                                <div class="md:col-span-2">
                                    <p class="text-gray-500">หมายเหตุใบงาน</p>
                                    <p class="text-gray-800 whitespace-pre-line">{{ request.note || '-' }}</p>
                                </div>
                                <div class="md:col-span-2"
                                    v-if="request.technicianDiagnosis || request.technicianAction || request.escalationReason">
                                    <p class="text-gray-500">ข้อมูลจากช่าง</p>
                                    <div class="mt-1 space-y-2 rounded-lg border border-amber-100 bg-amber-50/60 p-3">
                                        <div v-if="request.technicianDiagnosis">
                                            <p class="text-xs font-semibold text-amber-700">ผลวิเคราะห์เบื้องต้น</p>
                                            <p class="text-sm text-gray-800 whitespace-pre-line">{{
                                                request.technicianDiagnosis }}</p>
                                        </div>
                                        <div v-if="request.escalationReason">
                                            <p class="text-xs font-semibold text-amber-700">เหตุผลที่ส่งหัวหน้าพิจารณา
                                            </p>
                                            <p class="text-sm text-gray-800 whitespace-pre-line">{{
                                                request.escalationReason }}</p>
                                        </div>
                                        <div v-if="request.technicianAction">
                                            <p class="text-xs font-semibold text-amber-700">
                                                วิธีแก้ไขที่ช่างเสนอ/ดำเนินการ</p>
                                            <p class="text-sm text-gray-800 whitespace-pre-line">{{
                                                request.technicianAction }}</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class="bg-white border border-gray-200 rounded-xl p-4">
                            <h2 class="font-bold text-gray-900 mb-3">ไทม์ไลน์ดำเนินการ</h2>
                            <div class="space-y-3">
                                <div v-for="(step, index) in timelineItems" :key="`${index}-${step.label}-${step.time}`"
                                    class="flex items-start gap-3">
                                    <div class="mt-0.5 w-3 h-3 rounded-full"
                                        :class="step.state === 'done' ? 'bg-emerald-500' : 'bg-blue-500'" />
                                    <div>
                                        <p class="text-sm font-semibold text-gray-800">{{ step.label }}</p>
                                        <p class="text-xs text-gray-400">{{ step.time }}</p>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div v-if="canTechnicianAction" class="bg-white border border-gray-200 rounded-xl p-4">
                            <h2 class="font-bold text-gray-900 mb-3">ปุ่มจัดการช่าง</h2>
                            <div class="flex flex-wrap gap-2">
                                <Button label="รับงาน" icon="pi pi-play" severity="info" size="small"
                                    :disabled="!canStartWork" :loading="actionLoading" @click="startWork" />
                                <Button label="ส่งหัวหน้าช่างพิจารณา" icon="pi pi-arrow-up-right" severity="warn"
                                    size="small" :disabled="!canEscalateToSupervisor" :loading="actionLoading"
                                    @click="escalateVisible = true" />
                                <Button label="ปิดงานซ่อมเสร็จ" icon="pi pi-check" severity="success" size="small"
                                    :disabled="!canCloseCompleted" :loading="actionLoading"
                                    @click="completeVisible = true" />
                            </div>
                        </div>

                        <div v-if="canRequesterActions" class="bg-white border border-gray-200 rounded-xl p-4">
                            <h2 class="font-bold text-gray-900 mb-3">ปุ่มสำหรับผู้แจ้งงาน</h2>
                            <div class="flex flex-wrap gap-2">
                                <Button label="แก้ไขใบงาน" icon="pi pi-pencil" severity="info" size="small"
                                    :disabled="!canRequesterEditWorkOrder" @click="openRequesterEdit" />
                                <Button label="ยกเลิกใบงาน" icon="pi pi-times" severity="danger" size="small"
                                    :disabled="!canRequesterEditWorkOrder" @click="requesterCancelVisible = true" />
                            </div>
                            <p v-if="showRequesterLockedEditNotice" class="text-xs text-amber-700 mt-3">
                                ช่างรับงานแล้ว จึงไม่สามารถแก้ไขรายละเอียดใบงานได้
                            </p>
                            <p v-if="showRequesterExternalHandledByAdminNotice" class="text-xs text-blue-700 mt-2">
                                งานช่างนอกจะอัปเดตโดยเจ้าหน้าที่ธุรการเท่านั้น ผู้แจ้งไม่สามารถอัปเดตสถานะช่างนอกได้
                            </p>
                            <p v-if="showCentralAssetExternalNotice" class="text-xs text-blue-700 mt-2">
                                งานนี้เป็นทรัพย์สินส่วนกลาง
                                ระบบส่งเรื่องไปให้เจ้าหน้าที่ธุรการที่หน้ารับเรื่องจ้างช่างนอกแล้ว
                            </p>
                            <p v-if="showRequesterCentralCloseNotice" class="text-xs text-gray-600 mt-2">
                                งานจ้างช่างภายนอกสำหรับทรัพย์สินส่วนกลาง จะปิดงานโดยเจ้าหน้าที่ธุรการอาคาร
                            </p>
                        </div>

                        <div v-if="canSupervisorAction && request.status === 'need_supervisor_review'"
                            class="bg-white border border-gray-200 rounded-xl p-4">
                            <Button label="หัวหน้าพิจารณา" icon="pi pi-search" severity="warn" size="small"
                                @click="reviewVisible = true" />
                        </div>
                        <div v-if="
                            (canCentralExternalAction && ['waiting_central_external_procurement', 'external_scheduled', 'external_in_progress'].includes(request.status)) ||
                            (canDepartmentExternalAction && !isCentralAssetValue && ['waiting_department_external_procurement', 'external_scheduled', 'external_in_progress', 'resolved'].includes(request.status))
                        " class="bg-white border border-gray-200 rounded-xl p-4">
                            <Button label="อัปเดตงานช่างนอก" icon="pi pi-briefcase" severity="secondary" size="small"
                                @click="() => { externalForm = { vendorName: request?.externalVendorName ?? '', scheduledAt: null, completedAt: null, result: request?.externalResult ?? '', closeAfterComplete: false }; externalVisible = true }" />
                        </div>
                    </div>

                    <div class="xl:col-span-3 bg-white border border-gray-200 rounded-xl flex flex-col min-h-0">
                        <div class="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
                            <h2 class="font-bold text-gray-900">แชทติดตามงาน</h2>
                            <span class="text-xs text-gray-400">อัปเดตแบบเรียลไทม์</span>
                        </div>

                        <div class="flex-1 overflow-y-auto px-4 py-3 space-y-3 bg-gray-50/50">
                            <div v-if="messages.length === 0"
                                class="flex flex-col items-center justify-center py-16 text-gray-400">
                                <i class="pi pi-comments text-4xl mb-3"></i>
                                <p class="text-sm">ยังไม่มีข้อความ — เริ่มต้นสนทนาเพื่อติดตามงานนี้</p>
                            </div>

                            <div v-for="msg in messages" :key="msg.id" :class="[
                                'flex gap-2',
                                msg.senderRole === 'system' ? 'justify-center' : isMine(msg) ? 'flex-row-reverse' : 'flex-row',
                            ]">
                                <div v-if="msg.senderRole === 'system'" class="w-full text-center">
                                    <span class="text-xs text-gray-400 bg-gray-200/70 rounded-full px-3 py-1">{{
                                        msg.text }}</span>
                                </div>

                                <template v-else>
                                    <div class="w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold shrink-0 mt-1"
                                        :class="isMine(msg) ? 'bg-indigo-600 text-white' : 'bg-slate-200 text-slate-600'">
                                        {{ formatPersonName(msg.senderName, msg.senderEmail).charAt(0).toUpperCase() }}
                                    </div>
                                    <div
                                        :class="['max-w-sm lg:max-w-md', isMine(msg) ? 'items-end' : 'items-start', 'flex flex-col']">
                                        <div class="flex items-baseline gap-2 mb-1"
                                            :class="isMine(msg) ? 'flex-row-reverse' : ''">
                                            <span class="text-xs font-semibold text-gray-700">{{
                                                formatPersonName(msg.senderName, msg.senderEmail) }}</span>
                                            <span class="text-xs text-gray-400">{{ formatTime(msg.createdAt) }}</span>
                                        </div>
                                        <div class="rounded-2xl px-4 py-2.5 text-sm leading-relaxed whitespace-pre-wrap"
                                            :class="isMine(msg)
                                                ? 'bg-indigo-600 text-white rounded-tr-sm'
                                                : 'bg-white border border-gray-200 text-gray-800 rounded-tl-sm shadow-sm'">
                                            {{ msg.text }}
                                        </div>
                                        <div v-if="isMine(msg)" class="text-[11px] text-gray-400 mt-1 px-1">
                                            {{ msg.isRead ? `อ่านแล้ว ${formatTime(msg.readAt)}` : 'ส่งแล้ว' }}
                                        </div>
                                    </div>
                                </template>
                            </div>

                            <div ref="chatBottom" class="h-1"></div>
                        </div>

                        <div class="border-t border-gray-100 px-4 py-3">
                            <div v-if="!canChat" class="text-center text-sm text-gray-400 py-2">
                                <i class="pi pi-lock mr-1"></i>
                                เฉพาะผู้แจ้ง ผู้รับผิดชอบ และผู้ดูแลระบบเท่านั้นที่สามารถส่งข้อความได้
                            </div>
                            <div v-else class="flex gap-3 items-end">
                                <Textarea v-model="messageText" :rows="2"
                                    placeholder="พิมพ์ข้อความ... (Enter ส่ง, Shift+Enter ขึ้นบรรทัดใหม่)"
                                    class="flex-1 resize-none" @keydown="handleEnter" />
                                <Button icon="pi pi-send" :loading="sending" :disabled="!messageText.trim()"
                                    @click="sendMessage" class="mb-0.5" />
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <Dialog v-model:visible="completeVisible" header="บันทึกผลการซ่อม" modal :style="{ width: '36rem' }">
                <div class="space-y-3">
                    <div>
                        <label class="text-sm text-gray-700">ผลวิเคราะห์/สาเหตุ</label>
                        <Textarea v-model="completeForm.diagnosis" rows="3" class="w-full" />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">วิธีการแก้ไข</label>
                        <Textarea v-model="completeForm.action" rows="3" class="w-full" />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">หมายเหตุ</label>
                        <Textarea v-model="completeForm.note" rows="2" class="w-full" />
                    </div>
                </div>
                <template #footer>
                    <Button label="ยกเลิก" text @click="completeVisible = false" />
                    <Button label="บันทึก" icon="pi pi-check" :loading="actionLoading" @click="submitComplete" />
                </template>
            </Dialog>

            <Dialog v-model:visible="escalateVisible" header="ส่งหัวหน้าตรวจสอบ" modal :style="{ width: '36rem' }">
                <div class="space-y-3">
                    <div>
                        <label class="text-sm text-gray-700">ผลวิเคราะห์เบื้องต้น</label>
                        <Textarea v-model="escalateForm.diagnosis" rows="3" class="w-full" />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">เหตุผลที่ต้องให้หัวหน้าพิจารณา</label>
                        <Textarea v-model="escalateForm.escalationReason" rows="4" class="w-full" />
                    </div>
                </div>
                <template #footer>
                    <Button label="ยกเลิก" text @click="escalateVisible = false" />
                    <Button label="ส่งหัวหน้า" icon="pi pi-send" severity="warn" :loading="actionLoading"
                        @click="submitEscalate" />
                </template>
            </Dialog>

            <Dialog v-model:visible="reviewVisible" header="ผลพิจารณาหัวหน้าช่าง" modal :style="{ width: '38rem' }">
                <div class="space-y-3">
                    <div>
                        <label class="text-sm text-gray-700">ผลการประเมิน</label>
                        <Select v-model="reviewForm.canRepairInHouse" :options="[
                            { label: 'ซ่อมภายในได้', value: true },
                            { label: 'ซ่อมภายในไม่ได้ ต้องจ้างภายนอก', value: false }
                        ]" optionLabel="label" optionValue="value" class="w-full" />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">เหตุผล</label>
                        <Textarea v-model="reviewForm.reason" rows="3" class="w-full" />
                    </div>
                    <div v-if="reviewForm.canRepairInHouse">
                        <label class="text-sm text-gray-700">แผนที่ส่งกลับให้ช่าง</label>
                        <Textarea v-model="reviewForm.repairPlan" rows="3" class="w-full" />
                    </div>
                    <div v-else>
                        <label class="text-sm text-gray-700">คำแนะนำสำหรับงานจ้างภายนอก</label>
                        <Textarea v-model="reviewForm.externalAdvice" rows="3" class="w-full" />
                    </div>
                </div>
                <template #footer>
                    <Button label="ยกเลิก" text @click="reviewVisible = false" />
                    <Button label="บันทึก" icon="pi pi-check" severity="warn" :loading="actionLoading"
                        @click="submitReview" />
                </template>
            </Dialog>

            <Dialog v-model:visible="externalVisible" header="อัปเดตงานช่างนอก" modal :style="{ width: '38rem' }">
                <div class="space-y-3">
                    <div>
                        <label class="text-sm text-gray-700">บริษัท/ช่างภายนอก</label>
                        <InputText v-model="externalForm.vendorName" class="w-full" />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">วันนัดหมาย</label>
                        <DatePicker v-model="externalForm.scheduledAt" dateFormat="dd/mm/yy" showTime hourFormat="24"
                            class="w-full" showIcon />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">วันซ่อมเสร็จ</label>
                        <DatePicker v-model="externalForm.completedAt" dateFormat="dd/mm/yy" showTime hourFormat="24"
                            class="w-full" showIcon />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">ผลการซ่อม</label>
                        <Textarea v-model="externalForm.result" rows="3" class="w-full" />
                    </div>
                    <div class="flex items-center gap-2">
                        <input id="closeAfterComplete" v-model="externalForm.closeAfterComplete" type="checkbox" />
                        <label for="closeAfterComplete" class="text-sm text-gray-700">ปิดงานทันทีเมื่อซ่อมเสร็จ</label>
                    </div>
                </div>
                <template #footer>
                    <Button label="ยกเลิก" text @click="externalVisible = false" />
                    <Button label="บันทึก" icon="pi pi-check" :loading="actionLoading"
                        @click="submitExternalProgress" />
                </template>
            </Dialog>

            <Dialog v-model:visible="requesterEditVisible" header="แก้ไขใบงาน (ผู้แจ้ง)" modal
                :style="{ width: '38rem' }">
                <div class="space-y-3">
                    <div>
                        <label class="text-sm text-gray-700">หัวข้อ</label>
                        <InputText v-model="requesterEditForm.title" class="w-full" />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">รายละเอียด</label>
                        <Textarea v-model="requesterEditForm.description" rows="3" class="w-full" />
                    </div>
                    <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                        <div>
                            <label class="text-sm text-gray-700">อาคาร</label>
                            <InputText v-model="requesterEditForm.buildingName" class="w-full" />
                        </div>
                        <div>
                            <label class="text-sm text-gray-700">จุดที่พบปัญหา</label>
                            <InputText v-model="requesterEditForm.locationDetail" class="w-full" />
                        </div>
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">เบอร์ติดต่อ</label>
                        <InputText v-model="requesterEditForm.extension" class="w-full" />
                    </div>
                </div>
                <template #footer>
                    <Button label="ยกเลิก" text @click="requesterEditVisible = false" />
                    <Button label="บันทึกการแก้ไข" icon="pi pi-check" :loading="actionLoading"
                        @click="submitRequesterEdit" />
                </template>
            </Dialog>

            <Dialog v-model:visible="requesterCancelVisible" header="ยืนยันยกเลิกใบงาน" modal
                :style="{ width: '34rem' }">
                <div class="space-y-3">
                    <p class="text-sm text-gray-600">ระบุเหตุผลที่ยกเลิกใบงาน</p>
                    <Textarea v-model="requesterCancelReason" rows="3" class="w-full"
                        placeholder="เหตุผลการยกเลิก..." />
                </div>
                <template #footer>
                    <Button label="ย้อนกลับ" text @click="requesterCancelVisible = false" />
                    <Button label="ยืนยันยกเลิก" icon="pi pi-times" severity="danger" :loading="actionLoading"
                        @click="cancelByRequester" />
                </template>
            </Dialog>

        </template>

        <template v-else>
            <div class="h-full flex items-center justify-center px-6">
                <div class="max-w-lg w-full bg-white border border-red-200 rounded-2xl p-6 text-center">
                    <i class="pi pi-exclamation-triangle text-red-500 text-3xl mb-3"></i>
                    <h2 class="text-lg font-bold text-gray-900">ไม่สามารถแสดงรายละเอียดใบงานได้</h2>
                    <p class="text-sm text-gray-600 mt-2">{{ errorMessage || 'กรุณาลองใหม่อีกครั้ง' }}</p>
                    <div class="mt-4">
                        <Button label="กลับไปหน้ารับงานช่าง" icon="pi pi-arrow-left" @click="goBack" />
                    </div>
                </div>
            </div>
        </template>
    </div>
</template>

<style scoped>
.print-sheet {
    padding: 16px;
    color: #111827;
    background: #ffffff;
    font-size: 13px;
    line-height: 1.45;
}

.print-header h1 {
    margin: 0;
    font-size: 22px;
    font-weight: 700;
}

.print-header p {
    margin: 2px 0;
}

.print-section {
    margin-top: 16px;
    border: 1px solid #d1d5db;
    border-radius: 8px;
    padding: 12px;
}

.print-section h2 {
    margin: 0 0 8px;
    font-size: 15px;
    font-weight: 700;
}

.print-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 6px 12px;
}

.print-block {
    margin-top: 10px;
}

.print-block p {
    white-space: pre-wrap;
    margin: 4px 0 0;
}

.print-timeline {
    display: flex;
    flex-direction: column;
    gap: 6px;
    margin-top: 6px;
}

.print-timeline-item {
    display: grid;
    grid-template-columns: 11rem 1fr;
    gap: 12px;
    align-items: start;
}

.print-timeline-time {
    color: #4b5563;
}

.print-timeline-label {
    color: #111827;
}

@media print {
    :global(body *) {
        visibility: hidden !important;
    }

    .print-sheet,
    .print-sheet * {
        visibility: visible !important;
    }

    .print-sheet {
        position: fixed;
        inset: 0;
        width: 100%;
        height: 100%;
        overflow: auto;
        padding: 0;
        background: #fff;
    }
}
</style>
