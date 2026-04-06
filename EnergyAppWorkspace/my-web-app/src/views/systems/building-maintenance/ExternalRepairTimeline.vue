<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import {
    MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
    hasMaintenancePermission,
} from '@/config/maintenancePermissions'

import Card from 'primevue/card'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'

interface RepairRequest {
    id: string
    workOrderNo: string
    title: string
    status: string
    buildingName: string
    locationDetail: string
    assetNumber: string
    isCentralAsset: boolean
    requesterName: string
    supervisorReason: string
    supervisorExternalAdvice: string
    externalVendorName: string
    externalScheduledAt: string | null
    externalCompletedAt: string | null
    externalResult: string
    updatedAt: string | null
}

const authStore = useAuthStore()
const role = computed(() => (authStore.user?.role ?? '').toLowerCase())
const maintenanceAdminSystems = computed(() => authStore.userProfile?.adminSystems ?? [])
const canManageExternal = computed(() =>
    ['superadmin', 'adminbuilding'].includes(role.value) ||
    hasMaintenancePermission(maintenanceAdminSystems.value, MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION),
)

const loading = ref(false)
const requests = ref<RepairRequest[]>([])
const successMessage = ref('')
const errorMessage = ref('')

const detailVisible = ref(false)
const selectedRequest = ref<RepairRequest | null>(null)
const form = ref({
    vendorName: '',
    result: '',
    mode: 'external_scheduled',
})

const statusOptions = [
    { label: 'นัดช่างภายนอกแล้ว', value: 'external_scheduled' },
    { label: 'ช่างภายนอกกำลังซ่อม', value: 'external_in_progress' },
    { label: 'ซ่อมเสร็จ (ยังไม่ปิด)', value: 'resolved' },
    { label: 'ปิดงาน', value: 'closed' },
]

const externalQueue = computed(() =>
    requests.value.filter((x) =>
        x.isCentralAsset &&
        [
            'waiting_central_external_procurement',
            'external_scheduled',
            'external_in_progress',
            'resolved',
            'closed',
        ].includes(x.status),
    ),
)

const statusTag = (value: string): { severity: 'secondary' | 'info' | 'success' | 'danger'; label: string } => {
    if (value === 'waiting_central_external_procurement') return { severity: 'danger', label: 'รอธุรการส่วนกลาง จัดจ้างช่างภายนอก' }
    if (value === 'external_scheduled') return { severity: 'info', label: 'นัดช่างภายนอกแล้ว' }
    if (value === 'external_in_progress') return { severity: 'info', label: 'ช่างภายนอกกำลังซ่อม' }
    if (value === 'resolved') return { severity: 'success', label: 'ซ่อมเสร็จ' }
    if (value === 'closed') return { severity: 'success', label: 'ปิดงาน' }
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
    try {
        const res = await api.get('/ServiceRequest', { params: { take: 500 } })
        requests.value = res.data.items ?? []
    } catch {
        errorMessage.value = 'โหลดรายการงานช่างภายนอกไม่สำเร็จ'
    } finally {
        loading.value = false
    }
}

onMounted(async () => {
    if (canManageExternal.value) {
        await loadQueue()
    }
})

const openDetail = (item: RepairRequest) => {
    selectedRequest.value = item
    form.value = {
        vendorName: item.externalVendorName ?? '',
        result: item.externalResult ?? '',
        mode: ['external_scheduled', 'external_in_progress', 'resolved', 'closed'].includes(item.status)
            ? item.status
            : 'external_scheduled',
    }
    detailVisible.value = true
}

const saveProgress = async () => {
    if (!selectedRequest.value) return
    try {
        const mode = form.value.mode
        await api.put(`/ServiceRequest/${selectedRequest.value.id}/external-progress`, {
            vendorName: form.value.vendorName,
            scheduledAt: mode === 'external_scheduled' ? new Date().toISOString() : null,
            completedAt: mode === 'resolved' || mode === 'closed' ? new Date().toISOString() : null,
            result: form.value.result,
            closeAfterComplete: mode === 'closed',
        })

        successMessage.value = 'อัปเดต Timeline งานช่างภายนอกสำเร็จ'
        detailVisible.value = false
        await loadQueue()
    } catch {
        errorMessage.value = 'อัปเดต Timeline ไม่สำเร็จ'
    }
}
</script>

