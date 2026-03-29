<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { collection, query, orderBy, onSnapshot, addDoc, updateDoc, deleteDoc, doc, serverTimestamp, Timestamp, type QuerySnapshot, type QueryDocumentSnapshot } from 'firebase/firestore'
import { db } from '@/firebase/config'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Button from 'primevue/button'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'

// ─── Types ────────────────────────────────────────────────────────────────────
interface ServiceRequest {
  id: string
  title: string
  description: string
  category: string
  priority: string
  status: string
  extension: string
  requesterName: string
  requesterEmail: string
  assignedTo?: string
  note?: string
  createdAt: Timestamp | null
  updatedAt?: Timestamp | null
}

// ─── Store ────────────────────────────────────────────────────────────────────
const router = useRouter()
const authStore = useAuthStore()
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('ipphone')

// ─── Options ──────────────────────────────────────────────────────────────────
const categoryOptions = [
  { label: 'แจ้งซ่อมโทรศัพท์', value: 'repair' },
  { label: 'ขอติดตั้งสาย/เบอร์ใหม่', value: 'install' },
  { label: 'ขอย้ายสาย/เบอร์', value: 'move' },
  { label: 'ปัญหาคุณภาพเสียง', value: 'quality' },
  { label: 'อื่น ๆ', value: 'other' },
]
const priorityOptions = [
  { label: 'ปกติ', value: 'normal' },
  { label: 'เร่งด่วน', value: 'urgent' },
  { label: 'วิกฤต', value: 'critical' },
]
const statusOptions = [
  { label: 'รอดำเนินการ', value: 'pending' },
  { label: 'กำลังดำเนินการ', value: 'in_progress' },
  { label: 'เสร็จสิ้น', value: 'done' },
  { label: 'ยกเลิก', value: 'cancelled' },
]

// ─── State ────────────────────────────────────────────────────────────────────
const requests = ref<ServiceRequest[]>([])
const loading = ref(true)
const saving = ref(false)
const successMsg = ref('')

// Form
const form = ref({
  title: '',
  description: '',
  category: 'repair',
  priority: 'normal',
  extension: '',
})
const formVisible = ref(false)
const formError = ref('')

// Edit / Delete
const editVisible = ref(false)
const deleteVisible = ref(false)
const selectedRequest = ref<ServiceRequest | null>(null)
const editForm = ref({ status: 'pending', assignedTo: '', note: '' })
const requestToDelete = ref<ServiceRequest | null>(null)

// Filter
const filterStatus = ref<string | null>(null)
const filterSearch = ref('')

// ─── Firestore ────────────────────────────────────────────────────────────────
let unsub: (() => void) | null = null

onMounted(() => {
  const q = query(collection(db, 'ipphone_service_requests'), orderBy('createdAt', 'desc'))
  unsub = onSnapshot(q, (snap: QuerySnapshot) => {
    requests.value = snap.docs.map((d: QueryDocumentSnapshot) => ({ id: d.id, ...d.data() } as ServiceRequest))
    loading.value = false
  })
})

onUnmounted(() => {
  if (unsub) unsub()
})

// ─── Computed ─────────────────────────────────────────────────────────────────
const filteredRequests = computed(() => {
  let list = requests.value
  if (filterStatus.value) list = list.filter((r) => r.status === filterStatus.value)
  if (filterSearch.value.trim()) {
    const q = filterSearch.value.trim().toLowerCase()
    list = list.filter(
      (r) =>
        r.title.toLowerCase().includes(q) ||
        r.extension.includes(q) ||
        r.requesterName.toLowerCase().includes(q),
    )
  }
  return list
})

const statCounts = computed(() => ({
  pending: requests.value.filter((r) => r.status === 'pending').length,
  in_progress: requests.value.filter((r) => r.status === 'in_progress').length,
  done: requests.value.filter((r) => r.status === 'done').length,
}))

