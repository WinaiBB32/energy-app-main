<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'
import { useAppToast } from '@/composables/useAppToast'
import api from '@/services/api' // <--- ใช้ API ของเรา
import axios from 'axios'


import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Message from 'primevue/message'
import ToggleSwitch from 'primevue/toggleswitch'

interface AppUser {
  id: string
  email: string
  displayName: string
  departmentId: string | null
  role: 'user' | 'admin' | 'superadmin' | string
  status: 'pending' | 'active' | 'suspended' | string
  adminSystems: string[]
  accessibleSystems: string[]
}

const authStore = useAuthStore()
const toast = useAppToast()

interface Department { id: string; name: string }
const departments = ref<Department[]>([])

const roles = [
  { id: 'User', name: 'ผู้ใช้งานทั่วไป (User)' },
  { id: 'Admin', name: 'ผู้ดูแลองค์กร (Admin)' },
  { id: 'SuperAdmin', name: 'ผู้ดูแลระบบสูงสุด (SuperAdmin)' },
]

const statuses = [
  { id: 'pending', name: 'รออนุมัติ' },
  { id: 'active', name: 'ใช้งานปกติ' },
  { id: 'suspended', name: 'ระงับการใช้งาน' },
]

interface SystemModule {
  id: string
  name: string
  shortLabel: string
  description: string
  icon: string
  cardBorder: string
}

// ลิสต์โมดูลยังเหมือนเดิม...
const systemModules: SystemModule[] = [
  { id: 'system1', name: 'ระบบไฟฟ้า & Solar', shortLabel: 'ไฟฟ้า & Solar', description: 'บิลค่าไฟ กฟภ./กฟน. และ Solar', icon: 'pi-bolt', cardBorder: 'border-l-amber-400' },
  { id: 'system2', name: 'ระบบน้ำประปา', shortLabel: 'น้ำประปา', description: 'บันทึกมิเตอร์และค่าน้ำ', icon: 'pi-tint', cardBorder: 'border-l-cyan-400' },
  { id: 'system3', name: 'ระบบน้ำมันเชื้อเพลิง', shortLabel: 'น้ำมันเชื้อเพลิง', description: 'การเติมน้ำมันและใบรับรอง', icon: 'pi-car', cardBorder: 'border-l-rose-400' },
  { id: 'system4', name: 'ระบบโทรศัพท์', shortLabel: 'ค่าโทรศัพท์', description: 'บันทึกค่าใช้จ่ายโทรศัพท์', icon: 'pi-phone', cardBorder: 'border-l-emerald-400' },
  { id: 'system5', name: 'ระบบสารบรรณ', shortLabel: 'สารบรรณ', description: 'สถิติงานรับ–ส่งเอกสาร', icon: 'pi-folder-open', cardBorder: 'border-l-violet-400' },
  { id: 'system6', name: 'ระบบ IP-Phone', shortLabel: 'IP-Phone', description: 'สมุดโทรศัพท์และสถิติการโทร', icon: 'pi-desktop', cardBorder: 'border-l-teal-400' },
  { id: 'system7', name: 'ระบบไปรษณีย์', shortLabel: 'ไปรษณีย์', description: 'สถิติจัดส่ง ธรรมดา/ลงทะเบียน/EMS', icon: 'pi-envelope', cardBorder: 'border-l-blue-400' },
  { id: 'system8', name: 'สถิติห้องประชุมส่วนกลาง', shortLabel: 'ห้องประชุม', description: 'สถิติการใช้ห้องประชุม', icon: 'pi-users', cardBorder: 'border-l-indigo-400' },
]

const users = ref<AppUser[]>([])
const isLoading = ref(true)
const searchQuery = ref('')
const statusFilter = ref<string>('all')

const detailDialogVisible = ref(false)
const editDialogVisible = ref(false)
const isSaving = ref(false)
const successMessage = ref('')
const errorMessage = ref('')

const selectedUser = ref<AppUser | null>(null)
const editingUser = ref<AppUser | null>(null)

// ตรวจสอบสิทธิ์ว่าปรับแต่งระบบได้ไหม
const canEditPerSystemMatrix = computed(() => editingUser.value?.role === 'User')

