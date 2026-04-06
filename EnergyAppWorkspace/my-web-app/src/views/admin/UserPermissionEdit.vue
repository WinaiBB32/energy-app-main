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

interface PermissionGuide {
    userCapabilities: string[]
    adminCapabilities: string[]
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

const permissionGuideBySystem: Record<string, PermissionGuide> = {
    system1: {
        userCapabilities: ['ดู Dashboard ค่าไฟและ Solar'],
        adminCapabilities: ['บันทึกบิลค่าไฟ', 'บันทึกพลังงาน Solar'],
    },
    system2: {
        userCapabilities: ['ดู Dashboard ค่าน้ำประปา'],
        adminCapabilities: ['บันทึกค่าน้ำประปา'],
    },
    system3: {
        userCapabilities: ['ดู Dashboard น้ำมันเชื้อเพลิง'],
        adminCapabilities: ['บันทึกเติมน้ำมัน', 'ประวัติและพิมพ์เอกสารน้ำมัน'],
    },
    system4: {
        userCapabilities: ['ดู Dashboard ค่าโทรศัพท์'],
        adminCapabilities: ['บันทึกค่าโทรศัพท์'],
    },
    system5: {
        userCapabilities: ['ดู Dashboard งานสารบรรณ'],
        adminCapabilities: ['บันทึกงานสารบรรณ'],
    },
    system6: {
        userCapabilities: ['ดู Dashboard IP-Phone', 'เข้าดู Directory และหน้า Support'],
        adminCapabilities: ['กำหนดสิทธิ์ผู้ใช้เฉพาะ SuperAdmin สำหรับ Upload/Mapping'],
    },
    system7: {
        userCapabilities: ['ดู Dashboard งานไปรษณีย์'],
        adminCapabilities: ['บันทึกไปรษณีย์เข้า/ออก'],
    },
    system8: {
        userCapabilities: ['ดู Dashboard ห้องประชุม'],
        adminCapabilities: ['บันทึกสถิติห้องประชุมรายเดือน'],
    },
    system9: {
        userCapabilities: ['ดู Dashboard ซ่อมอาคาร', 'สร้างและติดตามใบแจ้งซ่อม'],
        adminCapabilities: ['สิทธิ์เฉพาะบทบาท Technician/Supervisor/AdminBuilding ตามหน้า'],
    },
    system10: {
        userCapabilities: ['ไม่มีหน้าสำหรับ User/Admin ทั่วไป'],
        adminCapabilities: ['อนุญาตเฉพาะ SuperAdmin เท่านั้น'],
    },
}

const canEditPerSystemMatrix = computed(() => !!editingUser.value)

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

const getPermissionGuide = (systemId: string): PermissionGuide =>
    permissionGuideBySystem[systemId] ?? {
        userCapabilities: ['ดูข้อมูลในระบบ'],
        adminCapabilities: ['จัดการข้อมูลในระบบ'],
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
    <div class="max-w-7xl mx-auto pb-10 space-y-6">
        <div class="flex items-start justify-between gap-3">
            <div>
                <h2 class="text-3xl font-bold text-gray-800">หน้าแก้ไขสิทธิ์ผู้ใช้งาน</h2>
                <p class="text-gray-500 mt-1">กำหนดสถานะ บทบาท และสิทธิ์รายระบบแบบเต็มหน้า</p>
            </div>
            <Button label="กลับไปหน้า จัดการสิทธิ์ผู้ใช้งาน" icon="pi pi-arrow-left" outlined @click="goBack" />
        </div>

        <div v-if="isLoading" class="bg-white rounded-xl border border-gray-100 p-6 text-sm text-gray-500">
            กำลังโหลดข้อมูล...
        </div>

        <Message v-if="successMessage" severity="success" :closable="false">{{ successMessage }}</Message>
        <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>

        <section v-if="editingUser" class="bg-white rounded-2xl border border-gray-200 shadow-sm p-5 lg:p-6 space-y-5">
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
                    <Select v-model="editingUser.departmentId" :options="departments" optionLabel="name"
                        optionValue="id" placeholder="เลือกหน่วยงาน" class="w-full" />
                </div>
                <div class="space-y-1.5">
                    <label class="text-xs font-semibold text-blue-500 uppercase tracking-wide flex items-center gap-1">
                        <i class="pi pi-shield text-[10px]"></i> สถานะบัญชี
                    </label>
                    <div class="flex flex-col gap-1.5">
                        <button v-for="s in statuses" :key="s.id"
                            @click="editingUser.status = s.id as AppUser['status']"
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
                <div class="rounded-xl border border-gray-200 bg-gray-50 p-3">
                    <p class="text-xs font-semibold text-gray-600 uppercase tracking-wide">ระดับสิทธิ์ (Role) แบบรวม</p>
                    <p class="text-sm font-semibold text-gray-800 mt-1">{{ editingUser.role }}</p>
                    <p class="text-xs text-gray-500 mt-1">{{ roleDescription }}</p>
                    <p v-if="hasAnyMaintenancePermission" class="text-xs text-orange-700 mt-2">
                        สิทธิ์งานซ่อมที่เปิด: {{ maintenancePermissionSummary.join(' , ') }}
                    </p>
                </div>

                <div class="space-y-2">
                    <div class="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-2">
                        <div>
                            <label
                                class="text-xs font-semibold text-gray-500 uppercase tracking-wide">จัดการสิทธิ์ตามระบบ</label>
                            <p class="text-[11px] text-gray-400 mt-0.5">
                                เปิด <span class="font-medium text-emerald-700">User</span> = เข้า Portal/ระบบนั้นได้ ·
                                เปิด <span class="font-medium text-amber-700">เจ้าหน้าที่ (Officer)</span> =
                                บันทึกงานระบบนั้น
                            </p>
                        </div>
                    </div>

                    <div v-if="!canEditPerSystemMatrix"
                        class="rounded-xl border border-dashed border-gray-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
                        <i class="pi pi-lock text-slate-400 mr-2"></i> เลือกบทบาท <strong>User</strong>
                        เพื่อกำหนดสิทธิ์รายระบบ
                    </div>

                    <div v-else
                        class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 max-h-[min(52vh,480px)] overflow-y-auto pr-1">
                        <div v-for="sys in systemModules" :key="sys.id"
                            class="rounded-xl border border-gray-200 bg-white shadow-sm p-4 flex flex-col gap-3 border-l-4 transition-shadow hover:shadow-md"
                            :class="[sys.cardBorder]">
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
                                <div v-if="sys.id !== 'system9'" class="flex items-center justify-between gap-2">
                                    <span class="text-xs font-medium text-gray-600">เข้าใช้งาน (User)</span>
                                    <ToggleSwitch :modelValue="userHasSystem(sys.id)"
                                        @update:modelValue="(v) => onUserSystemToggle(sys.id, v)" />
                                </div>
                                <div v-if="sys.id !== 'system9'" class="flex items-center justify-between gap-2">
                                    <span class="text-xs font-medium text-gray-600">
                                        {{ sys.id === 'system10' ? 'ผู้ดูแล (Admin)' : 'เจ้าหน้าที่ (Officer)' }}
                                    </span>
                                    <ToggleSwitch :modelValue="adminHasSystem(sys.id)"
                                        @update:modelValue="(v) => onAdminSystemToggle(sys.id, v)" />
                                </div>
                                <div
                                    class="rounded-lg bg-gray-50 border border-gray-100 p-2.5 text-[11px] text-gray-600 leading-relaxed">
                                    <p class="font-semibold text-gray-700">สิทธิ์ที่ระบบนี้รองรับ</p>
                                    <p class="mt-1">User: {{ getPermissionGuide(sys.id).userCapabilities.join(' , ') }}
                                    </p>
                                    <p class="mt-1">Admin: {{ getPermissionGuide(sys.id).adminCapabilities.join(' , ')
                                    }}</p>
                                    <p v-if="sys.id === 'system10'" class="mt-1 text-red-600 font-semibold">
                                        หมายเหตุ: เปิด ผู้ดูแล(Admin) ของ Admin Tool = ตั้งเป็น SuperAdmin
                                    </p>
                                </div>

                                <div v-if="sys.id === 'system9'"
                                    class="rounded-lg border border-orange-200 bg-orange-50/60 p-2.5">
                                    <p class="text-[11px] font-semibold text-orange-800">สิทธิ์เฉพาะระบบซ่อมงานอาคาร</p>
                                    <div class="flex items-center justify-between mt-1.5 mb-1.5">
                                        <span class="text-[11px] text-orange-700">เปิดใช้งานระบบซ่อม</span>
                                        <ToggleSwitch :modelValue="userHasSystem('system9')"
                                            @update:modelValue="onMaintenanceAccessToggle" />
                                    </div>
                                    <p class="text-[11px] text-orange-700 mt-0.5">
                                        เปิดได้หลายสิทธิ์พร้อมกันตามงานที่รับผิดชอบ</p>
                                    <div class="space-y-1.5 mt-2">
                                        <div v-for="mr in maintenanceRoles" :key="mr.id"
                                            class="flex items-center justify-between gap-2">
                                            <span class="text-xs font-medium text-orange-900">{{ mr.name }}</span>
                                            <ToggleSwitch :modelValue="isMaintenanceRoleActive(mr.id)"
                                                @update:modelValue="(v) => onMaintenanceRoleToggle(mr.id, v)" />
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="flex items-center justify-between gap-2 pt-2 border-t border-gray-100">
                <Button label="กลับไปหน้า จัดการสิทธิ์ผู้ใช้งาน" icon="pi pi-arrow-left" text severity="secondary"
                    @click="goBack" />
                <Button label="บันทึกข้อมูล" icon="pi pi-save" :loading="isSaving" @click="saveUser" />
            </div>
        </section>
    </div>
</template>