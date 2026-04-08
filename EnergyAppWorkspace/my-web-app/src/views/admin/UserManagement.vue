<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'
import { useAppToast } from '@/composables/useAppToast'
import api from '@/services/api'

import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import InputText from 'primevue/inputtext'

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
const route = useRoute()
const router = useRouter()

interface Department { id: string; name: string }
const departments = ref<Department[]>([])

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
  { id: 'system9', name: 'ระบบแจ้งซ่อมงานอาคาร', shortLabel: 'ซ่อมงานอาคาร', description: 'ใบงานซ่อม คลังอะไหล่ และช่างภายนอก', icon: 'pi-wrench', cardBorder: 'border-l-orange-400' },
  { id: 'system10', name: 'Admin Tool', shortLabel: 'Admin Tool', description: 'เครื่องมือผู้ดูแลระบบและการกำหนดสิทธิ์', icon: 'pi-shield', cardBorder: 'border-l-slate-400' },
  { id: 'system7', name: 'ระบบไปรษณีย์', shortLabel: 'ไปรษณีย์', description: 'สถิติจัดส่ง ธรรมดา/ลงทะเบียน/EMS', icon: 'pi-envelope', cardBorder: 'border-l-blue-400' },
  { id: 'system8', name: 'สถิติห้องประชุมส่วนกลาง', shortLabel: 'ห้องประชุม', description: 'สถิติการใช้ห้องประชุม', icon: 'pi-users', cardBorder: 'border-l-indigo-400' },
]

const focusedSystemId = computed(() => {
  const q = route.query.focusSystem
  return typeof q === 'string' ? q : ''
})

const focusedSystem = computed(() =>
  systemModules.find((m) => m.id === focusedSystemId.value) ?? null,
)

const users = ref<AppUser[]>([])
const isLoading = ref(true)
const searchQuery = ref('')
const statusFilter = ref<string>('all')

const detailDialogVisible = ref(false)
const selectedUser = ref<AppUser | null>(null)

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

  if (focusedSystemId.value) {
    list = list.filter(
      (u) =>
        (u.accessibleSystems ?? []).includes(focusedSystemId.value) ||
        (u.adminSystems ?? []).includes(focusedSystemId.value),
    )
  }

  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return list
  return list.filter(
    (u) => u.displayName?.toLowerCase().includes(q) || u.email?.toLowerCase().includes(q),
  )
})

const openDetail = (user: AppUser) => { selectedUser.value = user; detailDialogVisible.value = true }
const openEdit = (user: AppUser) => {
  detailDialogVisible.value = false
  router.push(`/admin/users/${user.id}/permissions`)
}

// 🟢 ลบผู้ใช้งานผ่าน .NET
// 🔑 รีเซ็ตรหัสผ่าน
const resetPasswordDialogVisible = ref(false)
const resetPasswordValue = ref('')
const isResetting = ref(false)

const openResetPassword = () => {
  resetPasswordValue.value = ''
  resetPasswordDialogVisible.value = true
}

const confirmResetPassword = async () => {
  if (!selectedUser.value) return
  if (resetPasswordValue.value.length < 6) {
    toast.error('รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร')
    return
  }
  isResetting.value = true
  try {
    await api.post(`/User/${selectedUser.value.id}/reset-password`, { newPassword: resetPasswordValue.value })
    const actor = { uid: authStore.user?.id ?? '', displayName: authStore.user?.firstName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.user?.role ?? 'User' }
    logAudit(actor, 'UPDATE', 'UserManagement', `รีเซ็ตรหัสผ่านผู้ใช้: ${selectedUser.value.email}`)
    resetPasswordDialogVisible.value = false
    toast.success('รีเซ็ตรหัสผ่านสำเร็จ')
  } catch (error) {
    toast.fromError(error, 'ไม่สามารถรีเซ็ตรหัสผ่านได้')
  } finally {
    isResetting.value = false
  }
}

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
  if (role === 'AdminBuilding') return 'contrast'
  if (role === 'Supervisor') return 'warn'
  if (role === 'Technician') return 'success'
  if (role === 'Officer') return 'info'
  if (role === 'SuperAdmin') return 'danger'
  if (role === 'Admin') return 'warn'
  return 'info'
}