// Logic Toggle Matrix เหมือนเดิม
function userHasSystem(sysId: string): boolean {
  return editingUser.value?.accessibleSystems.includes(sysId) ?? false
}
function adminHasSystem(sysId: string): boolean {
  return editingUser.value?.adminSystems.includes(sysId) ?? false
}

function onUserSystemToggle(sysId: string, value: boolean): void {
  if (!editingUser.value || editingUser.value.role !== 'User') return
  const acc = editingUser.value.accessibleSystems
  const adm = editingUser.value.adminSystems
  if (value) {
    if (!acc.includes(sysId)) acc.push(sysId)
  } else {
    const i = acc.indexOf(sysId); if (i >= 0) acc.splice(i, 1)
    const j = adm.indexOf(sysId); if (j >= 0) adm.splice(j, 1)
  }
}
function onAdminSystemToggle(sysId: string, value: boolean): void {
  if (!editingUser.value || editingUser.value.role !== 'User') return
  const acc = editingUser.value.accessibleSystems
  const adm = editingUser.value.adminSystems
  if (value) {
    if (!adm.includes(sysId)) adm.push(sysId)
    if (!acc.includes(sysId)) acc.push(sysId)
  } else {
    const j = adm.indexOf(sysId); if (j >= 0) adm.splice(j, 1)
  }
}

function selectedUserHasAccess(sysId: string): boolean { return selectedUser.value?.accessibleSystems?.includes(sysId) ?? false }
function selectedUserIsAdmin(sysId: string): boolean { return selectedUser.value?.adminSystems?.includes(sysId) ?? false }

// 🟢 ดึงข้อมูลจาก .NET
const fetchData = async () => {
  isLoading.value = true
  try {
    const [usersRes, deptsRes] = await Promise.all([
      api.get('/User'),
      api.get('/Department/all')
    ])
    users.value = usersRes.data
    departments.value = deptsRes.data
  } catch (error) {
    toast.fromError(error, 'โหลดข้อมูลไม่สำเร็จ')
  } finally {
    isLoading.value = false
  }
}

onMounted(() => { fetchData() })

const stats = computed(() => ({
  total: users.value.length,
  pending: users.value.filter((u) => u.status === 'pending').length,
  active: users.value.filter((u) => u.status === 'active').length,
  suspended: users.value.filter((u) => u.status === 'suspended').length,
}))

const filteredUsers = computed(() => {
  let list = users.value
  if (statusFilter.value !== 'all') list = list.filter((u) => u.status === statusFilter.value)
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return list
  return list.filter(
    (u) => u.displayName?.toLowerCase().includes(q) || u.email?.toLowerCase().includes(q),
  )
})

const openDetail = (user: AppUser) => { selectedUser.value = user; detailDialogVisible.value = true }
const openEdit = (user: AppUser) => {
  successMessage.value = ''; errorMessage.value = ''
  editingUser.value = { ...user, adminSystems: [...(user.adminSystems || [])], accessibleSystems: [...(user.accessibleSystems || [])] }
  editDialogVisible.value = true
}

// 🟢 อัปเดตข้อมูลไปที่ .NET
const saveUser = async () => {
  if (!editingUser.value) return
  try {
    isSaving.value = true
    errorMessage.value = ''

    // ยิง API PUT
    await api.put(`/User/${editingUser.value.id}`, {
      displayName: editingUser.value.displayName,
      departmentId: editingUser.value.departmentId,
      role: editingUser.value.role,
      status: editingUser.value.status,
      adminSystems: editingUser.value.adminSystems,
      accessibleSystems: editingUser.value.accessibleSystems
    })

    const actor = { uid: authStore.user?.id ?? '', displayName: authStore.user?.firstName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.user?.role ?? 'User' }
    logAudit(actor, 'UPDATE', 'UserManagement', `จัดการผู้ใช้: ${editingUser.value.email}`)

    successMessage.value = 'บันทึกสำเร็จ'
    await fetchData() // โหลดใหม่ให้ตารางอัปเดต

    if (selectedUser.value?.id === editingUser.value.id) {
      selectedUser.value = users.value.find(u => u.id === editingUser.value!.id) || null
    }

    setTimeout(() => { editDialogVisible.value = false }, 1000)
  } catch (error: unknown) {
    if (axios.isAxiosError(error)) errorMessage.value = error.response?.data?.message || 'เกิดข้อผิดพลาดในการบันทึก'
    else errorMessage.value = 'เกิดข้อผิดพลาด'
  } finally {
    isSaving.value = false
  }
}

