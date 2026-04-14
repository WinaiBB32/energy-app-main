<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'
import { useAppToast } from '@/composables/useAppToast'
import api from '@/services/api'
import axios from 'axios'

import Button from 'primevue/button'
import Select from 'primevue/select'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import ToggleSwitch from 'primevue/toggleswitch'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import {
    MAINTENANCE_ADMIN_BUILDING_PERMISSION,
    MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
    MAINTENANCE_PERMISSION_KEYS,
    MAINTENANCE_SUPERVISOR_PERMISSION,
    MAINTENANCE_TECHNICIAN_PERMISSION,
    hasMaintenancePermission,
} from '@/config/maintenancePermissions'

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

interface Department {
    id: string
    name: string
}

interface SystemModule {
    id: string
    name: string
    shortLabel: string
    description: string
    icon: string
    cardBorder: string
}


const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const toast = useAppToast()

const userId = computed(() => String(route.params.id || ''))
const isLoading = ref(true)
const isSaving = ref(false)
const successMessage = ref('')
const errorMessage = ref('')

const departments = ref<Department[]>([])
const editingUser = ref<AppUser | null>(null)

const statuses = [
    { id: 'pending', name: 'รออนุมัติ' },
    { id: 'active', name: 'ใช้งานปกติ' },
    { id: 'suspended', name: 'ระงับการใช้งาน' },
]

const systemModules: SystemModule[] = [
    { id: 'system1', name: 'ระบบไฟฟ้า & Solar', shortLabel: 'ไฟฟ้า & Solar', description: 'บิลค่าไฟ กฟภ./กฟน. และ Solar', icon: 'pi-bolt', cardBorder: 'border-l-amber-400' },
    { id: 'system2', name: 'ระบบน้ำประปา', shortLabel: 'น้ำประปา', description: 'บันทึกมิเตอร์และค่าน้ำ', icon: 'pi-tint', cardBorder: 'border-l-cyan-400' },
    { id: 'system3', name: 'ระบบน้ำมันเชื้อเพลิง', shortLabel: 'น้ำมันเชื้อเพลิง', description: 'การเติมน้ำมันและใบรับรอง', icon: 'pi-car', cardBorder: 'border-l-rose-400' },
    { id: 'system4', name: 'ระบบโทรศัพท์', shortLabel: 'ค่าโทรศัพท์', description: 'บันทึกค่าใช้จ่ายโทรศัพท์', icon: 'pi-phone', cardBorder: 'border-l-emerald-400' },
    { id: 'system5', name: 'ระบบสารบรรณ', shortLabel: 'สารบรรณ', description: 'สถิติงานรับ-ส่งเอกสาร', icon: 'pi-folder-open', cardBorder: 'border-l-violet-400' },
    { id: 'system6', name: 'ระบบ IP-Phone', shortLabel: 'IP-Phone', description: 'สมุดโทรศัพท์และสถิติการโทร', icon: 'pi-desktop', cardBorder: 'border-l-teal-400' },
    { id: 'system9', name: 'ระบบแจ้งซ่อมงานอาคาร', shortLabel: 'ซ่อมงานอาคาร', description: 'ใบงานซ่อม คลังอะไหล่ และช่างภายนอก', icon: 'pi-wrench', cardBorder: 'border-l-orange-400' },
    { id: 'system10', name: 'Admin Tool', shortLabel: 'Admin Tool', description: 'เครื่องมือผู้ดูแลระบบและการกำหนดสิทธิ์', icon: 'pi-shield', cardBorder: 'border-l-slate-400' },
    { id: 'system7', name: 'ระบบไปรษณีย์', shortLabel: 'ไปรษณีย์', description: 'สถิติจัดส่ง ธรรมดา/ลงทะเบียน/EMS', icon: 'pi-envelope', cardBorder: 'border-l-blue-400' },
    { id: 'system8', name: 'สถิติห้องประชุมส่วนกลาง', shortLabel: 'ห้องประชุม', description: 'สถิติการใช้ห้องประชุม', icon: 'pi-users', cardBorder: 'border-l-indigo-400' },
]


const ensureSystemAccess = (systemId: string) => {
    if (!editingUser.value) return
    if (!editingUser.value.accessibleSystems.includes(systemId)) {
        editingUser.value.accessibleSystems.push(systemId)
    }
}

const removeMaintenancePermissions = () => {
    if (!editingUser.value) return
    editingUser.value.adminSystems = editingUser.value.adminSystems.filter(
        (systemId) => !MAINTENANCE_PERMISSION_KEYS.includes(systemId as (typeof MAINTENANCE_PERMISSION_KEYS)[number]),
    )
}