<template>
    <div class="space-y-6">
        <Message v-if="!canManageExternal" severity="warn" :closable="false">
            เฉพาะธุรการงานอาคารและผู้ดูแลระบบเท่านั้นที่จัดการ Timeline งานช่างภายนอกได้
        </Message>

        <template v-if="canManageExternal">
            <div class="flex items-center justify-between">
                <div>
                    <h1 class="text-2xl font-bold text-gray-900">Timeline งานซ่อมภายนอก</h1>
                    <p class="text-sm text-gray-400 mt-1">ติดตามการจ้างบริษัทภายนอกสำหรับสินทรัพย์ส่วนกลาง</p>
                </div>
                <Button label="รีเฟรช" icon="pi pi-refresh" @click="loadQueue" />
            </div>

            <Message v-if="successMessage" severity="success" :closable="false">{{ successMessage }}</Message>
            <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>

            <Card>
                <template #content>
                    <DataTable :value="externalQueue" :loading="loading" size="small" striped-rows>
                        <Column field="workOrderNo" header="เลขใบงาน" style="width: 10rem" />
                        <Column field="title" header="งานซ่อม" />
                        <Column field="assetNumber" header="เลขสินทรัพย์" style="width: 9rem" />
                        <Column field="buildingName" header="อาคาร/จุด" style="width: 13rem">
                            <template #body="{ data }">
                                <div class="text-xs text-gray-700">{{ data.buildingName || '-' }}</div>
                                <div class="text-xs text-gray-400 truncate max-w-52">{{ data.locationDetail || '-' }}
                                </div>
                            </template>
                        </Column>
                        <Column field="status" header="สถานะ" style="width: 13rem">
                            <template #body="{ data }">
                                <Tag :severity="statusTag(data.status).severity"
                                    :value="statusTag(data.status).label" />
                            </template>
                        </Column>
                        <Column field="externalScheduledAt" header="นัดซ่อม" style="width: 11rem">
                            <template #body="{ data }">{{ formatDate(data.externalScheduledAt) }}</template>
                        </Column>
                        <Column field="externalCompletedAt" header="ซ่อมเสร็จ" style="width: 11rem">
                            <template #body="{ data }">{{ formatDate(data.externalCompletedAt) }}</template>
                        </Column>
                        <Column header="จัดการ" style="width: 7rem">
                            <template #body="{ data }">
                                <Button label="อัปเดต" size="small" @click="openDetail(data)" />
                            </template>
                        </Column>
                    </DataTable>
                </template>
            </Card>

            <Dialog v-model:visible="detailVisible" header="อัปเดต Timeline ช่างภายนอก" modal
                :style="{ width: '36rem' }">
                <div class="space-y-3">
                    <div>
                        <label class="text-sm text-gray-600">เลขใบงาน</label>
                        <InputText :model-value="selectedRequest?.workOrderNo" disabled class="w-full" />
                    </div>
                    <div>
                        <label class="text-sm text-gray-600">สถานะถัดไป</label>
                        <select v-model="form.mode" class="w-full border rounded-lg px-3 py-2">
                            <option v-for="s in statusOptions" :key="s.value" :value="s.value">{{ s.label }}</option>
                        </select>
                    </div>
                    <div>
                        <label class="text-sm text-gray-600">ชื่อบริษัทภายนอก</label>
                        <InputText v-model="form.vendorName" class="w-full" />
                    </div>
                    <div>
                        <label class="text-sm text-gray-600">ผลการซ่อม/หมายเหตุ</label>
                        <Textarea v-model="form.result" rows="4" class="w-full" />
                    </div>
                </div>
                <template #footer>
                    <Button label="ยกเลิก" text @click="detailVisible = false" />
                    <Button label="บันทึก Timeline" @click="saveProgress" />
                </template>
            </Dialog>
        </template>
    </div>
</template>