// 🟢 ลบผู้ใช้งานผ่าน .NET
const deleteUser = async (userId: string) => {
  if (!confirm('ต้องการลบข้อมูลผู้ใช้นี้ออกจากระบบใช่หรือไม่?')) return
  try {
    await api.delete(`/User/${userId}`)

    const actor = { uid: authStore.user?.id ?? '', displayName: authStore.user?.firstName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.user?.role ?? 'User' }
    logAudit(actor, 'DELETE', 'UserManagement', `ลบผู้ใช้ ID: ${userId}`)

    detailDialogVisible.value = false
    await fetchData()
    toast.success('ลบผู้ใช้สำเร็จ')
  } catch (error) {
    toast.fromError(error, 'ไม่สามารถลบผู้ใช้งานได้')
  }
}

const getDeptName = (id: string | null) => {
  if (!id) return 'ไม่ระบุ'
  return departments.value.find((x) => x.id === id)?.name || 'ไม่ระบุ'
}
const getRoleSeverity = (role: string) => {
  if (role === 'SuperAdmin') return 'danger'
  if (role === 'Admin') return 'warn'
  return 'info'
}

const getStatusName = (status: string) => status === 'active' ? 'ใช้งานปกติ' : status === 'suspended' ? 'ระงับใช้งาน' : 'รออนุมัติ'
const getRoleLabel = (role: string) => {
  if (role === 'SuperAdmin') return 'SuperAdmin'
  if (role === 'Admin') return 'Admin องค์กร'
  return 'User ทั่วไป'
}
const getAvatarColor = (role: string) => {
  if (role === 'SuperAdmin') return 'bg-red-100 text-red-600'
  if (role === 'Admin') return 'bg-violet-100 text-violet-700'
  return 'bg-blue-100 text-blue-600'
}

