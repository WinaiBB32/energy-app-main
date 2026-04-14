<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { usePermissions } from '@/composables/usePermissions'
import { MAINTENANCE_SUPERVISOR_PERMISSION, hasMaintenancePermission } from '@/config/maintenancePermissions'

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
    buildingName?: string
    locationDetail?: string
    requesterName: string
    technicianName?: string
    technicianDiagnosis?: string
    escalationReason?: string
    createdAt?: string | null
    updatedAt?: string | null
}

const authStore = useAuthStore()
const router = useRouter()
const { isSystemAdmin } = usePermissions()
const role = computed(() => (authStore.user?.role ?? '').trim().toLowerCase())
const maintenanceAdminSystems = computed(() => authStore.userProfile?.adminSystems ?? [])
const canSupervisor = computed(
    () =>
        isSystemAdmin('maintenance') ||
        ['superadmin', 'supervisor', 'admin'].includes(role.value) ||
        hasMaintenancePermission(maintenanceAdminSystems.value, MAINTENANCE_SUPERVISOR_PERMISSION),
)

const loading = ref(false)
const requests = ref<ServiceRequest[]>([])
const successMessage = ref('')
const errorMessage = ref('')

const queueItems = computed(() =>
    requests.value.filter((item) => item.status === 'need_supervisor_review'),
)

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
        const res = await api.get('/ServiceRequest', { params: { take: 500, status: 'need_supervisor_review' } })
        requests.value = (res.data.items ?? []) as ServiceRequest[]
    } catch {
        errorMessage.value = 'ไม่สามารถโหลดคิวรอตรวจสอบจากหัวหน้าได้'
    } finally {
        loading.value = false
    }
}

onMounted(async () => {
    if (canSupervisor.value) {
        await loadQueue()
    }
})

const openDetail = (item: ServiceRequest) => {
    router.push(`/maintenance/service/${item.id}`)
}
</script>

<template>
    <div class="space-y-6">
        <Message v-if="!canSupervisor" severity="warn" :closable="false">
            หน้านี้สำหรับหัวหน้าช่าง และผู้ดูแลระบบ
        </Message>

        <template v-else>
            <div class="flex items-center justify-between gap-3">
                <div>
                    <h1 class="text-2xl font-bold text-gray-900">หน้าตรวจสอบงานโดยหัวหน้าช่าง</h1>
                    <p class="text-sm text-gray-500 mt-1">ตรวจผลงานที่ช่างส่งมา
                        แล้วประเมินว่าจะส่งกลับให้ช่างหรือส่งต่อจ้างภายนอก</p>
                </div>
                <Button label="รีเฟรช" icon="pi pi-refresh" outlined :loading="loading" @click="loadQueue" />
            </div>

            <Message v-if="successMessage" severity="success" :closable="false">{{ successMessage }}</Message>
            <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>

            <Card>
                <template #content>
                    <DataTable :value="queueItems" :loading="loading" striped-rows size="small"
                        empty-message="ไม่มีรายการรอหัวหน้าตรวจสอบ">
                        <Column field="workOrderNo" header="เลขใบงาน" style="width: 10rem" />
                        <Column field="title" header="รายการงาน / สถานที่">
                            <template #body="{ data }">
                                <div class="font-medium text-gray-800">{{ data.title }}</div>
                                <div class="text-xs text-gray-500 line-clamp-1">{{ data.description }}</div>
                                <div v-if="data.buildingName || data.locationDetail" class="text-xs text-blue-600 mt-0.5">
                                    <i class="pi pi-map-marker mr-1" />{{ [data.buildingName, data.locationDetail].filter(Boolean).join(' — ') }}
                                </div>
                            </template>
                        </Column>
                        <Column field="technicianName" header="ช่างผู้ส่ง" style="width: 9rem">
                            <template #body="{ data }">{{ data.technicianName || '-' }}</template>
                        </Column>
                        <Column header="ผลการตรวจสอบของช่าง" style="width: 16rem">
                            <template #body="{ data }">
                                <div v-if="data.technicianDiagnosis" class="text-sm text-gray-700 line-clamp-2">
                                    {{ data.technicianDiagnosis }}
                                </div>
                                <span v-else class="text-xs text-gray-400">-</span>
                            </template>
                        </Column>
                        <Column header="เหตุผลที่ส่งหัวหน้า" style="width: 16rem">
                            <template #body="{ data }">
                                <div v-if="data.escalationReason" class="text-sm text-orange-700 line-clamp-2">
                                    {{ data.escalationReason }}
                                </div>
                                <span v-else class="text-xs text-gray-400">-</span>
                            </template>
                        </Column>
                        <Column header="สถานะ" style="width: 9rem">
                            <template #body>
                                <Tag severity="danger" value="รอหัวหน้าพิจารณา" />
                            </template>
                        </Column>
                        <Column header="วันที่ส่งมา" style="width: 11rem">
                            <template #body="{ data }">{{ formatDate(data.updatedAt ?? data.createdAt) }}</template>
                        </Column>
                        <Column header="จัดการ" style="width: 8rem">
                            <template #body="{ data }">
                                <Button label="พิจารณา" icon="pi pi-search" size="small" severity="warn" outlined
                                    @click="openDetail(data)" />
                            </template>
                        </Column>
                    </DataTable>
                </template>
            </Card>
        </template>
    </div>
</template>