const getStatusName = (status: string) => status === 'active' ? 'ใช้งานปกติ' : status === 'suspended' ? 'ระงับใช้งาน' : 'รออนุมัติ'
const getRoleLabel = (role: string) => {
  if (role === 'SuperAdmin') return 'SuperAdmin'
  if (role === 'Admin') return 'Admin องค์กร'
  if (role === 'Officer') return 'เจ้าหน้าที่'
  if (role === 'AdminBuilding') return 'ธุรการช่างนอก'
  if (role === 'Supervisor') return 'หัวหน้าช่าง'
  if (role === 'Technician') return 'ช่างซ่อม'
  return 'User ทั่วไป'
}
const getAvatarColor = (role: string) => {
  if (role === 'SuperAdmin') return 'bg-red-100 text-red-600'
  if (role === 'Admin') return 'bg-violet-100 text-violet-700'
  if (role === 'Officer') return 'bg-sky-100 text-sky-700'
  if (role === 'AdminBuilding') return 'bg-orange-100 text-orange-700'
  if (role === 'Supervisor') return 'bg-amber-100 text-amber-700'
  if (role === 'Technician') return 'bg-cyan-100 text-cyan-700'
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
      <div v-if="focusedSystem"
        class="mt-3 inline-flex items-center gap-2 rounded-lg border border-indigo-200 bg-indigo-50 px-3 py-1.5 text-sm text-indigo-700">
        <i class="pi pi-filter text-xs"></i>
        โฟกัสระบบ: <span class="font-semibold">{{ focusedSystem.name }}</span>
      </div>
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
                <span class="text-[9px] font-bold text-gray-400 uppercase tracking-wide w-full">Officer</span>
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
              :class="[sys.cardBorder, focusedSystemId === sys.id ? 'ring-2 ring-indigo-300 bg-indigo-50/40' : '']">
              <div class="flex items-center gap-2 min-w-0">
                <div class="w-8 h-8 rounded-md bg-gray-50 flex items-center justify-center shrink-0">
                  <i :class="['pi text-sm text-gray-600', sys.icon]"></i>
                </div>
                <span class="text-xs font-semibold text-gray-800 truncate">{{ sys.shortLabel }}</span>
              </div>
              <div class="flex flex-col items-end gap-0.5 shrink-0">
                <Tag v-if="selectedUserHasAccess(sys.id)" value="User" severity="success"
                  class="text-[10px] px-1.5! py-0!" />
                <Tag v-if="selectedUserIsAdmin(sys.id)" value="Officer" severity="warn"
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
          <div class="flex gap-2">
            <Button label="รีเซ็ตรหัสผ่าน" icon="pi pi-key" severity="warn" outlined size="small"
              @click="openResetPassword" />
            <Button label="แก้ไขสิทธิ์" icon="pi pi-user-edit" @click="openEdit(selectedUser!)" />
          </div>
        </div>
      </template>
    </Dialog>

    <!-- Reset Password Dialog -->
    <Dialog v-model:visible="resetPasswordDialogVisible" modal header="รีเซ็ตรหัสผ่าน"
      :style="{ width: '22rem' }" :breakpoints="{ '575px': '95vw' }">
      <div class="space-y-3 py-1">
        <p class="text-sm text-gray-600">ตั้งรหัสผ่านใหม่สำหรับ <span class="font-semibold">{{ selectedUser?.displayName || selectedUser?.email }}</span></p>
        <InputText v-model="resetPasswordValue" placeholder="รหัสผ่านใหม่ (อย่างน้อย 6 ตัวอักษร)"
          type="password" class="w-full" />
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <Button label="ยกเลิก" severity="secondary" outlined @click="resetPasswordDialogVisible = false" />
          <Button label="บันทึก" icon="pi pi-check" :loading="isResetting" @click="confirmResetPassword" />
        </div>
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