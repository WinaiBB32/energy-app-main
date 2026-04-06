<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { MAINTENANCE_TECHNICIAN_PERMISSION, hasMaintenancePermission } from '@/config/maintenancePermissions'

import Card from 'primevue/card'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Button from 'primevue/button'
import Message from 'primevue/message'

interface ServiceRequest {
    id: string
    workOrderNo?: string
    title: string
    description: string
    status: string
    priority: string
    buildingName?: string
    locationDetail?: string
    technicianUid?: string
    technicianName?: string
    requesterName: string
    createdAt?: string | null
    updatedAt?: string | null
}

const authStore = useAuthStore()
const router = useRouter()
const role = computed(() => (authStore.user?.role ?? '').toLowerCase())
const uid = computed(() => authStore.user?.uid ?? authStore.user?.id ?? '')
const maintenanceAdminSystems = computed(() => authStore.userProfile?.adminSystems ?? [])
const canTechnician = computed(() =>
    ['superadmin', 'technician'].includes(role.value) ||
    hasMaintenancePermission(maintenanceAdminSystems.value, MAINTENANCE_TECHNICIAN_PERMISSION),
)

const loading = ref(false)
const requests = ref<ServiceRequest[]>([])
const errorMessage = ref('')

const queueItems = computed(() => {
    if (!canTechnician.value) return []
    return requests.value.filter((item) => {
        const inQueueStatus = ['new', 'assigned', 'returned_to_technician', 'in_progress'].includes(item.status)
        if (!inQueueStatus) return false

        if (role.value === 'superadmin') return true
        if (!item.technicianUid) return true
        return item.technicianUid === uid.value
    })
})

const statusTag = (status: string): { severity: 'secondary' | 'info' | 'success' | 'danger'; label: string } => {
    if (status === 'new') return { severity: 'secondary', label: 'งานใหม่' }
    if (status === 'assigned') return { severity: 'info', label: 'มอบหมายแล้ว' }
    if (status === 'in_progress') return { severity: 'info', label: 'กำลังดำเนินการ' }
    if (status === 'returned_to_technician') return { severity: 'danger', label: 'หัวหน้าส่งกลับให้ช่าง' }
    return { severity: 'secondary', label: status }
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

const formatDisplayName = (value: string | null | undefined): string => {
    const raw = (value || '').trim()
    if (!raw) return '-'
    if (!raw.includes('@')) return raw

    const localPart = raw.split('@')[0] || raw
    return localPart.replace(/[._-]+/g, ' ')
}

const loadQueue = async () => {
    loading.value = true
    errorMessage.value = ''
    try {
        const res = await api.get('/ServiceRequest', { params: { take: 500 } })
        requests.value = (res.data.items ?? []) as ServiceRequest[]
    } catch {
        errorMessage.value = 'ไม่สามารถโหลดคิวงานช่างได้'
    } finally {
        loading.value = false
    }
}

onMounted(async () => {
    if (canTechnician.value) {
        await loadQueue()
    }
})

const openDetail = (item: ServiceRequest) => {
    router.push(`/maintenance/service/${item.id}`)
}
</script>

<template>
    <div class="space-y-6">
        <Message v-if="!canTechnician" severity="warn" :closable="false">
            หน้านี้สำหรับบทบาทช่าง และผู้ดูแลระบบ
        </Message>

        <template v-else>
            <div class="flex items-center justify-between gap-3">
                <div>
                    <h1 class="text-2xl font-bold text-gray-900">หน้ารับงานช่าง</h1>
                    <p class="text-sm text-gray-500 mt-1">รายการงานสำหรับช่าง -
                        ปุ่มรับงานและดำเนินการอยู่ในหน้ารายละเอียดงาน</p>
                </div>
                <Button label="รีเฟรช" icon="pi pi-refresh" outlined :loading="loading" @click="loadQueue" />
            </div>

            <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>

            <Card>
                <template #content>
                    <DataTable :value="queueItems" :loading="loading" size="small" striped-rows
                        empty-message="ไม่มีงานสำหรับช่าง">
                        <Column field="workOrderNo" header="เลขใบงาน" style="width: 10rem" />
                        <Column field="title" header="รายการงาน">
                            <template #body="{ data }">
                                <div class="font-medium text-gray-800">{{ data.title }}</div>
                                <div class="text-xs text-gray-500 line-clamp-2">{{ data.description }}</div>
                            </template>
                        </Column>
                        <Column field="buildingName" header="อาคาร/จุด" style="width: 14rem">
                            <template #body="{ data }">
                                <div>{{ data.buildingName || '-' }}</div>
                                <div class="text-xs text-gray-400">{{ data.locationDetail || '-' }}</div>
                            </template>
                        </Column>
                        <Column field="status" header="สถานะ" style="width: 12rem">
                            <template #body="{ data }">
                                <Tag :severity="statusTag(data.status).severity"
                                    :value="statusTag(data.status).label" />
                            </template>
                        </Column>
                        <Column field="requesterName" header="ผู้แจ้ง" style="width: 10rem">
                            <template #body="{ data }">{{ formatDisplayName(data.requesterName) }}</template>
                        </Column>
                        <Column field="updatedAt" header="อัปเดตล่าสุด" style="width: 11rem">
                            <template #body="{ data }">{{ formatDate(data.updatedAt || data.createdAt) }}</template>
                        </Column>
                        <Column header="การดำเนินการ" style="width: 9rem">
                            <template #body="{ data }">
                                <Button label="รายละเอียด" icon="pi pi-file" size="small" outlined
                                    @click="openDetail(data)" />
                            </template>
                        </Column>
                    </DataTable>
                </template>
            </Card>
        </template>
    </div>
</template>
