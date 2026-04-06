<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import {
    MAINTENANCE_ADMIN_BUILDING_PERMISSION,
    MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
    hasMaintenancePermission,
} from '@/config/maintenancePermissions'

import Card from 'primevue/card'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Textarea from 'primevue/textarea'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import Message from 'primevue/message'

interface ServiceRequest {
    id: string
    workOrderNo?: string
    title: string
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
    requesterDepartmentCode?: string
}

interface DepartmentItem {
    id: string
    code?: string
}

const authStore = useAuthStore()
const role = computed(() => (authStore.user?.role ?? '').toLowerCase())
const maintenanceAdminSystems = computed(() => authStore.userProfile?.adminSystems ?? [])
const departmentId = computed(() => (authStore.userProfile?.departmentId ?? authStore.user?.departmentId ?? '').toString())
const departmentCode = ref('')
const hasDepartmentExternalPermission = computed(() =>
    hasMaintenancePermission(maintenanceAdminSystems.value, MAINTENANCE_ADMIN_BUILDING_PERMISSION),
)
const hasCentralExternalPermission = computed(() =>
    hasMaintenancePermission(maintenanceAdminSystems.value, MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION),
)
const canAdminExternal = computed(() =>
    ['superadmin', 'adminbuilding', 'admin'].includes(role.value) ||
    hasDepartmentExternalPermission.value ||
    hasCentralExternalPermission.value,
)

const loading = ref(false)
const saving = ref(false)
const requests = ref<ServiceRequest[]>([])
const successMessage = ref('')
const errorMessage = ref('')

const detailVisible = ref(false)
const selectedRequest = ref<ServiceRequest | null>(null)
const form = ref({
    vendorName: '',
    scheduledAt: null as Date | null,
    completedAt: null as Date | null,
    result: '',
    closeAfterComplete: false,
})

const normalizedDepartmentKeys = computed(() => {
    const keys = [departmentId.value, departmentCode.value]
        .map((x) => (x || '').trim().toLowerCase())
        .filter((x) => x.length > 0)
    return Array.from(new Set(keys))
})

const queueItems = computed(() =>
    requests.value.filter((item) => {
        const canSeeDepartmentQueue =
            role.value === 'admin' || role.value === 'superadmin' || hasDepartmentExternalPermission.value
        const canSeeCentralQueue =
            role.value === 'adminbuilding' || role.value === 'superadmin' || hasCentralExternalPermission.value

        const requesterDepartment = (item.requesterDepartmentCode || '').trim().toLowerCase()
        const isOwnDepartment =
            role.value === 'superadmin' ||
            (requesterDepartment.length > 0 && normalizedDepartmentKeys.value.includes(requesterDepartment))

        const centralQueueStatuses = ['waiting_central_external_procurement', 'external_scheduled', 'external_in_progress', 'resolved']
        const departmentQueueStatuses = [
            'waiting_department_external_procurement',
            'external_scheduled',
            'external_in_progress',
            'resolved',
        ]

        const inCentralQueue = canSeeCentralQueue && !!item.isCentralAsset && centralQueueStatuses.includes(item.status)
        const inDepartmentQueue =
            canSeeDepartmentQueue &&
            !item.isCentralAsset &&
            isOwnDepartment &&
            departmentQueueStatuses.includes(item.status)

        return inCentralQueue || inDepartmentQueue
    }),
)

const statusTag = (value: string): { severity: 'secondary' | 'info' | 'success' | 'danger'; label: string } => {
    if (value === 'waiting_department_external_procurement') return { severity: 'danger', label: 'รอหน่วยงาน/กองจัดจ้างช่างภายนอก' }
    if (value === 'waiting_central_external_procurement') return { severity: 'danger', label: 'รอธุรการส่วนกลางจัดจ้างช่างภายนอก' }
    if (value === 'external_scheduled') return { severity: 'info', label: 'นัดช่างภายนอกแล้ว' }
    if (value === 'external_in_progress') return { severity: 'info', label: 'ช่างภายนอกกำลังซ่อม' }
    if (value === 'resolved') return { severity: 'success', label: 'ซ่อมเสร็จ (รอปิดงาน)' }
    if (value === 'closed') return { severity: 'success', label: 'ปิดงานแล้ว' }
    return { severity: 'secondary', label: value }
}

const formatDate = (value: string | null | undefined): string => {
    if (!value) return '-'
    return new Date(value).toLocaleString('th-TH', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
    })
}

const loadQueue = async () => {
    loading.value = true
    errorMessage.value = ''
    try {
        const res = await api.get('/ServiceRequest', { params: { take: 500 } })
        requests.value = (res.data.items ?? []) as ServiceRequest[]
    } catch {
        errorMessage.value = 'ไม่สามารถโหลดงานจ้างภายนอกได้'
    } finally {
        loading.value = false
    }
}

const loadDepartmentCode = async () => {
    if (!departmentId.value) return
    try {
        const res = await api.get('/Department/all')
        const departments = (res.data ?? []) as DepartmentItem[]
        const current = departments.find((dep) => dep.id === departmentId.value)
        departmentCode.value = (current?.code ?? '').toString()
    } catch {
        departmentCode.value = ''
    }
}

onMounted(async () => {
    if (canAdminExternal.value) {
        await loadDepartmentCode()
        await loadQueue()
    }
})

