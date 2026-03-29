// src/router/index.ts
import { createRouter, createWebHistory, type RouteRecordNormalized } from 'vue-router'
import { useAuthStore } from '../stores/auth'

/** meta.system จาก route ลูกสุดท้ายที่มีค่า (รองรับ nested routes) */
function getMatchedSystemMeta(matched: RouteRecordNormalized[]): string | undefined {
  for (let i = matched.length - 1; i >= 0; i--) {
    const m = matched[i]
    if (!m) continue
    const s = m.meta?.system
    if (typeof s === 'string' && s.length > 0) return s
  }
  return undefined
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/login', name: 'login', component: () => import('../views/auth/LoginView.vue') },
    {
      path: '/pending',
      name: 'pending',
      component: () => import('../views/auth/PendingApproval.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/profile',
      name: 'profile',
      component: () => import('../views/ProfileView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/support',
      name: 'user-support',
      component: () => import('../views/UserSupportView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/',
      name: 'portal',
      component: () => import('../views/PortalView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/system',
      component: () => import('../components/layout/AppLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        // --- ระบบไฟฟ้า ---
        {
          path: '/electricity/dashboard',
          name: 'electricity-dashboard',
          component: () => import('../views/systems/electricity/Dashboard.vue'),
          meta: { system: 'system1' },
        },
        {
          path: '/electricity',
          name: 'electricity-bill',
          component: () => import('../views/systems/electricity/Electricity.vue'),
          meta: { system: 'system1', requiresAdmin: true },
        },
        {
          path: '/electricity/solar',
          name: 'electricity-solar',
          component: () => import('../views/systems/electricity/Solar.vue'),
          meta: { system: 'system1', requiresAdmin: true },
        },

        // --- ระบบน้ำ ---
        {
          path: '/water/dashboard',
          name: 'water-dashboard',
          component: () => import('../views/systems/water/Dashboard.vue'),
          meta: { system: 'system2' },
        },
        {
          path: '/water',
          name: 'water-bill',
          component: () => import('../views/systems/water/Water.vue'),
          meta: { system: 'system2', requiresAdmin: true },
        },

        // --- ระบบน้ำมันเชื้อเพลิง ---
        {
          path: '/fuel/dashboard',
          name: 'fuel-dashboard',
          component: () => import('../views/systems/fuel/Dashboard.vue'),
          meta: { system: 'system3' },
        },
        {
          path: '/fuel',
          name: 'fuel-record',
          component: () => import('../views/systems/fuel/Fuel.vue'),
          meta: { system: 'system3', requiresAdmin: true },
        },
        {
          path: '/fuel/history',
          name: 'fuel-history',
          component: () => import('../views/systems/fuel/FuelHistory.vue'),
          meta: { system: 'system3', requiresAdmin: true },
        },
        {
          path: '/fuel/print',
          name: 'fuel-print',
          component: () => import('../views/systems/fuel/FuelPrint.vue'),
          meta: { system: 'system3', requiresAdmin: true },
        },
        {
          path: '/fuel/receipt',
          name: 'fuel-receipt',
          component: () => import('../views/systems/fuel/FuelReceipt.vue'),
          meta: { system: 'system3', requiresAdmin: true },
        },
        {
          path: '/fuel/receipt-history',
          name: 'fuel-receipt-history',
          component: () => import('../views/systems/fuel/FuelReceiptHistory.vue'),
          meta: { system: 'system3', requiresAdmin: true },
        },
        {
          path: '/fuel/receipt-print',
          name: 'fuel-receipt-print',
          component: () => import('../views/systems/fuel/FuelReceiptPrint.vue'),
          meta: { system: 'system3', requiresAdmin: true },
        },

        // --- ระบบค่าโทรศัพท์ ---
        {
          path: '/telephone/dashboard',
          name: 'telephone-dashboard',
          component: () => import('../views/systems/telephone/Dashboard.vue'),
          meta: { system: 'system4' },
        },
        {
          path: '/telephone',
          name: 'telephone-bill',
          component: () => import('../views/systems/telephone/Telephone.vue'),
          meta: { system: 'system4', requiresAdmin: true },
        },

        // --- งานสารบรรณ ---
        {
          path: '/saraban/dashboard',
          name: 'saraban-dashboard',
          component: () => import('../views/systems/saraban/Dashboard.vue'),
          meta: { system: 'system5' },
        },
        {
          path: '/saraban',
          name: 'saraban-record',
          component: () => import('../views/systems/saraban/Saraban.vue'),
          meta: { system: 'system5', requiresAdmin: true },
        },

        // --- ระบบ IP-Phone ---
        {
          path: '/ipphone/dashboard',
          name: 'ipphone-dashboard',
          component: () => import('../views/systems/ipphone/Dashboard.vue'),
          meta: { system: 'system6' },
        },
        {
          path: '/ipphone',
          name: 'ipphone-directory',
          component: () => import('../views/systems/ipphone/IPPhone.vue'),
          meta: { system: 'system6' },
        },
        {
          path: '/ipphone/upload',
          name: 'ipphone-upload',
          component: () => import('../views/systems/ipphone/Upload.vue'),
          meta: { system: 'system6', requiresSuperAdmin: true },
        },
        {
          path: '/ipphone/directory/:id',
          name: 'ipphone-detail',
          component: () => import('../views/systems/ipphone/IPPhoneDetail.vue'),
          meta: { requiresAuth: true },
        },
        {
          path: '/ipphone/service',
          name: 'ipphone-service',
          component: () => import('../views/systems/ipphone/ServiceRequest.vue'),
          meta: { system: 'system6', requiresSuperAdmin: true },
        },
        {
          path: '/ipphone/service/:id',
          name: 'ipphone-service-chat',
          component: () => import('../views/systems/ipphone/ServiceChat.vue'),
          meta: { requiresAuth: true },
        },
        {
          path: '/ipphone/mapping',
          name: 'ipphone-mapping',
          component: () => import('../views/systems/ipphone/UserMapping.vue'),
          meta: { system: 'system6', requiresSuperAdmin: true },
        },

        // --- งานไปรษณีย์ ---
        {
          path: '/postal/dashboard',
          name: 'postal-dashboard',
          component: () => import('../views/systems/postal/Dashboard.vue'),
          meta: { system: 'system7' },
        },
        {
          path: '/postal',
          name: 'postal-record',
          component: () => import('../views/systems/postal/Postal.vue'),
          meta: { system: 'system7', requiresAdmin: true },
        },

        // --- ห้องประชุม ---
        {
          path: '/meeting/dashboard',
          name: 'meeting-dashboard',
          component: () => import('../views/systems/meeting/Dashboard.vue'),
          meta: { system: 'system8' },
        },
        {
          path: '/meeting',
          name: 'meeting-record',
          component: () => import('../views/systems/meeting/MeetingRecord.vue'),
          meta: { system: 'system8', requiresAdmin: true },
        },
        {
          path: '/admin/meeting-rooms',
          name: 'admin-meeting-rooms',
          component: () => import('../views/admin/MeetingRoomManagement.vue'),
          meta: { requiresSuperAdmin: true },
        },

        // --- จัดการหลังบ้าน (SuperAdmin) ---
        {
          path: '/admin/users',
          name: 'admin-users',
          component: () => import('../views/admin/UserManagement.vue'),
          meta: { requiresSuperAdmin: true },
        },
        {
          path: '/admin/departments',
          name: 'admin-departments',
          component: () => import('../views/admin/DepartmentManagement.vue'),
          meta: { requiresSuperAdmin: true },
        },
        {
          path: '/admin/buildings',
          name: 'admin-buildings',
          component: () => import('../views/admin/BuildingManagement.vue'),
          meta: { requiresSuperAdmin: true },
        },
        {
          path: '/admin/fuel-types',
          name: 'admin-fuel-types',
          component: () => import('../views/admin/FuelTypeManagement.vue'),
          meta: { requiresSuperAdmin: true },
        },
        {
          path: '/admin/audit',
          name: 'admin-audit',
          component: () => import('../views/admin/AuditLog.vue'),
          meta: { requiresSuperAdmin: true },
        },
        {
          path: '/admin/quota',
          name: 'admin-quota',
          component: () => import('../views/admin/SystemQuota.vue'),
          meta: { requiresSuperAdmin: true },
        },
      ],
    },
  ],
})

// 💂‍♂️ Navigation Guard (อัปเดตใหม่สำหรับ .NET JWT)
// 💂‍♂️ Navigation Guard (อัปเดตใหม่ ไร้รอยต่อ ไม่มี next())
router.beforeEach(async (to, from) => {
  const authStore = useAuthStore()

  // 1. อนุญาต iframe เข้า Dashboard ได้
  if (to.query.embed === 'true' && to.path.endsWith('/dashboard')) {
    return true // คืนค่า true คืออนุญาตให้ผ่าน
  }

  const requiresAuth = to.matched.some((record) => record.meta.requiresAuth)
  const requiresAdmin = to.matched.some((record) => record.meta.requiresAdmin)
  const requiresSuperAdmin = to.matched.some((record) => record.meta.requiresSuperAdmin)
  // const routeSystem = getMatchedSystemMeta(to.matched) // ปิดไว้ก่อนถ้ายังไม่ได้ใช้

  // 2. ไม่ได้ล็อกอิน -> ไปหน้า Login
  if (requiresAuth && !authStore.isAuthenticated) {
    return '/login'
  }

  // 3. ล็อกอินแล้ว -> ห้ามเข้าหน้า Login ซ้ำ
  if (to.path === '/login' && authStore.isAuthenticated) {
    return '/'
  }

  // 4. จัดการสิทธิ์ (Role-based Access)
  if (authStore.isAuthenticated && authStore.user) {
    const userRole = authStore.user.role

    // SuperAdmin ทะลวงด่าน
    if (userRole === 'SuperAdmin') {
      if (to.path === '/pending') return '/'
      return true
    }

    // User ธรรมดา รออนุมัติ
    if (userRole === 'User' && to.path !== '/pending') {
      return '/pending'
    }

    // เช็คสิทธิ์หน้า SuperAdmin
    if (requiresSuperAdmin && userRole !== 'SuperAdmin') {
      return '/'
    }

    // เช็คสิทธิ์หน้า Admin
    if (requiresAdmin && userRole !== 'Admin' && userRole !== 'SuperAdmin') {
      return '/'
    }
  }

  return true // ผ่านทุกเงื่อนไข
})

export default router