const getLegacyMaintenancePermission = (role: string): string | null => {
    const roleText = role.trim().toLowerCase()
    if (roleText === 'technician') return MAINTENANCE_TECHNICIAN_PERMISSION
    if (roleText === 'supervisor') return MAINTENANCE_SUPERVISOR_PERMISSION
    if (roleText === 'adminbuilding') return MAINTENANCE_ADMIN_BUILDING_PERMISSION
    return null
}

const normalizeLegacyMaintenancePermissions = () => {
    if (!editingUser.value) return
    const legacyPermission = getLegacyMaintenancePermission(editingUser.value.role)
    if (!legacyPermission) return

    if (!editingUser.value.adminSystems.includes(legacyPermission)) {
        editingUser.value.adminSystems.push(legacyPermission)
    }
    ensureSystemAccess('system9')
}

const syncGlobalRoleFromPermissions = () => {
    if (!editingUser.value) return

    if (editingUser.value.adminSystems.includes('system10')) {
        editingUser.value.role = 'SuperAdmin'
        return
    }

    // ไม่ downgrade role ที่ SuperAdmin ตั้งเองเป็น Admin
    if (editingUser.value.role === 'Admin') return

    const nonMaintenanceAdminSystems = editingUser.value.adminSystems.filter(
        (systemId) => !MAINTENANCE_PERMISSION_KEYS.includes(systemId as (typeof MAINTENANCE_PERMISSION_KEYS)[number]),
    )

    if (nonMaintenanceAdminSystems.length > 0) {
        editingUser.value.role = 'Officer'
        return
    }

    editingUser.value.role = 'User'
}

const maintenanceRoles = [
    { id: MAINTENANCE_TECHNICIAN_PERMISSION, name: 'ช่างซ่อม' },
    { id: MAINTENANCE_SUPERVISOR_PERMISSION, name: 'หัวหน้าช่าง' },
    { id: MAINTENANCE_ADMIN_BUILDING_PERMISSION, name: 'ธุรการช่างนอก (หน่วยงาน/กอง)' },
    { id: MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION, name: 'ธุรการช่างนอกส่วนกลาง' },
]

const isMaintenanceRoleActive = (roleId: string): boolean =>
    hasMaintenancePermission(editingUser.value?.adminSystems, roleId)

const onMaintenanceRoleToggle = (roleId: string, value: boolean): void => {
    if (!editingUser.value) return

    if (value) {
        if (!editingUser.value.adminSystems.includes(roleId)) {
            editingUser.value.adminSystems.push(roleId)
        }
        ensureSystemAccess('system9')
        syncGlobalRoleFromPermissions()
        return
    }

    editingUser.value.adminSystems = editingUser.value.adminSystems.filter((id) => id !== roleId)
    syncGlobalRoleFromPermissions()
}

const maintenancePermissionSummary = computed(() => {
    if (!editingUser.value) return []
    return maintenanceRoles
        .filter((roleItem) => hasMaintenancePermission(editingUser.value?.adminSystems, roleItem.id))
        .map((roleItem) => roleItem.name)
})

const hasAnyMaintenancePermission = computed(() => maintenancePermissionSummary.value.length > 0)

const onMaintenanceAccessToggle = (value: boolean): void => {
    if (!editingUser.value) return

    onUserSystemToggle('system9', value)
    if (!value) {
        removeMaintenancePermissions()
        syncGlobalRoleFromPermissions()
    }
}

function userHasSystem(sysId: string): boolean {
    return editingUser.value?.accessibleSystems.includes(sysId) ?? false
}

function adminHasSystem(sysId: string): boolean {
    return editingUser.value?.adminSystems.includes(sysId) ?? false
}

function onUserSystemToggle(sysId: string, value: boolean): void {
    if (!editingUser.value) return
    const acc = editingUser.value.accessibleSystems
    const adm = editingUser.value.adminSystems
    if (value) {
        if (!acc.includes(sysId)) acc.push(sysId)
    } else {
        const i = acc.indexOf(sysId)
        if (i >= 0) acc.splice(i, 1)
        const j = adm.indexOf(sysId)
        if (j >= 0) adm.splice(j, 1)

        if (sysId === 'system9') {
            removeMaintenancePermissions()
        }
    }

    syncGlobalRoleFromPermissions()
}