// ─── Helpers ──────────────────────────────────────────────────────────────────
function formatDate(ts: Timestamp | null | undefined): string {
  if (!ts) return '-'
  return ts.toDate().toLocaleString('th-TH', {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

function categoryLabel(val: string) {
  return categoryOptions.find((c) => c.value === val)?.label ?? val
}

function priorityTag(val: string): { severity: 'info' | 'warn' | 'danger'; label: string } {
  if (val === 'urgent') return { severity: 'warn', label: 'เร่งด่วน' }
  if (val === 'critical') return { severity: 'danger', label: 'วิกฤต' }
  return { severity: 'info', label: 'ปกติ' }
}

function statusTag(val: string): { severity: 'secondary' | 'info' | 'success' | 'danger'; label: string } {
  if (val === 'pending') return { severity: 'secondary', label: 'รอดำเนินการ' }
  if (val === 'in_progress') return { severity: 'info', label: 'กำลังดำเนินการ' }
  if (val === 'done') return { severity: 'success', label: 'เสร็จสิ้น' }
  return { severity: 'danger', label: 'ยกเลิก' }
}

// ─── Actions ──────────────────────────────────────────────────────────────────
function openForm() {
  form.value = { title: '', description: '', category: 'repair', priority: 'normal', extension: '' }
  formError.value = ''
  formVisible.value = true
}

async function submitRequest() {
  if (!form.value.title.trim()) { formError.value = 'กรุณาระบุหัวข้อ'; return }
  if (!form.value.description.trim()) { formError.value = 'กรุณาระบุรายละเอียด'; return }
  saving.value = true
  try {
    await addDoc(collection(db, 'ipphone_service_requests'), {
      ...form.value,
      status: 'pending',
      requesterName: authStore.user?.displayName ?? authStore.user?.email ?? '',
      requesterEmail: authStore.user?.email ?? '',
      createdAt: serverTimestamp(),
      updatedAt: serverTimestamp(),
    })
    formVisible.value = false
    successMsg.value = 'ส่งคำร้องเรียบร้อยแล้ว'
    setTimeout(() => (successMsg.value = ''), 4000)
  } catch {
    formError.value = 'เกิดข้อผิดพลาด กรุณาลองใหม่'
  } finally {
    saving.value = false
  }
}

function openDetail(r: ServiceRequest) {
  router.push(`/ipphone/service/${r.id}`)
}

function openEdit(r: ServiceRequest) {
  selectedRequest.value = r
  editForm.value = {
    status: r.status,
    assignedTo: r.assignedTo ?? '',
    note: r.note ?? '',
  }
  editVisible.value = true
}

async function saveEdit() {
  if (!selectedRequest.value) return
  saving.value = true
  try {
    await updateDoc(doc(db, 'ipphone_service_requests', selectedRequest.value.id), {
      status: editForm.value.status,
      assignedTo: editForm.value.assignedTo,
      note: editForm.value.note,
      updatedAt: serverTimestamp(),
    })
    logAudit(
      { uid: authStore.user?.uid ?? '', displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.userProfile?.role ?? 'user' },
      'UPDATE', 'ServiceRequest', `อัปเดตสถานะ [${selectedRequest.value.id}] → ${editForm.value.status}`,
    )
    editVisible.value = false
    successMsg.value = 'อัปเดตสถานะเรียบร้อยแล้ว'
    setTimeout(() => (successMsg.value = ''), 4000)
  } catch {
    alert('เกิดข้อผิดพลาด')
  } finally {
    saving.value = false
  }
}

function confirmDelete(r: ServiceRequest) {
  requestToDelete.value = r
  deleteVisible.value = true
}

async function deleteRequest() {
  if (!requestToDelete.value) return
  try {
    const req = requestToDelete.value
    await deleteDoc(doc(db, 'ipphone_service_requests', req.id))
    logAudit(
      { uid: authStore.user?.uid ?? '', displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.userProfile?.role ?? 'user' },
      'DELETE', 'ServiceRequest', `ลบคำร้อง: ${req.title} (${req.requesterEmail})`,
    )
    deleteVisible.value = false
    successMsg.value = 'ลบคำร้องเรียบร้อยแล้ว'
    setTimeout(() => (successMsg.value = ''), 4000)
  } catch {
    alert('เกิดข้อผิดพลาด')
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900">ประวัติการให้บริการ / แจ้งปัญหา</h1>
        <p class="text-sm text-gray-400 mt-1">แจ้งปัญหาหรือขอรับบริการเกี่ยวกับระบบโทรศัพท์</p>
      </div>
      <Button label="แจ้งปัญหา / ขอรับบริการ" icon="pi pi-plus" @click="openForm" />
    </div>

    <!-- Success -->
    <Message v-if="successMsg" severity="success" :closable="false">{{ successMsg }}</Message>

    <!-- Stat cards -->
    <div class="grid grid-cols-3 gap-4">
      <div
        class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3 cursor-pointer hover:border-yellow-300 transition-colors"
        :class="filterStatus === 'pending' ? 'border-yellow-400 bg-yellow-50' : ''"
        @click="filterStatus = filterStatus === 'pending' ? null : 'pending'">
        <div class="w-10 h-10 bg-yellow-100 rounded-lg flex items-center justify-center">
          <i class="pi pi-clock text-yellow-600"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-gray-900">{{ statCounts.pending }}</p>
          <p class="text-xs text-gray-400">รอดำเนินการ</p>
        </div>
      </div>
      <div
        class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3 cursor-pointer hover:border-blue-300 transition-colors"
        :class="filterStatus === 'in_progress' ? 'border-blue-400 bg-blue-50' : ''"
        @click="filterStatus = filterStatus === 'in_progress' ? null : 'in_progress'">
        <div class="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
          <i class="pi pi-sync text-blue-600"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-gray-900">{{ statCounts.in_progress }}</p>
          <p class="text-xs text-gray-400">กำลังดำเนินการ</p>
        </div>
      </div>
      <div
        class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3 cursor-pointer hover:border-green-300 transition-colors"
        :class="filterStatus === 'done' ? 'border-green-400 bg-green-50' : ''"
        @click="filterStatus = filterStatus === 'done' ? null : 'done'">
        <div class="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
          <i class="pi pi-check-circle text-green-600"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-gray-900">{{ statCounts.done }}</p>
          <p class="text-xs text-gray-400">เสร็จสิ้น</p>
        </div>
      </div>
    </div>

    <!-- Table -->
    <Card>
      <template #content>
        <!-- Filters -->
        <div class="flex gap-3 mb-4">
          <InputText v-model="filterSearch" placeholder="ค้นหาหัวข้อ, เบอร์, ชื่อผู้แจ้ง..." class="flex-1" />
          <Select v-model="filterStatus" :options="[{ label: 'ทุกสถานะ', value: null }, ...statusOptions]"
            option-label="label" option-value="value" placeholder="กรองสถานะ" class="w-48" />
        </div>

        <DataTable :value="filteredRequests" :loading="loading" paginator :rows="15" size="small" striped-rows
          empty-message="ไม่มีคำร้อง">
          <Column field="createdAt" header="วันที่" style="width: 9rem">
            <template #body="{ data }">
              <span class="text-xs text-gray-500">{{ formatDate(data.createdAt) }}</span>
            </template>
          </Column>
          <Column field="title" header="หัวข้อ">
            <template #body="{ data }">
              <span class="font-medium text-gray-800">{{ data.title }}</span>
              <p class="text-xs text-gray-400 truncate max-w-xs">{{ data.description }}</p>
            </template>
          </Column>
          <Column field="category" header="ประเภท" style="width: 10rem">
            <template #body="{ data }">
              <span class="text-sm text-gray-600">{{ categoryLabel(data.category) }}</span>
            </template>
          </Column>
          <Column field="extension" header="เบอร์" style="width: 6rem">
            <template #body="{ data }">
              <span class="font-mono text-sm">{{ data.extension || '-' }}</span>
            </template>
          </Column>
          <Column field="priority" header="ความสำคัญ" style="width: 7rem">
            <template #body="{ data }">
              <Tag :severity="priorityTag(data.priority).severity" :value="priorityTag(data.priority).label" />
            </template>
          </Column>
          <Column field="status" header="สถานะ" style="width: 9rem">
            <template #body="{ data }">
              <Tag :severity="statusTag(data.status).severity" :value="statusTag(data.status).label" />
            </template>
          </Column>
          <Column field="requesterName" header="ผู้แจ้ง" style="width: 9rem">
            <template #body="{ data }">
              <span class="text-sm text-gray-600">{{ data.requesterName }}</span>
            </template>
          </Column>
          <Column header="จัดการ" style="width: 8rem">
            <template #body="{ data }">
              <div class="flex gap-1">
                <Button icon="pi pi-comments" text rounded size="small" severity="secondary" @click="openDetail(data)"
                  v-tooltip="'แชท / ดูรายละเอียด'" />
                <Button v-if="isAdmin" icon="pi pi-pencil" text rounded size="small" severity="info"
                  @click="openEdit(data)" v-tooltip="'อัปเดตสถานะ'" />
                <Button v-if="isAdmin" icon="pi pi-trash" text rounded size="small" severity="danger"
                  @click="confirmDelete(data)" v-tooltip="'ลบ'" />
              </div>
            </template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <!-- ── New Request Dialog ── -->
    <Dialog v-model:visible="formVisible" header="แจ้งปัญหา / ขอรับบริการ" modal :style="{ width: '38rem' }">
      <div class="space-y-4">
        <Message v-if="formError" severity="error" :closable="false">{{ formError }}</Message>

        <div class="grid grid-cols-2 gap-4">
          <div class="col-span-2 flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">หัวข้อ <span class="text-red-500">*</span></label>
            <InputText v-model="form.title" placeholder="ระบุหัวข้อปัญหา" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">ประเภท</label>
            <Select v-model="form.category" :options="categoryOptions" option-label="label" option-value="value" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">ความสำคัญ</label>
            <Select v-model="form.priority" :options="priorityOptions" option-label="label" option-value="value" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">เบอร์ต่อ (ถ้ามี)</label>
            <InputText v-model="form.extension" placeholder="เช่น 1234" />
          </div>
          <div class="col-span-2 flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">รายละเอียด <span class="text-red-500">*</span></label>
            <Textarea v-model="form.description" rows="4" placeholder="อธิบายปัญหาหรือสิ่งที่ต้องการ..."
              class="w-full" />
          </div>
        </div>
      </div>
      <template #footer>
        <Button label="ยกเลิก" text @click="formVisible = false" />
        <Button label="ส่งคำร้อง" icon="pi pi-send" :loading="saving" @click="submitRequest" />
      </template>
    </Dialog>

    <!-- ── Edit Status Dialog (admin) ── -->
    <Dialog v-model:visible="editVisible" header="อัปเดตสถานะคำร้อง" modal :style="{ width: '32rem' }">
      <div class="space-y-4">
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">สถานะ</label>
          <Select v-model="editForm.status" :options="statusOptions" option-label="label" option-value="value" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">ผู้รับผิดชอบ</label>
          <InputText v-model="editForm.assignedTo" placeholder="ชื่อผู้รับผิดชอบ" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">หมายเหตุ</label>
          <Textarea v-model="editForm.note" rows="3" placeholder="บันทึกเพิ่มเติม..." class="w-full" />
        </div>
      </div>
      <template #footer>
        <Button label="ยกเลิก" text @click="editVisible = false" />
        <Button label="บันทึก" icon="pi pi-check" :loading="saving" @click="saveEdit" />
      </template>
    </Dialog>

    <!-- ── Delete Confirm Dialog ── -->
    <Dialog v-model:visible="deleteVisible" header="ยืนยันการลบ" modal :style="{ width: '28rem' }">
      <div class="flex items-start gap-3">
        <div class="w-10 h-10 bg-red-100 rounded-full flex items-center justify-center shrink-0">
          <i class="pi pi-exclamation-triangle text-red-600"></i>
        </div>
        <div>
          <p class="text-gray-800 font-medium">ต้องการลบคำร้องนี้ใช่หรือไม่?</p>
          <p class="text-sm text-gray-500 mt-1">{{ requestToDelete?.title }}</p>
          <p class="text-xs text-red-500 mt-2">การกระทำนี้ไม่สามารถย้อนกลับได้</p>
        </div>
      </div>
      <template #footer>
        <Button label="ยกเลิก" text @click="deleteVisible = false" />
        <Button label="ลบ" icon="pi pi-trash" severity="danger" @click="deleteRequest" />
      </template>
    </Dialog>
  </div>
</template>
