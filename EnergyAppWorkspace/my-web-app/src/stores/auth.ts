import { defineStore } from 'pinia'
import api from '@/services/api'
import type { User, UserProfile, UserRole } from '@/types'
import axios from 'axios' // <--- Import axios เพื่อมาทำ Type Guard

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('jwt_token') || null,
    user: JSON.parse(localStorage.getItem('user_data') || 'null') as User | null,
    lastUserSyncAt: 0 as number,
    syncingUser: false as boolean,
    loading: false as boolean,
    error: null as string | null,
  }),

  getters: {
    isAuthenticated: (state): boolean => !!state.token,
    isAdmin: (state): boolean => state.user?.role === 'Admin' || state.user?.role === 'SuperAdmin',
    isSuperAdmin: (state): boolean => state.user?.role === 'SuperAdmin',
    userProfile: (state): UserProfile | null => {
      if (!state.user) return null
      const roleText = state.user.role.toLowerCase()
      const role: UserRole =
        roleText === 'superadmin' ? 'superadmin'
        : roleText === 'admin' ? 'admin'
        : roleText === 'officer' ? 'officer'
        : 'user'
      const normalizedStatus = (state.user.status ?? 'active').toLowerCase()
      const status =
        normalizedStatus === 'pending' || normalizedStatus === 'suspended'
          ? normalizedStatus
          : 'active'
      return {
        departmentId: state.user.departmentId ?? '',
        role,
        status,
        accessibleSystems: state.user.accessibleSystems ?? [],
        adminSystems: state.user.adminSystems ?? [],
        displayName: `${state.user.firstName} ${state.user.lastName}`,
        email: state.user.email,
      }
    },
  },

  actions: {
    async login(email: string, password: string): Promise<boolean> {
      this.loading = true
      this.error = null
      try {
        const response = await api.post('/Auth/login', { email, password })

        // Assert type ให้ชัดเจน
        this.token = response.data.token as string
        const u = response.data.user as User
        u.uid = u.id
        u.displayName = u.displayName || `${u.firstName} ${u.lastName}`.trim()
        this.user = u
        this.lastUserSyncAt = Date.now()

        localStorage.setItem('jwt_token', this.token)
        localStorage.setItem('user_data', JSON.stringify(this.user))

        return true
      } catch (err: unknown) {
        // <--- เปลี่ยนจาก any เป็น unknown

        // --- กระบวนการ Type Guard ---
        if (axios.isAxiosError(err)) {
          // กรณี Error มาจากการยิง API (Backend ตอบ 400, 401 ฯลฯ)
          this.error = err.response?.data?.message || 'อีเมลหรือรหัสผ่านไม่ถูกต้อง'
        } else if (err instanceof Error) {
          // กรณี Error ทั่วไปของ JavaScript
          this.error = err.message
        } else {
          // กรณี Error ประหลาดอื่นๆ
          this.error = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ'
        }

        throw err
      } finally {
        this.loading = false
      }
    },

    async syncCurrentUser(ttlMs = 30000): Promise<boolean> {
      if (!this.token) return false
      if (this.syncingUser) return true
      if (this.lastUserSyncAt > 0 && Date.now() - this.lastUserSyncAt < ttlMs) return true

      this.syncingUser = true
      try {
        const response = await api.get('/Auth/me')
        const u = response.data.user as User
        u.uid = u.id
        u.displayName = u.displayName || `${u.firstName} ${u.lastName}`.trim()

        this.user = u
        this.lastUserSyncAt = Date.now()
        localStorage.setItem('user_data', JSON.stringify(this.user))
        return true
      } catch (err: unknown) {
        if (axios.isAxiosError(err) && err.response?.status === 401) {
          this.logout()
        }
        return false
      } finally {
        this.syncingUser = false
      }
    },

    logout(): void {
      this.token = null
      this.user = null
      this.lastUserSyncAt = 0
      this.syncingUser = false
      localStorage.removeItem('jwt_token')
      localStorage.removeItem('user_data')
    },
  },
})
