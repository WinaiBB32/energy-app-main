// src/composables/useAuth.ts
// Composable wrapping the auth store for convenient use in components

import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import type { UserRole, UserStatus } from '@/types'

export function useAuth() {
  const authStore = useAuthStore()

  const user = computed(() => authStore.user)
  const userProfile = computed(() => authStore.userProfile)
  const loading = computed(() => authStore.loading)

  const isLoggedIn = computed(() => !!authStore.user)

  const role = computed<UserRole | null>(() => authStore.userProfile?.role ?? null)
  const status = computed<UserStatus | null>(() => authStore.userProfile?.status ?? null)

  const isUser = computed(() => role.value === 'user')
  const isSuperAdmin = computed(() => role.value === 'superadmin')

  const isActive = computed(() => status.value === 'active')
  const isPending = computed(() => status.value === 'pending')
  const isSuspended = computed(() => status.value === 'suspended')

  const displayName = computed(
    () =>
      authStore.userProfile?.displayName ??
      authStore.user?.displayName ??
      authStore.user?.email ??
      '',
  )

  const email = computed(() => authStore.user?.email ?? '')
  const uid = computed(() => authStore.user?.uid ?? '')

  const accessibleSystems = computed(
    () => authStore.userProfile?.accessibleSystems ?? [],
  )

  const adminSystems = computed(
    () => authStore.userProfile?.adminSystems ?? [],
  )

  function hasSystemAccess(system: string): boolean {
    if (role.value === 'superadmin') return true
    if (role.value === 'admin') return true
    const adm = authStore.userProfile?.adminSystems ?? []
    return accessibleSystems.value.includes(system) || adm.includes(system)
  }

  const logout = () => authStore.logout()

  return {
    // Raw refs
    user,
    userProfile,
    loading,
    // Convenience
    isLoggedIn,
    role,
    status,
    isUser,
    isSuperAdmin,
    isActive,
    isPending,
    isSuspended,
    displayName,
    email,
    uid,
    accessibleSystems,
    adminSystems,
    hasSystemAccess,
    logout,
  }
}
