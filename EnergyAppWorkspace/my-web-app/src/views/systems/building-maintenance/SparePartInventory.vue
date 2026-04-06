<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import {
  MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
  MAINTENANCE_TECHNICIAN_PERMISSION,
  hasMaintenancePermission,
} from '@/config/maintenancePermissions'

import Card from 'primevue/card'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Message from 'primevue/message'

interface SparePart {
  id: string
  partCode: string
  partName: string
  unit: string
  quantityOnHand: number
  reorderPoint: number
  isActive: boolean
}

interface IssueRequestItem {
  id: string
  sparePartId: string
  qtyRequested: number
  qtyApproved: number
}

interface IssueRequest {
  id: string
  requestNo: string
  status: 'pending' | 'approved' | 'rejected'
  requestedByUid: string
  requestedByName: string
  approvedByName: string
  rejectReason: string
  note: string
  createdAt: string
  approvedAt: string | null
  items: IssueRequestItem[]
}

interface WorkOrderOption {
  id: string
  workOrderNo: string
  title: string
  status: string
  assetNumber?: string
}

const authStore = useAuthStore()
const role = computed(() => (authStore.user?.role ?? '').toLowerCase())
const currentUid = computed(() => authStore.user?.uid ?? authStore.user?.id ?? '')
const maintenanceAdminSystems = computed(() => authStore.userProfile?.adminSystems ?? [])
const isTechnician = computed(
  () =>
    role.value === 'technician' ||
    hasMaintenancePermission(maintenanceAdminSystems.value, MAINTENANCE_TECHNICIAN_PERMISSION),
)
const isAdminBuilding = computed(
  () =>
    ['superadmin', 'adminbuilding'].includes(role.value) ||
    hasMaintenancePermission(
      maintenanceAdminSystems.value,
      MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
    ),
)
const canViewInventory = computed(() => isTechnician.value || isAdminBuilding.value)
const canManageStock = computed(() => isAdminBuilding.value)
const canApproveIssue = computed(() => isAdminBuilding.value)
const canRequestIssue = computed(() => isTechnician.value || isAdminBuilding.value)

const loading = ref(false)
const loadingRequests = ref(false)
const loadingWorkOrders = ref(false)
const parts = ref<SparePart[]>([])
const requests = ref<IssueRequest[]>([])
const workOrders = ref<WorkOrderOption[]>([])
const successMessage = ref('')
const errorMessage = ref('')
const partSearch = ref('')
const requestSearch = ref('')
const activeTab = ref<'parts' | 'requests'>('parts')

const createPartVisible = ref(false)
const receiveVisible = ref(false)
const requestVisible = ref(false)
const approveVisible = ref(false)

const createPartForm = ref({
  partCode: '',
  partName: '',
  unit: 'pcs',
  reorderPoint: 0,
})

const receiveForm = ref({
  sparePartId: '',
  quantity: 0,
  note: '',
})

const issueForm = ref({
  serviceRequestId: null as string | null,
  note: '',
  sparePartId: '',
  qtyRequested: 1,
})

const selectedIssueRequest = ref<IssueRequest | null>(null)
const approveRows = ref<Array<{ sparePartId: string; qtyApproved: number }>>([])
const approveNote = ref('')
const rejectReason = ref('')

const lowStockCount = computed(
  () => parts.value.filter((p) => p.quantityOnHand <= p.reorderPoint).length,
)

const pendingRequestsCount = computed(
  () => requests.value.filter((r) => r.status === 'pending').length,
)

const requestsForView = computed(() => {
  if (canApproveIssue.value) return requests.value
  return requests.value.filter((r) => r.requestedByUid === currentUid.value)
})

const normalizedPartSearch = computed(() => partSearch.value.trim().toLowerCase())
const filteredParts = computed(() => {
  if (!normalizedPartSearch.value) return parts.value

  return parts.value.filter((part) => {
    const partCode = part.partCode?.toLowerCase() ?? ''
    const partName = part.partName?.toLowerCase() ?? ''
    const unit = part.unit?.toLowerCase() ?? ''
    return (
      partCode.includes(normalizedPartSearch.value) ||
      partName.includes(normalizedPartSearch.value) ||
      unit.includes(normalizedPartSearch.value)
    )
  })
})

