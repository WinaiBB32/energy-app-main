<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAppToast } from '@/composables/useAppToast'
import api from '@/services/api'
import { SYSTEMS } from '@/config/systems'

import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
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

const toast = useAppToast()
const router = useRouter()

interface Department { id: string; name: string }
const departments = ref<Department[]>([])

const systemModules = SYSTEMS

const users = ref<AppUser[]>([])
const isLoading = ref(true)
const searchQuery = ref('')
const statusFilter = ref<string>('all')

// 🟢 ดึงข้อมูลจาก .NET
const fetchData = async () => {
  isLoading.value = true
  try {
    const [usersRes, deptsRes] = await Promise.all([
      api.get('/User', { params: { pageSize: 200 } }),
      api.get('/Department/all')
    ])
    users.value = usersRes.data.items ?? usersRes.data
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
      <DataTable :value="filteredUsers" :loading="isLoading" paginator :rows="10"
        @row-click="(e) => router.push(`/admin/users/${e.data.id}/permissions`)"
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