const openDialog = (item: ServiceRequest) => {
    selectedRequest.value = item
    form.value = {
        vendorName: item.externalVendorName || '',
        scheduledAt: item.externalScheduledAt ? new Date(item.externalScheduledAt) : null,
        completedAt: item.externalCompletedAt ? new Date(item.externalCompletedAt) : null,
        result: item.externalResult || '',
        closeAfterComplete: false,
    }
    detailVisible.value = true
}

const saveProgress = async () => {
    if (!selectedRequest.value) return
    if (!form.value.vendorName.trim()) {
        errorMessage.value = 'กรุณาระบุชื่อช่างหรือบริษัทภายนอก'
        return
    }

    saving.value = true
    try {
        await api.put(`/ServiceRequest/${selectedRequest.value.id}/external-progress`, {
            vendorName: form.value.vendorName,
            scheduledAt: form.value.scheduledAt ? form.value.scheduledAt.toISOString() : null,
            completedAt: form.value.completedAt ? form.value.completedAt.toISOString() : null,
            result: form.value.result,
            closeAfterComplete: form.value.closeAfterComplete,
        })

        detailVisible.value = false
        successMessage.value = 'อัปเดตงานจ้างช่างภายนอกเรียบร้อยแล้ว'
        await loadQueue()
    } catch {
        errorMessage.value = 'ไม่สามารถอัปเดตงานจ้างภายนอกได้'
    } finally {
        saving.value = false
    }
}
</script>

<template>
    <div class="space-y-6">
        <Message v-if="!canAdminExternal" severity="warn" :closable="false">
            หน้านี้สำหรับเจ้าหน้าที่รับเรื่องจ้างช่างนอก และผู้ดูแลระบบ
        </Message>

        <template v-else>
            <div class="flex items-center justify-between gap-3">
                <div>
                    <h1 class="text-2xl font-bold text-gray-900">หน้ารับเรื่องจ้างช่างนอก</h1>
                    <p class="text-sm text-gray-500 mt-1">รับงานจากระบบตามสิทธิ์บทบาท (กอง/ส่วนกลาง)
                        เพื่อจัดจ้างช่างภายนอก</p>
                </div>
                <Button label="รีเฟรช" icon="pi pi-refresh" outlined :loading="loading" @click="loadQueue" />
            </div>

            <Message v-if="successMessage" severity="success" :closable="false">{{ successMessage }}</Message>
            <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>

            <Card>
                <template #content>
                    <DataTable :value="queueItems" :loading="loading" striped-rows size="small"
                        empty-message="ไม่มีงานรอจ้างช่างนอก">
                        <Column field="workOrderNo" header="เลขใบงาน" style="width: 10rem" />
                        <Column field="title" header="รายการงาน" />
                        <Column field="buildingName" header="อาคาร/จุด" style="width: 14rem">
                            <template #body="{ data }">
                                <div>{{ data.buildingName || '-' }}</div>
                                <div class="text-xs text-gray-400">{{ data.locationDetail || '-' }}</div>
                            </template>
                        </Column>
                        <Column field="supervisorReason" header="เหตุผลจากหัวหน้า" style="width: 16rem">
                            <template #body="{ data }">{{ data.supervisorReason || data.supervisorExternalAdvice || '-'
                                }}</template>
                        </Column>
                        <Column field="status" header="สถานะ" style="width: 12rem">
                            <template #body="{ data }">
                                <Tag :severity="statusTag(data.status).severity"
                                    :value="statusTag(data.status).label" />
                            </template>
                        </Column>
                        <Column header="วันที่รับเรื่อง" style="width: 11rem">
                            <template #body="{ data }">{{ formatDate(data.updatedAt ?? data.createdAt) }}</template>
                        </Column>
                        <Column header="จัดการ" style="width: 7rem">
                            <template #body="{ data }">
                                <Button label="อัปเดต" icon="pi pi-pencil" size="small" @click="openDialog(data)" />
                            </template>
                        </Column>
                    </DataTable>
                </template>
            </Card>

            <Dialog v-model:visible="detailVisible" header="อัปเดตงานช่างนอก" modal :style="{ width: '38rem' }">
                <div class="space-y-3">
                    <div>
                        <label class="text-sm text-gray-700">ชื่อบริษัท/ช่างภายนอก</label>
                        <InputText v-model="form.vendorName" class="w-full" />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">วัน-เวลา นัดหมาย</label>
                        <DatePicker v-model="form.scheduledAt" showTime hourFormat="24" dateFormat="dd/mm/yy"
                            class="w-full" showIcon />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">วัน-เวลา ซ่อมเสร็จ (ถ้ามี)</label>
                        <DatePicker v-model="form.completedAt" showTime hourFormat="24" dateFormat="dd/mm/yy"
                            class="w-full" showIcon />
                    </div>
                    <div>
                        <label class="text-sm text-gray-700">ผลการซ่อม</label>
                        <Textarea v-model="form.result" rows="3" class="w-full" />
                    </div>
                    <div class="flex items-center gap-2">
                        <input id="closeAfterComplete" v-model="form.closeAfterComplete" type="checkbox" />
                        <label for="closeAfterComplete"
                            class="text-sm text-gray-700">ปิดงานทันทีเมื่อระบุวันซ่อมเสร็จ</label>
                    </div>
                </div>
                <template #footer>
                    <Button label="ยกเลิก" text @click="detailVisible = false" />
                    <Button label="บันทึก" icon="pi pi-check" :loading="saving" @click="saveProgress" />
                </template>
            </Dialog>
        </template>
    </div>
</template>