const normalizedRequestSearch = computed(() => requestSearch.value.trim().toLowerCase())
const filteredRequestsForView = computed(() => {
  if (!normalizedRequestSearch.value) return requestsForView.value

  return requestsForView.value.filter((request) => {
    const requestNo = request.requestNo?.toLowerCase() ?? ''
    const requestedByName = request.requestedByName?.toLowerCase() ?? ''
    const note = request.note?.toLowerCase() ?? ''
    return (
      requestNo.includes(normalizedRequestSearch.value) ||
      requestedByName.includes(normalizedRequestSearch.value) ||
      note.includes(normalizedRequestSearch.value)
    )
  })
})

const workOrderOptions = computed(() =>
  workOrders.value.map((w) => ({
    label: `${w.workOrderNo} - ${w.title}${w.assetNumber ? ` (${w.assetNumber})` : ''}`,
    value: w.id,
  })),
)

const formatDateTime = (value: string | null | undefined): string => {
  if (!value) return '-'
  return new Date(value).toLocaleString('th-TH', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const requestStatusTag = (status: string): { severity: 'secondary' | 'success' | 'danger'; label: string } => {
  if (status === 'approved') return { severity: 'success', label: 'อนุมัติแล้ว' }
  if (status === 'rejected') return { severity: 'danger', label: 'ปฏิเสธแล้ว' }
  return { severity: 'secondary', label: 'รออนุมัติ' }
}

const clearMessages = () => {
  successMessage.value = ''
  errorMessage.value = ''
}

const loadParts = async () => {
  loading.value = true
  try {
    const res = await api.get('/SparePart')
    parts.value = res.data ?? []
  } catch (e) {
    errorMessage.value = 'โหลดรายการอะไหล่ไม่สำเร็จ'
  } finally {
    loading.value = false
  }
}

const loadRequests = async () => {
  loadingRequests.value = true
  try {
    const res = await api.get('/SparePart/issue-requests')
    requests.value = res.data ?? []
  } catch {
    errorMessage.value = 'โหลดรายการขอเบิกไม่สำเร็จ'
  } finally {
    loadingRequests.value = false
  }
}

const loadWorkOrders = async () => {
  loadingWorkOrders.value = true
  try {
    const res = await api.get('/ServiceRequest', { params: { take: 500 } })
    const items = (res.data.items ?? res.data ?? []) as WorkOrderOption[]
    workOrders.value = items.filter((x) => x.status !== 'closed')
  } catch {
    errorMessage.value = 'โหลดเลขใบงานไม่สำเร็จ'
  } finally {
    loadingWorkOrders.value = false
  }
}

const refreshAll = async () => {
  clearMessages()
  await Promise.all([loadParts(), loadRequests(), loadWorkOrders()])
}

onMounted(async () => {
  if (canViewInventory.value) {
    await refreshAll()
  }
})

const submitCreatePart = async () => {
  if (!createPartForm.value.partCode.trim() || !createPartForm.value.partName.trim()) {
    errorMessage.value = 'กรุณาระบุรหัสและชื่ออะไหล่'
    return
  }

  try {
    await api.post('/SparePart', {
      partCode: createPartForm.value.partCode,
      partName: createPartForm.value.partName,
      unit: createPartForm.value.unit,
      reorderPoint: createPartForm.value.reorderPoint,
    })

    createPartVisible.value = false
    createPartForm.value = { partCode: '', partName: '', unit: 'pcs', reorderPoint: 0 }
    successMessage.value = 'เพิ่มรายการอะไหล่สำเร็จ'
    await refreshAll()
  } catch {
    errorMessage.value = 'เพิ่มรายการอะไหล่ไม่สำเร็จ'
  }
}

const submitReceive = async () => {
  if (!receiveForm.value.sparePartId || receiveForm.value.quantity <= 0) {
    errorMessage.value = 'กรุณาเลือกอะไหล่และระบุจำนวนรับเข้า'
    return
  }

  try {
    await api.post('/SparePart/receive', {
      sparePartId: receiveForm.value.sparePartId,
      quantity: receiveForm.value.quantity,
      note: receiveForm.value.note,
    })

    receiveVisible.value = false
    receiveForm.value = { sparePartId: '', quantity: 0, note: '' }
    successMessage.value = 'รับอะไหล่เข้าคลังสำเร็จ'
    await refreshAll()
  } catch {
    errorMessage.value = 'รับอะไหล่เข้าคลังไม่สำเร็จ'
  }
}

const submitIssueRequest = async () => {
  if (!issueForm.value.sparePartId || issueForm.value.qtyRequested <= 0) {
    errorMessage.value = 'กรุณาเลือกอะไหล่และจำนวนที่ต้องการเบิก'
    return
  }

  try {
    await api.post('/SparePart/issue-requests', {
      serviceRequestId: issueForm.value.serviceRequestId,
      note: issueForm.value.note,
      items: [
        {
          sparePartId: issueForm.value.sparePartId,
          qtyRequested: issueForm.value.qtyRequested,
        },
      ],
    })

    requestVisible.value = false
    issueForm.value = { serviceRequestId: null, note: '', sparePartId: '', qtyRequested: 1 }
    successMessage.value = 'ส่งคำขอเบิกอะไหล่สำเร็จ'
    await refreshAll()
  } catch {
    errorMessage.value = 'ส่งคำขอเบิกไม่สำเร็จ'
  }
}

const openApproveDialog = (request: IssueRequest) => {
  selectedIssueRequest.value = request
  approveRows.value = request.items.map((item) => ({
    sparePartId: item.sparePartId,
    qtyApproved: item.qtyRequested,
  }))
  approveNote.value = ''
  rejectReason.value = ''
  approveVisible.value = true
}

const submitApprove = async () => {
  if (!selectedIssueRequest.value) return

  try {
    await api.post(`/SparePart/issue-requests/${selectedIssueRequest.value.id}/approve`, {
      note: approveNote.value,
      items: approveRows.value,
    })

    approveVisible.value = false
    selectedIssueRequest.value = null
    successMessage.value = 'อนุมัติคำขอเบิกสำเร็จ'
    await refreshAll()
  } catch {
    errorMessage.value = 'อนุมัติคำขอเบิกไม่สำเร็จ'
  }
}

const submitReject = async () => {
  if (!selectedIssueRequest.value) return
  if (!rejectReason.value.trim()) {
    errorMessage.value = 'กรุณาระบุเหตุผลการปฏิเสธ'
    return
  }

  try {
    await api.post(`/SparePart/issue-requests/${selectedIssueRequest.value.id}/reject`, {
      reason: rejectReason.value,
    })

    approveVisible.value = false
    selectedIssueRequest.value = null
    successMessage.value = 'ปฏิเสธคำขอเบิกสำเร็จ'
    await refreshAll()
  } catch {
    errorMessage.value = 'ปฏิเสธคำขอเบิกไม่สำเร็จ'
  }
}
</script>

<template>
  <div class="space-y-6">
    <Message v-if="!canViewInventory" severity="warn" :closable="false">
      เฉพาะบทบาทช่างอาคารและธุรการงานอาคารเท่านั้นที่ใช้งานคลังอะไหล่ได้
    </Message>

    <template v-if="canViewInventory">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">คลังอะไหล่งานอาคาร</h1>
          <p class="text-sm text-gray-400 mt-1">ตรวจสอบสต็อก, ขอเบิก, อนุมัติ และรับเข้าอะไหล่</p>
        </div>
        <div class="flex items-center gap-2">
          <Button v-if="canRequestIssue" label="ขอเบิกอะไหล่" icon="pi pi-send" @click="requestVisible = true" />
          <Button v-if="canManageStock" label="รับเข้าอะไหล่" icon="pi pi-plus-circle" severity="help"
            @click="receiveVisible = true" />
          <Button v-if="canManageStock" label="เพิ่มรายการอะไหล่" icon="pi pi-box" severity="secondary"
            @click="createPartVisible = true" />
        </div>
      </div>

      <Message v-if="successMessage" severity="success" :closable="false">{{ successMessage }}</Message>
      <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>

      <div class="grid grid-cols-3 gap-4">
        <div class="bg-white rounded-xl border border-gray-100 p-4">
          <p class="text-xs text-gray-400">จำนวนอะไหล่ทั้งหมด</p>
          <p class="text-2xl font-bold text-gray-900 mt-1">{{ parts.length }}</p>
        </div>
        <div class="rounded-xl border border-yellow-200 p-4 bg-yellow-50">
          <p class="text-xs text-yellow-700">รายการต่ำกว่าจุดสั่งซื้อ</p>
          <p class="text-2xl font-bold text-yellow-800 mt-1">{{ lowStockCount }}</p>
        </div>
        <div class="rounded-xl border border-blue-200 p-4 bg-blue-50">
          <p class="text-xs text-blue-700">คำขอเบิกรออนุมัติ</p>
          <p class="text-2xl font-bold text-blue-800 mt-1">{{ pendingRequestsCount }}</p>
        </div>
      </div>

      <div class="flex flex-wrap items-center gap-2 border-b border-gray-200 pb-2">
        <button type="button" class="px-3 py-1.5 rounded-lg text-sm font-medium transition"
          :class="activeTab === 'parts' ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-700 hover:bg-gray-200'"
          @click="activeTab = 'parts'">
          รายการอะไหล่ ({{ filteredParts.length }})
        </button>
        <button type="button" class="px-3 py-1.5 rounded-lg text-sm font-medium transition"
          :class="activeTab === 'requests' ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-700 hover:bg-gray-200'"
          @click="activeTab = 'requests'">
          คำขอเบิกอะไหล่ ({{ filteredRequestsForView.length }})
        </button>
      </div>

      <Card v-if="activeTab === 'parts'">
        <template #title>รายการอะไหล่</template>
        <template #content>
          <div class="mb-3">
            <InputText v-model="partSearch" class="w-full md:w-80" placeholder="ค้นหา: รหัสอะไหล่, ชื่ออะไหล่, หน่วย" />
          </div>
          <DataTable :value="filteredParts" :loading="loading" size="small" striped-rows>
            <Column field="partCode" header="รหัส" style="width: 8rem" />
            <Column field="partName" header="ชื่ออะไหล่" />
            <Column field="unit" header="หน่วย" style="width: 5rem" />
            <Column field="quantityOnHand" header="คงเหลือ" style="width: 8rem">
              <template #body="{ data }">
                <span class="font-semibold"
                  :class="data.quantityOnHand <= data.reorderPoint ? 'text-red-600' : 'text-gray-800'">
                  {{ data.quantityOnHand }}
                </span>
              </template>
            </Column>
            <Column field="reorderPoint" header="จุดสั่งซื้อ" style="width: 8rem" />
            <Column header="สถานะ" style="width: 8rem">
              <template #body="{ data }">
                <Tag :severity="data.quantityOnHand <= data.reorderPoint ? 'danger' : 'success'"
                  :value="data.quantityOnHand <= data.reorderPoint ? 'สต็อกต่ำ' : 'ปกติ'" />
              </template>
            </Column>
          </DataTable>
        </template>
      </Card>

      <Card v-else>
        <template #title>คำขอเบิกอะไหล่</template>
        <template #content>
          <div class="mb-3">
            <InputText v-model="requestSearch" class="w-full md:w-96" placeholder="ค้นหา: เลขคำขอ, ผู้ขอ, หมายเหตุ" />
          </div>
          <DataTable :value="filteredRequestsForView" :loading="loadingRequests" size="small" striped-rows>
            <Column field="requestNo" header="เลขคำขอ" style="width: 10rem" />
            <Column field="requestedByName" header="ผู้ขอ" style="width: 12rem" />
            <Column field="createdAt" header="วันที่ขอ" style="width: 11rem">
              <template #body="{ data }">{{ formatDateTime(data.createdAt) }}</template>
            </Column>
            <Column header="รายการ" style="width: 7rem">
              <template #body="{ data }">{{ data.items?.length ?? 0 }} รายการ</template>
            </Column>
            <Column field="status" header="สถานะ" style="width: 10rem">
              <template #body="{ data }">
                <Tag :severity="requestStatusTag(data.status).severity" :value="requestStatusTag(data.status).label" />
              </template>
            </Column>
            <Column header="จัดการ" style="width: 8rem">
              <template #body="{ data }">
                <Button v-if="canApproveIssue && data.status === 'pending'" label="พิจารณา" size="small"
                  @click="openApproveDialog(data)" />
                <span v-else class="text-xs text-gray-400">-</span>
              </template>
            </Column>
          </DataTable>
        </template>
      </Card>

      <Dialog v-model:visible="createPartVisible" header="เพิ่มรายการอะไหล่" modal :style="{ width: '30rem' }">
        <div class="space-y-3">
          <div>
            <label class="text-sm text-gray-600">รหัสอะไหล่</label>
            <InputText v-model="createPartForm.partCode" class="w-full" />
          </div>
          <div>
            <label class="text-sm text-gray-600">ชื่ออะไหล่</label>
            <InputText v-model="createPartForm.partName" class="w-full" />
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="text-sm text-gray-600">หน่วย</label>
              <InputText v-model="createPartForm.unit" class="w-full" />
            </div>
            <div>
              <label class="text-sm text-gray-600">จุดสั่งซื้อ</label>
              <InputNumber v-model="createPartForm.reorderPoint" class="w-full" :min="0" />
            </div>
          </div>
        </div>
        <template #footer>
          <Button label="ยกเลิก" text @click="createPartVisible = false" />
          <Button label="บันทึก" @click="submitCreatePart" />
        </template>
      </Dialog>

      <Dialog v-model:visible="receiveVisible" header="รับอะไหล่เข้าคลัง" modal :style="{ width: '32rem' }">
        <div class="space-y-3">
          <div>
            <label class="text-sm text-gray-600">รหัสอะไหล่</label>
            <select v-model="receiveForm.sparePartId" class="w-full border rounded-lg px-3 py-2">
              <option value="">-- เลือกอะไหล่ --</option>
              <option v-for="part in parts" :key="part.id" :value="part.id">{{ part.partCode }} - {{ part.partName }}
              </option>
            </select>
          </div>
          <div>
            <label class="text-sm text-gray-600">จำนวนรับเข้า</label>
            <InputNumber v-model="receiveForm.quantity" class="w-full" :min="0" />
          </div>
          <div>
            <label class="text-sm text-gray-600">หมายเหตุ</label>
            <Textarea v-model="receiveForm.note" rows="3" class="w-full" />
          </div>
        </div>
        <template #footer>
          <Button label="ยกเลิก" text @click="receiveVisible = false" />
          <Button label="รับเข้า" @click="submitReceive" />
        </template>
      </Dialog>

      <Dialog v-model:visible="requestVisible" header="สร้างคำขอเบิกอะไหล่" modal :style="{ width: '34rem' }">
        <div class="space-y-3">
          <div>
            <label class="text-sm text-gray-600">เลขใบแจ้งซ่อม (ถ้ามี)</label>
            <Select v-model="issueForm.serviceRequestId" :options="workOrderOptions" option-label="label"
              option-value="value" :loading="loadingWorkOrders" filter show-clear placeholder="เลือกเลขใบงาน"
              class="w-full" />
          </div>
          <div>
            <label class="text-sm text-gray-600">อะไหล่ที่ต้องการ</label>
            <select v-model="issueForm.sparePartId" class="w-full border rounded-lg px-3 py-2">
              <option value="">-- เลือกอะไหล่ --</option>
              <option v-for="part in parts" :key="part.id" :value="part.id">{{ part.partCode }} - {{ part.partName }}
                (คงเหลือ
                {{ part.quantityOnHand }})</option>
            </select>
          </div>
          <div>
            <label class="text-sm text-gray-600">จำนวนที่ต้องการเบิก</label>
            <InputNumber v-model="issueForm.qtyRequested" class="w-full" :min="1" />
          </div>
          <div>
            <label class="text-sm text-gray-600">หมายเหตุ</label>
            <Textarea v-model="issueForm.note" rows="3" class="w-full" />
          </div>
        </div>
        <template #footer>
          <Button label="ยกเลิก" text @click="requestVisible = false" />
          <Button label="ส่งคำขอเบิก" @click="submitIssueRequest" />
        </template>
      </Dialog>

      <Dialog v-model:visible="approveVisible" header="พิจารณาคำขอเบิก" modal :style="{ width: '38rem' }">
        <div class="space-y-3">
          <p class="text-sm text-gray-600">
            คำขอ: <span class="font-mono">{{ selectedIssueRequest?.requestNo }}</span>
          </p>
          <div v-for="row in approveRows" :key="row.sparePartId" class="grid grid-cols-2 gap-3 items-center">
            <div class="text-sm text-gray-700">
              {{parts.find((x) => x.id === row.sparePartId)?.partCode}} - {{parts.find((x) => x.id ===
                row.sparePartId)?.partName}}
            </div>
            <InputNumber v-model="row.qtyApproved" :min="0" class="w-full" />
          </div>
          <div>
            <label class="text-sm text-gray-600">หมายเหตุอนุมัติ</label>
            <Textarea v-model="approveNote" rows="2" class="w-full" />
          </div>
          <div>
            <label class="text-sm text-gray-600">เหตุผลปฏิเสธ (กรณีปฏิเสธ)</label>
            <Textarea v-model="rejectReason" rows="2" class="w-full" />
          </div>
        </div>
        <template #footer>
          <Button label="ปฏิเสธ" severity="danger" text @click="submitReject" />
          <Button label="อนุมัติ" @click="submitApprove" />
        </template>
      </Dialog>
    </template>
  </div>
</template>