const filterTabs = [
  { key: 'all', label: 'ทั้งหมด', icon: 'pi-users' },
  { key: 'pending', label: 'รออนุมัติ', icon: 'pi-clock' },
  { key: 'active', label: 'ใช้งานอยู่', icon: 'pi-check-circle' },
  { key: 'suspended', label: 'ระงับแล้ว', icon: 'pi-ban' },
]
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10 space-y-6">

    <div>
      <h2 class="text-3xl font-bold text-gray-800">จัดการสิทธิ์ผู้ใช้งาน</h2>
      <p class="text-gray-500 mt-1">อนุมัติการเข้าใช้งาน กำหนดหน่วยงาน และจัดการสิทธิ์ระบบ</p>
    </div>

    <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <div class="bg-white rounded-xl border border-gray-100 shadow-sm p-4 flex items-center gap-3">
        <div class="w-10 h-10 rounded-full bg-gray-100 flex items-center justify-center text-gray-600">
          <i class="pi pi-users"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-gray-800">{{ stats.total }}</p>
          <p class="text-xs text-gray-500">ผู้ใช้ทั้งหมด</p>
        </div>
      </div>
      <div class="bg-white rounded-xl border border-amber-200 shadow-sm p-4 flex items-center gap-3">
        <div class="w-10 h-10 rounded-full bg-amber-100 flex items-center justify-center text-amber-600">
          <i class="pi pi-clock"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-amber-600">{{ stats.pending }}</p>
          <p class="text-xs text-gray-500">รออนุมัติ</p>
        </div>
      </div>
      <div class="bg-white rounded-xl border border-green-200 shadow-sm p-4 flex items-center gap-3">
        <div class="w-10 h-10 rounded-full bg-green-100 flex items-center justify-center text-green-600">
          <i class="pi pi-check-circle"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-green-600">{{ stats.active }}</p>
          <p class="text-xs text-gray-500">ใช้งานอยู่</p>
        </div>
      </div>
      <div class="bg-white rounded-xl border border-red-200 shadow-sm p-4 flex items-center gap-3">
        <div class="w-10 h-10 rounded-full bg-red-100 flex items-center justify-center text-red-500">
          <i class="pi pi-ban"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-red-500">{{ stats.suspended }}</p>
          <p class="text-xs text-gray-500">ระงับการใช้งาน</p>
        </div>
      </div>
    </div>

    <div class="flex flex-col sm:flex-row gap-3 items-start sm:items-center justify-between">
      <div class="flex gap-1 bg-gray-100 p-1 rounded-lg">
        <button v-for="tab in filterTabs" :key="tab.key" @click="statusFilter = tab.key"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm font-medium transition-all" :class="statusFilter === tab.key
            ? 'bg-white text-gray-800 shadow-sm'
            : 'text-gray-500 hover:text-gray-700'">
          <i :class="`pi ${tab.icon} text-xs`"></i>
          {{ tab.label }}
          <span v-if="tab.key === 'pending' && stats.pending > 0"
            class="bg-amber-500 text-white text-[10px] font-bold px-1.5 py-0.5 rounded-full leading-none">
            {{ stats.pending }}
          </span>
        </button>
      </div>

      <IconField class="w-full sm:w-72">
        <InputIcon class="pi pi-search" />
        <InputText v-model="searchQuery" placeholder="ค้นหาชื่อหรืออีเมล..." class="w-full text-sm" />
      </IconField>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
      <DataTable :value="filteredUsers" :loading="isLoading" paginator :rows="10" @row-click="(e) => openDetail(e.data)"
        :rowClass="(data: AppUser) => [
          'cursor-pointer transition-colors',
          data.status === 'pending' ? 'bg-amber-50/60 hover:bg-amber-50' : 'hover:bg-blue-50/40'
        ]" emptyMessage="ไม่พบข้อมูลผู้ใช้งาน">
        <Column header="ผู้ใช้งาน" style="min-width: 240px">
          <template #body="{ data }">
            <div class="flex items-center gap-3 py-1">
              <div
                :class="`w-9 h-9 rounded-full flex items-center justify-center font-bold text-sm shrink-0 ${getAvatarColor(data.role)}`">
                {{ (data.displayName || data.email || '?').charAt(0).toUpperCase() }}
              </div>
              <div>
                <div class="font-semibold text-gray-800 leading-tight">{{ data.displayName || 'ไม่มีชื่อ' }}</div>
                <div class="text-xs text-gray-400 mt-0.5">{{ data.email }}</div>
              </div>
            </div>
          </template>
        </Column>

        <Column header="หน่วยงาน">
          <template #body="{ data }">
            <span class="text-sm text-gray-700">{{ getDeptName(data.departmentId) }}</span>
          </template>
        </Column>

        <Column header="สถานะ / สิทธิ์">
          <template #body="{ data }">
            <div class="flex flex-col gap-1">
              <div class="flex items-center gap-1.5">
                <span v-if="data.status === 'pending'"
                  class="w-1.5 h-1.5 rounded-full bg-amber-400 animate-pulse"></span>
                <span v-else-if="data.status === 'active'" class="w-1.5 h-1.5 rounded-full bg-green-400"></span>
                <span v-else class="w-1.5 h-1.5 rounded-full bg-red-400"></span>
                <span class="text-xs font-medium" :class="{
                  'text-amber-600': data.status === 'pending',
                  'text-green-600': data.status === 'active',
                  'text-red-500': data.status === 'suspended',
                }">{{ getStatusName(data.status) }}</span>
              </div>
              <Tag :value="getRoleLabel(data.role)" :severity="getRoleSeverity(data.role)" rounded
                class="text-[10px] w-fit" />
            </div>
          </template>
        </Column>

        <Column header="สิทธิ์ระบบ" style="min-width: 200px">
          <template #body="{ data }">
            <div class="flex flex-col gap-1">
              <div class="flex gap-1 flex-wrap items-center">
                <span class="text-[9px] font-bold text-gray-400 uppercase tracking-wide w-full">User</span>
                <template v-if="data.accessibleSystems?.length">
                  <span v-for="sys in data.accessibleSystems.slice(0, 3)" :key="'u-' + sys"
                    class="bg-blue-50 text-blue-700 text-[10px] px-1.5 py-0.5 rounded border border-blue-100 font-medium">
                    {{systemModules.find((m) => m.id === sys)?.shortLabel ?? sys}}
                  </span>
                  <span v-if="data.accessibleSystems.length > 3"
                    class="bg-gray-100 text-gray-500 text-[10px] px-1.5 py-0.5 rounded">
                    +{{ data.accessibleSystems.length - 3 }}
                  </span>
                </template>
                <span v-else class="text-[10px] text-red-400 italic">ไม่มี</span>
              </div>
              <div class="flex gap-1 flex-wrap items-center">
                <span class="text-[9px] font-bold text-gray-400 uppercase tracking-wide w-full">Admin</span>
                <template v-if="data.adminSystems?.length">
                  <span v-for="sys in data.adminSystems.slice(0, 3)" :key="'a-' + sys"
                    class="bg-amber-50 text-amber-800 text-[10px] px-1.5 py-0.5 rounded border border-amber-100 font-medium">
                    {{systemModules.find((m) => m.id === sys)?.shortLabel ?? sys}}
                  </span>
                  <span v-if="data.adminSystems.length > 3"
                    class="bg-gray-100 text-gray-500 text-[10px] px-1.5 py-0.5 rounded">
                    +{{ data.adminSystems.length - 3 }}
                  </span>
                </template>
                <span v-else class="text-[10px] text-gray-400">—</span>
              </div>
            </div>
          </template>
        </Column>

        <Column style="width: 48px">
          <template #body><i class="pi pi-angle-right text-gray-300"></i></template>
        </Column>
      </DataTable>
    </div>

    <Dialog v-model:visible="detailDialogVisible" modal :style="{ width: '34rem' }" :breakpoints="{ '575px': '95vw' }">
      <template #header>
        <div class="flex items-center gap-3 w-full">
          <div
            :class="`w-12 h-12 rounded-full flex items-center justify-center font-bold text-lg shrink-0 ${selectedUser ? getAvatarColor(selectedUser.role) : 'bg-gray-100 text-gray-600'}`">
            {{ (selectedUser?.displayName || selectedUser?.email || '?').charAt(0).toUpperCase() }}
          </div>
          <div class="flex-1 min-w-0">
            <p class="font-bold text-gray-800 truncate">{{ selectedUser?.displayName || 'ไม่มีชื่อ' }}</p>
            <p class="text-xs text-gray-400 truncate">{{ selectedUser?.email }}</p>
          </div>
        </div>
      </template>

      <div v-if="selectedUser" class="space-y-4 py-1">
        <div class="flex items-center gap-2 p-3 rounded-lg" :class="{
          'bg-amber-50 border border-amber-200': selectedUser.status === 'pending',
          'bg-green-50 border border-green-200': selectedUser.status === 'active',
          'bg-red-50 border border-red-200': selectedUser.status === 'suspended',
        }">
          <i class="pi"
            :class="{ 'pi-clock text-amber-500': selectedUser.status === 'pending', 'pi-check-circle text-green-500': selectedUser.status === 'active', 'pi-ban text-red-500': selectedUser.status === 'suspended' }"></i>
          <span class="font-semibold text-sm"
            :class="{ 'text-amber-700': selectedUser.status === 'pending', 'text-green-700': selectedUser.status === 'active', 'text-red-700': selectedUser.status === 'suspended' }">
            {{ getStatusName(selectedUser.status) }}
          </span>
          <Tag :value="getRoleLabel(selectedUser.role)" :severity="getRoleSeverity(selectedUser.role)" rounded
            class="ml-auto text-xs" />
        </div>

        <div class="bg-gray-50 rounded-lg p-3 border border-gray-100">
          <p class="text-[11px] text-gray-400 uppercase tracking-wide mb-1">หน่วยงาน</p>
          <p class="font-semibold text-gray-800 text-sm">{{ getDeptName(selectedUser.departmentId) }}</p>
        </div>

        <div class="space-y-2">
          <p class="text-[11px] text-gray-400 uppercase tracking-wide">สิทธิ์ตามระบบ</p>
          <div v-if="selectedUser.role === 'SuperAdmin' || selectedUser.role === 'Admin'"
            class="rounded-lg border border-violet-100 bg-violet-50/80 px-3 py-2.5 text-sm text-violet-800">
            <i class="pi pi-info-circle mr-1.5 text-violet-500"></i>
            บทบาท {{ selectedUser.role === 'SuperAdmin' ? 'Superadmin' : 'Admin องค์กร' }} มีสิทธิ์ครอบคลุมตามนโยบายแอป
          </div>
          <div class="max-h-56 overflow-y-auto pr-1 grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div v-for="sys in systemModules" :key="sys.id"
              class="rounded-lg border border-gray-100 bg-white p-2.5 shadow-sm flex items-center justify-between gap-2 border-l-4"
              :class="sys.cardBorder">
              <div class="flex items-center gap-2 min-w-0">
                <div class="w-8 h-8 rounded-md bg-gray-50 flex items-center justify-center shrink-0">
                  <i :class="['pi text-sm text-gray-600', sys.icon]"></i>
                </div>
                <span class="text-xs font-semibold text-gray-800 truncate">{{ sys.shortLabel }}</span>
              </div>
              <div class="flex flex-col items-end gap-0.5 shrink-0">
                <Tag v-if="selectedUserHasAccess(sys.id)" value="User" severity="success"
                  class="text-[10px] px-1.5! py-0!" />
                <Tag v-if="selectedUserIsAdmin(sys.id)" value="Admin" severity="warn"
                  class="text-[10px] px-1.5! py-0!" />
                <span v-if="!selectedUserHasAccess(sys.id) && !selectedUserIsAdmin(sys.id)"
                  class="text-[10px] text-gray-400">—</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <div class="flex items-center justify-between w-full">
          <Button label="ลบผู้ใช้" icon="pi pi-trash" severity="danger" text size="small"
            @click="deleteUser(selectedUser!.id)" />
          <Button label="แก้ไขสิทธิ์" icon="pi pi-user-edit" @click="openEdit(selectedUser!)" />
        </div>
      </template>
    </Dialog>

    <Dialog v-model:visible="editDialogVisible" modal :style="{ width: 'min(56rem, 96vw)' }"
      :breakpoints="{ '575px': '98vw' }">
      <template #header>
        <div class="flex items-center gap-2">
          <i class="pi pi-user-edit text-blue-500"></i><span class="font-bold text-gray-800">แก้ไขสิทธิ์ผู้ใช้งาน</span>
        </div>
      </template>

      <Message v-if="successMessage" severity="success" :closable="false" class="mb-3">{{ successMessage }}</Message>
      <Message v-if="errorMessage" severity="error" :closable="false" class="mb-3">{{ errorMessage }}</Message>

      <div v-if="editingUser" class="space-y-5 py-1">
        <div class="grid grid-cols-2 gap-4">
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-gray-500 uppercase tracking-wide">อีเมล</label>
            <InputText :value="editingUser.email" disabled class="w-full bg-gray-50 text-gray-500" />
          </div>
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-gray-500 uppercase tracking-wide">ชื่อแสดงผล</label>
            <InputText v-model="editingUser.displayName" class="w-full" />
          </div>
        </div>

        <div class="grid grid-cols-2 gap-4 pt-4 border-t border-gray-100">
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-gray-500 uppercase tracking-wide">หน่วยงาน</label>
            <Select v-model="editingUser.departmentId" :options="departments" optionLabel="name" optionValue="id"
              placeholder="เลือกหน่วยงาน" class="w-full" />
          </div>
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-blue-500 uppercase tracking-wide flex items-center gap-1">
              <i class="pi pi-shield text-[10px]"></i> สถานะบัญชี
            </label>
            <div class="flex flex-col gap-1.5">
              <button v-for="s in statuses" :key="s.id" @click="editingUser.status = s.id as AppUser['status']"
                class="flex items-center gap-2.5 w-full px-3 py-2 rounded-lg border-2 text-sm font-medium transition-all text-left"
                :class="{
                  'border-amber-400 bg-amber-50 text-amber-700': editingUser.status === s.id && s.id === 'pending',
                  'border-green-400 bg-green-50 text-green-700': editingUser.status === s.id && s.id === 'active',
                  'border-red-400 bg-red-50 text-red-700': editingUser.status === s.id && s.id === 'suspended',
                  'border-gray-200 bg-white text-gray-400 hover:border-gray-300': editingUser.status !== s.id,
                }">
                <i class="pi text-base"
                  :class="{ 'pi-clock': s.id === 'pending', 'pi-check-circle': s.id === 'active', 'pi-ban': s.id === 'suspended' }"></i>
                {{ s.name }}
                <i v-if="editingUser.status === s.id" class="pi pi-check ml-auto text-xs"></i>
              </button>
            </div>
          </div>
        </div>

        <div class="space-y-4 pt-4 border-t border-gray-100">
          <div class="space-y-1.5">
            <label class="text-xs font-semibold text-gray-500 uppercase tracking-wide">ระดับสิทธิ์ (Role)</label>
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-2">
              <button v-for="r in roles" :key="r.id" type="button" @click="editingUser.role = r.id as AppUser['role']"
                class="flex flex-col items-center gap-1 py-2.5 px-2 rounded-xl border-2 text-sm font-medium transition-all"
                :class="{
                  'border-blue-400 bg-blue-50 text-blue-800': editingUser.role === r.id && r.id === 'User',
                  'border-violet-400 bg-violet-50 text-violet-800': editingUser.role === r.id && r.id === 'Admin',
                  'border-red-400 bg-red-50 text-red-800': editingUser.role === r.id && r.id === 'SuperAdmin',
                  'border-gray-200 bg-white text-gray-500 hover:border-gray-300': editingUser.role !== r.id,
                }">
                <i class="pi text-lg"
                  :class="{ 'pi-user': r.id === 'User', 'pi-building': r.id === 'Admin', 'pi-crown': r.id === 'SuperAdmin' }"></i>
                <span class="text-[11px] leading-tight text-center">{{ r.name }}</span>
              </button>
            </div>
          </div>

          <div class="space-y-2">
            <div class="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-2">
              <div>
                <label class="text-xs font-semibold text-gray-500 uppercase tracking-wide">จัดการสิทธิ์ตามระบบ</label>
                <p class="text-[11px] text-gray-400 mt-0.5">
                  เปิด <span class="font-medium text-emerald-700">User</span> = เข้า Portal/ระบบนั้นได้ · เปิด <span
                    class="font-medium text-amber-700">Admin</span> = บันทึกข้อมูล
                </p>
              </div>
            </div>

            <div v-if="!canEditPerSystemMatrix"
              class="rounded-xl border border-dashed border-gray-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
              <i class="pi pi-lock text-slate-400 mr-2"></i> เลือกบทบาท <strong>User</strong> เพื่อกำหนดสิทธิ์รายระบบ
            </div>

            <div v-else
              class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 max-h-[min(52vh,480px)] overflow-y-auto pr-1">
              <div v-for="sys in systemModules" :key="sys.id"
                class="rounded-xl border border-gray-200 bg-white shadow-sm p-4 flex flex-col gap-3 border-l-4 transition-shadow hover:shadow-md"
                :class="sys.cardBorder">
                <div class="flex items-start gap-3">
                  <div
                    class="w-10 h-10 rounded-lg bg-gray-50 flex items-center justify-center shrink-0 border border-gray-100">
                    <i :class="['pi text-lg text-gray-600', sys.icon]"></i>
                  </div>
                  <div class="min-w-0 flex-1">
                    <p class="font-bold text-gray-800 text-sm leading-tight">{{ sys.shortLabel }}</p>
                    <p class="text-[11px] text-gray-500 mt-1 leading-snug">{{ sys.description }}</p>
                  </div>
                </div>
                <div class="space-y-2.5 pt-2 border-t border-gray-100">
                  <div class="flex items-center justify-between gap-2">
                    <span class="text-xs font-medium text-gray-600">เข้าใช้งาน (User)</span>
                    <ToggleSwitch :modelValue="userHasSystem(sys.id)"
                      @update:modelValue="(v) => onUserSystemToggle(sys.id, v)" />
                  </div>
                  <div class="flex items-center justify-between gap-2">
                    <span class="text-xs font-medium text-gray-600">ผู้ดูแล (Admin)</span>
                    <ToggleSwitch :modelValue="adminHasSystem(sys.id)"
                      @update:modelValue="(v) => onAdminSystemToggle(sys.id, v)" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <Button label="ยกเลิก" icon="pi pi-times" text severity="secondary" @click="editDialogVisible = false" />
        <Button label="บันทึกข้อมูล" icon="pi pi-save" :loading="isSaving" @click="saveUser" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
:deep(.p-datatable-header-cell) {
  background-color: #f8fafc !important;
  color: #64748b !important;
  font-weight: 600 !important;
  font-size: 0.75rem !important;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

:deep(.p-datatable-tbody > tr) {
  border-bottom: 1px solid #f1f5f9;
}
</style>