function onAdminSystemToggle(sysId: string, value: boolean): void {
    if (!editingUser.value) return
    const acc = editingUser.value.accessibleSystems
    const adm = editingUser.value.adminSystems
    if (value) {
        if (!adm.includes(sysId)) adm.push(sysId)
        if (!acc.includes(sysId)) acc.push(sysId)
    } else {
        const j = adm.indexOf(sysId)
        if (j >= 0) adm.splice(j, 1)
    }

    syncGlobalRoleFromPermissions()
}

const roleDescription = computed(() => {
    if (!editingUser.value) return '-'
    const role = editingUser.value.role
    if (role === 'SuperAdmin') return 'ผู้ดูแลระบบสูงสุด (คำนวณจากสิทธิ์ Admin Tool)'
    if (role === 'Officer') return 'เจ้าหน้าที่ (คำนวณจากสิทธิ์บันทึกงานรายระบบ)'
    return 'ผู้ใช้งานทั่วไป'
})

const fetchData = async () => {
    isLoading.value = true
    try {
        const [usersRes, deptsRes] = await Promise.all([
            api.get('/User'),
            api.get('/Department/all'),
        ])

        departments.value = deptsRes.data
        const found = (usersRes.data as AppUser[]).find((u) => u.id === userId.value) || null

        if (!found) {
            errorMessage.value = 'ไม่พบผู้ใช้งานที่ต้องการแก้ไข'
            return
        }

        editingUser.value = {
            ...found,
            adminSystems: [...(found.adminSystems || [])],
            accessibleSystems: [...(found.accessibleSystems || [])],
        }
        normalizeLegacyMaintenancePermissions()
        syncGlobalRoleFromPermissions()
    } catch (error) {
        toast.fromError(error, 'โหลดข้อมูลผู้ใช้งานไม่สำเร็จ')
    } finally {
        isLoading.value = false
    }
}

onMounted(() => {
    fetchData()
})

const goBack = () => {
    router.push('/admin/users')
}

const saveUser = async () => {
    if (!editingUser.value) return
    try {
        isSaving.value = true
        errorMessage.value = ''

        await api.put(`/User/${editingUser.value.id}`, {
            displayName: editingUser.value.displayName,
            departmentId: editingUser.value.departmentId,
            role: editingUser.value.role,
            status: editingUser.value.status,
            adminSystems: editingUser.value.adminSystems,
            accessibleSystems: editingUser.value.accessibleSystems,
        })

        const actor = {
            uid: authStore.user?.id ?? '',
            displayName: authStore.user?.firstName ?? authStore.user?.email ?? '',
            email: authStore.user?.email ?? '',
            role: authStore.user?.role ?? 'User',
        }

        logAudit(actor, 'UPDATE', 'UserManagement', `จัดการผู้ใช้: ${editingUser.value.email}`)

        successMessage.value = 'บันทึกสำเร็จ'
        await fetchData()
    } catch (error: unknown) {
        if (axios.isAxiosError(error)) {
            errorMessage.value = error.response?.data?.message || 'เกิดข้อผิดพลาดในการบันทึก'
        } else {
            errorMessage.value = 'เกิดข้อผิดพลาด'
        }
    } finally {
        isSaving.value = false
    }
}
</script>

