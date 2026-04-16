import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth'

/** คีย์สั้นในโค้ด (saraban, ipphone, …) → รหัสใน Database / UserManagement (system5, …) */
const MODULE_TO_SYSTEM_ID: Record<string, string> = {
  electricity: 'system1',
  water: 'system2',
  fuel: 'system3',
  telephone: 'system4',
  saraban: 'system5',
  ipphone: 'system6',
  maintenance: 'system9',
  admintool: 'system10',
  postal: 'system7',
  meeting: 'system8',
}

export function usePermissions() {
  const authStore = useAuthStore()
  const currentUserRole = computed(() => authStore.userProfile?.role)
  const adminSystems = computed(() => authStore.userProfile?.adminSystems ?? [])

  const isSuperAdmin = computed(() => currentUserRole.value === 'superadmin')
  const isOfficer = computed(() => currentUserRole.value === 'officer')

  function isSystemAdmin(system: string): boolean {
    if (isSuperAdmin.value) return true
    const systemId = MODULE_TO_SYSTEM_ID[system] ?? system
    const sys = adminSystems.value
    return sys.includes(systemId) || sys.includes(system)
  }

  return {
    isSuperAdmin,
    isOfficer,
    isSystemAdmin,
    currentUserRole,
    adminSystems,
  }
}