<template>
    <div class="max-w-7xl mx-auto pb-10 space-y-5">

        <!-- Header -->
        <div class="flex items-center justify-between gap-3">
            <div>
                <h2 class="text-2xl font-bold text-gray-800">จัดการสิทธิ์ผู้ใช้งาน</h2>
                <p v-if="editingUser" class="text-sm text-gray-500 mt-0.5">
                    {{ editingUser.displayName || editingUser.email }}
                </p>
            </div>
            <Button label="กลับ" icon="pi pi-arrow-left" outlined size="small" @click="goBack" />
        </div>

        <!-- Loading -->
        <div v-if="isLoading" class="bg-white rounded-xl border border-gray-100 p-8 text-center text-sm text-gray-400">
            <i class="pi pi-spin pi-spinner text-xl mb-2 block"></i>
            กำลังโหลดข้อมูล...
        </div>

        <!-- Notifications -->
        <Message v-if="successMessage" severity="success" :closable="false">{{ successMessage }}</Message>
        <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>

        <!-- Main content -->
        <div v-if="editingUser" class="bg-white rounded-2xl border border-gray-200 shadow-sm overflow-hidden">
            <Tabs value="info">
                <TabList class="border-b border-gray-200 px-4">
                    <Tab value="info">
                        <span class="flex items-center gap-2 py-1">
                            <i class="pi pi-user text-sm"></i>
                            ข้อมูลผู้ใช้งาน
                        </span>
                    </Tab>
                    <Tab value="permissions">
                        <span class="flex items-center gap-2 py-1">
                            <i class="pi pi-shield text-sm"></i>
                            จัดการสิทธิ์
                        </span>
                    </Tab>
                </TabList>

                <TabPanels>
                    <!-- Tab 1: User Info -->
                    <TabPanel value="info">
                        <div class="p-5 lg:p-6 space-y-5">
                            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                                <div class="space-y-1.5">
                                    <label class="text-xs font-semibold text-gray-500 uppercase tracking-wide">อีเมล</label>
                                    <InputText :value="editingUser.email" disabled class="w-full bg-gray-50 text-gray-500" />
                                </div>
                                <div class="space-y-1.5">
                                    <label class="text-xs font-semibold text-gray-500 uppercase tracking-wide">ชื่อแสดงผล</label>
                                    <InputText v-model="editingUser.displayName" class="w-full" />
                                </div>
                            </div>

                            <div class="space-y-1.5">
                                <label class="text-xs font-semibold text-gray-500 uppercase tracking-wide">หน่วยงาน</label>
                                <Select v-model="editingUser.departmentId" :options="departments" optionLabel="name"
                                    optionValue="id" placeholder="เลือกหน่วยงาน" class="w-full" />
                            </div>

                            <div class="space-y-2">
                                <label class="text-xs font-semibold text-gray-500 uppercase tracking-wide flex items-center gap-1.5">
                                    <i class="pi pi-circle-fill text-[8px]"></i> สถานะบัญชี
                                </label>
                                <div class="flex flex-wrap gap-2">
                                    <button v-for="s in statuses" :key="s.id"
                                        @click="editingUser.status = s.id as AppUser['status']"
                                        class="flex items-center gap-2 px-4 py-2 rounded-lg border-2 text-sm font-medium transition-all"
                                        :class="{
                                            'border-amber-400 bg-amber-50 text-amber-700': editingUser.status === s.id && s.id === 'pending',
                                            'border-green-400 bg-green-50 text-green-700': editingUser.status === s.id && s.id === 'active',
                                            'border-red-400 bg-red-50 text-red-700': editingUser.status === s.id && s.id === 'suspended',
                                            'border-gray-200 bg-white text-gray-400 hover:border-gray-300': editingUser.status !== s.id,
                                        }">
                                        <i class="pi"
                                            :class="{ 'pi-clock': s.id === 'pending', 'pi-check-circle': s.id === 'active', 'pi-ban': s.id === 'suspended' }"></i>
                                        {{ s.name }}
                                        <i v-if="editingUser.status === s.id" class="pi pi-check text-xs"></i>
                                    </button>
                                </div>
                            </div>

                            <!-- Role summary -->
                            <div class="rounded-xl border border-gray-200 bg-gray-50 p-4 space-y-1">
                                <p class="text-[11px] font-semibold text-gray-500 uppercase tracking-wide">Role ที่ระบบคำนวณ</p>
                                <p class="text-base font-bold text-gray-800">{{ editingUser.role }}</p>
                                <p class="text-xs text-gray-500">{{ roleDescription }}</p>
                                <p v-if="hasAnyMaintenancePermission" class="text-xs text-orange-600 pt-1">
                                    <i class="pi pi-wrench mr-1"></i>
                                    สิทธิ์ซ่อมที่เปิด: {{ maintenancePermissionSummary.join(' · ') }}
                                </p>
                            </div>

                            <div class="flex justify-end pt-2 border-t border-gray-100">
                                <Button label="บันทึกข้อมูล" icon="pi pi-save" :loading="isSaving" @click="saveUser" />
                            </div>
                        </div>
                    </TabPanel>

                    <!-- Tab 2: Permissions -->
                    <TabPanel value="permissions">
                        <div class="p-5 lg:p-6 space-y-4">
                            <!-- Legend -->
                            <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500 bg-gray-50 border border-gray-200 rounded-lg px-4 py-2.5">
                                <span class="font-semibold text-gray-600">คำอธิบายสิทธิ์:</span>
                                <span>
                                    <span class="inline-block w-2.5 h-2.5 rounded-full bg-emerald-400 mr-1 align-middle"></span>
                                    <strong class="text-emerald-700">User</strong> — เข้าดูข้อมูลระบบนั้นได้
                                </span>
                                <span>
                                    <span class="inline-block w-2.5 h-2.5 rounded-full bg-amber-400 mr-1 align-middle"></span>
                                    <strong class="text-amber-700">Officer</strong> — บันทึก/จัดการข้อมูลระบบนั้น
                                </span>
                                <span class="text-gray-400">· เปิด Officer จะเปิด User อัตโนมัติ</span>
                            </div>

                            <!-- System permission cards -->
                            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                                <div v-for="sys in systemModules" :key="sys.id"
                                    class="rounded-xl border border-gray-200 bg-white shadow-sm flex flex-col border-l-4 transition-shadow hover:shadow-md overflow-hidden"
                                    :class="[sys.cardBorder]">

                                    <!-- Card header -->
                                    <div class="flex items-center gap-3 px-4 py-3 border-b border-gray-100">
                                        <div class="w-8 h-8 rounded-lg bg-gray-50 flex items-center justify-center shrink-0 border border-gray-100">
                                            <i :class="['pi text-base text-gray-500', sys.icon]"></i>
                                        </div>
                                        <div class="min-w-0">
                                            <p class="font-bold text-gray-800 text-sm leading-tight truncate">{{ sys.shortLabel }}</p>
                                            <p class="text-[11px] text-gray-400 leading-snug line-clamp-1">{{ sys.description }}</p>
                                        </div>
                                    </div>

                                    <!-- Toggles for regular systems -->
                                    <div v-if="sys.id !== 'system9'" class="px-4 py-3 space-y-2.5">
                                        <!-- system10: ไม่มี User toggle เพราะการเข้าใช้ต้องเป็น SuperAdmin เท่านั้น -->
                                        <div v-if="sys.id !== 'system10'" class="flex items-center justify-between gap-2">
                                            <div class="flex items-center gap-1.5">
                                                <span class="inline-block w-2 h-2 rounded-full bg-emerald-400"></span>
                                                <span class="text-xs font-medium text-gray-600">เข้าใช้งาน (User)</span>
                                            </div>
                                            <ToggleSwitch :modelValue="userHasSystem(sys.id)"
                                                @update:modelValue="(v) => onUserSystemToggle(sys.id, v)" />
                                        </div>
                                        <div class="flex items-center justify-between gap-2">
                                            <div class="flex items-center gap-1.5">
                                                <span class="inline-block w-2 h-2 rounded-full bg-amber-400"></span>
                                                <span class="text-xs font-medium text-gray-600">
                                                    {{ sys.id === 'system10' ? 'ผู้ดูแล (SuperAdmin)' : 'เจ้าหน้าที่ (Officer)' }}
                                                </span>
                                            </div>
                                            <ToggleSwitch :modelValue="adminHasSystem(sys.id)"
                                                @update:modelValue="(v) => onAdminSystemToggle(sys.id, v)" />
                                        </div>
                                        <p v-if="sys.id === 'system10'" class="text-[11px] text-red-500 bg-red-50 rounded px-2 py-1">
                                            <i class="pi pi-exclamation-triangle mr-1"></i>
                                            เปิด = ยกระดับเป็น SuperAdmin ทันที
                                        </p>
                                    </div>

                                    <!-- Special toggles for system9 (Maintenance) -->
                                    <div v-else class="px-4 py-3 space-y-2.5">
                                        <div class="flex items-center justify-between gap-2">
                                            <div class="flex items-center gap-1.5">
                                                <span class="inline-block w-2 h-2 rounded-full bg-emerald-400"></span>
                                                <span class="text-xs font-medium text-gray-600">เข้าใช้งานระบบซ่อม</span>
                                            </div>
                                            <ToggleSwitch :modelValue="userHasSystem('system9')"
                                                @update:modelValue="onMaintenanceAccessToggle" />
                                        </div>
                                        <div class="border-t border-orange-100 pt-2 space-y-2">
                                            <p class="text-[11px] font-semibold text-orange-700">บทบาทงานซ่อม (เปิดได้หลายอย่าง)</p>
                                            <div v-for="mr in maintenanceRoles" :key="mr.id"
                                                class="flex items-center justify-between gap-2">
                                                <span class="text-xs text-gray-700">{{ mr.name }}</span>
                                                <ToggleSwitch :modelValue="isMaintenanceRoleActive(mr.id)"
                                                    @update:modelValue="(v) => onMaintenanceRoleToggle(mr.id, v)" />
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <div class="flex items-center justify-between pt-2 border-t border-gray-100">
                                <Button label="กลับ" icon="pi pi-arrow-left" text severity="secondary" @click="goBack" />
                                <Button label="บันทึกสิทธิ์" icon="pi pi-save" :loading="isSaving" @click="saveUser" />
                            </div>
                        </div>
                    </TabPanel>
                </TabPanels>
            </Tabs>
        </div>
    </div>
</template>