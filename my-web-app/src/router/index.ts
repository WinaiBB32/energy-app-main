// src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import {
  MAINTENANCE_ADMIN_BUILDING_PERMISSION,
  MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
  MAINTENANCE_SUPERVISOR_PERMISSION,
  MAINTENANCE_TECHNICIAN_PERMISSION,
} from '../config/maintenancePermissions'

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
      path: '/tv-display/:id',
      name: 'tv-display',
      component: () => import('../views/systems/tv-dashboard/TvDisplay.vue'),
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

        // --- ระบบ IP-Phone (สถิติการโทร) ---
        {
          path: '/ipphone/dashboard',
          name: 'ipphone-dashboard',
          component: () => import('../views/systems/ipphone/Dashboard.vue'),
          meta: { system: 'system6' },
        },
        {
          path: '/ipphone',
          redirect: '/ipphone/dashboard',
        },
        {
          path: '/ipphone/upload',
          name: 'ipphone-upload',
          component: () => import('../views/systems/ipphone/Upload.vue'),
          meta: { system: 'system6', requiresSuperAdmin: true },
        },
        // Legacy redirect: old detail path → new directory system
        {
          path: '/ipphone/directory/:id',
          redirect: (to) => `/directory/${to.params.id}`,
        },

        // --- สมุดโทรศัพท์องค์กร (Directory) ---
        {
          path: '/directory',
          name: 'directory-list',
          component: () => import('../views/systems/directory/Directory.vue'),
          meta: { system: 'system12' },
        },
        {
          path: '/directory/:id',
          name: 'directory-detail',
          component: () => import('../views/systems/directory/DirectoryDetail.vue'),
          meta: { requiresAuth: true },
        },
        // --- ระบบแจ้งซ่อมงานอาคาร ---
        {
          path: '/maintenance/dashboard',
          name: 'maintenance-dashboard',
          component: () => import('../views/systems/building-maintenance/Dashboard.vue'),
          meta: {
            system: 'system9',
            requiresAuth: true,
            allowedRoles: ['superadmin', 'adminbuilding'],
          },
        },
        {
          path: '/maintenance/service',
          name: 'maintenance-service',
          component: () => import('../views/systems/building-maintenance/ServiceRequest.vue'),
          meta: { system: 'system9', requiresAuth: true },
        },
        {
          path: '/maintenance/technician',
          name: 'maintenance-technician',
          component: () => import('../views/systems/building-maintenance/TechnicianWorkbench.vue'),
          meta: {
            system: 'system9',
            requiresAuth: true,
            allowedRoles: ['superadmin', 'technician'],
          },
        },
        {
          path: '/maintenance/supervisor-review',
          name: 'maintenance-supervisor-review',
          component: () =>
            import('../views/systems/building-maintenance/SupervisorReviewQueue.vue'),
          meta: {
            system: 'system9',
            requiresAuth: true,
            allowedRoles: ['superadmin', 'supervisor', 'admin'],
          },
        },
        {
          path: '/maintenance/external-procurement',
          name: 'maintenance-external-procurement',
          component: () =>
            import('../views/systems/building-maintenance/ExternalProcurementAndTimeline.vue'),
          meta: {
            system: 'system9',
            requiresAuth: true,
            allowedRoles: ['superadmin', 'adminbuilding', 'admin'],
          },
        },
        {
          path: '/maintenance/external-timeline',
          name: 'maintenance-external-timeline',
          component: () =>
            import('../views/systems/building-maintenance/ExternalProcurementAndTimeline.vue'),
          meta: {
            system: 'system9',
            requiresAuth: true,
            allowedRoles: ['superadmin', 'adminbuilding', 'admin'],
          },
        },
        {
          path: '/maintenance/spare-parts',
          name: 'maintenance-spare-parts',
          component: () => import('../views/systems/building-maintenance/SparePartInventory.vue'),
          meta: {
            system: 'system9',
            requiresAuth: true,
            allowedRoles: ['superadmin', 'technician', 'adminbuilding'],
          },
        },
        // (ลบ route external-timeline ออก)
        {
          path: '/maintenance/service/:id',
          name: 'maintenance-service-chat',
          component: () => import('../views/systems/building-maintenance/ServiceChat.vue'),
          meta: { requiresAuth: true },
        },
        // Legacy aliases for old links
        { path: '/ipphone/service', redirect: '/maintenance/service' },
        { path: '/ipphone/spare-parts', redirect: '/maintenance/spare-parts' },
        { path: '/ipphone/service/:id', redirect: '/maintenance/service/:id' },
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
          path: '/postal/outgoing',
          name: 'postal-outgoing',
          component: () => import('../views/systems/postal/PostalOutgoing.vue'),
          meta: { system: 'system7', requiresAdmin: true },
        },
        {
          path: '/postal/incoming',
          name: 'postal-incoming',
          component: () => import('../views/systems/postal/PostalIncoming.vue'),
          meta: { system: 'system7', requiresAdmin: true },
        },
        { path: '/postal', redirect: '/postal/outgoing' },

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
          meta: { system: 'system10', requiresSuperAdmin: true },
        },

        {
          path: '/vehicle',
          name: 'vehicle-management',
          component: () => import('../views/systems/office/VehicleManagement.vue'),
          meta: { system: 'system11' },
        },
        {
          path: '/vehicle/person/:faceScanId',
          name: 'vehicle-person-detail',
          component: () => import('../views/systems/office/VehiclePersonDetail.vue'),
          meta: { system: 'system11' },
        },
        {
          path: '/vehicle/departments',
          name: 'vehicle-departments',
          component: () => import('../views/systems/office/VehicleDepartmentManagement.vue'),
          meta: { system: 'system11' },
        },
        {
          path: '/vehicle/provinces',
          name: 'vehicle-provinces',
          component: () => import('../views/systems/office/VehicleProvinceManagement.vue'),
          meta: { system: 'system11' },
        },

        // --- TV Dashboard ---
        {
          path: '/tv-dashboard',
          name: 'tv-dashboard',
          component: () => import('../views/systems/tv-dashboard/TvManage.vue'),
          meta: { system: 'system13', requiresSuperAdmin: true },
        },
        {
          path: '/tv-dashboard/new',
          name: 'tv-dashboard-new',
          component: () => import('../views/systems/tv-dashboard/TvEdit.vue'),
          meta: { system: 'system13', requiresSuperAdmin: true },
        },
        {
          path: '/tv-dashboard/:id/edit',
          name: 'tv-dashboard-edit',
          component: () => import('../views/systems/tv-dashboard/TvEdit.vue'),
          meta: { system: 'system13', requiresSuperAdmin: true },
        },

        // --- จัดการหลังบ้าน (SuperAdmin) ---
        {
          path: '/admin/users',
          name: 'admin-users',
          component: () => import('../views/admin/UserManagement.vue'),
          meta: { system: 'system10', requiresSuperAdmin: true },
        },
        {
          path: '/admin/users/:id/permissions',
          name: 'admin-user-permissions',
          component: () => import('../views/admin/UserPermissionEdit.vue'),
          meta: { system: 'system10', requiresSuperAdmin: true },
        },
        {
          path: '/admin/system-management',
          name: 'admin-system-management',
          component: () => import('../views/admin/SystemManagementHub.vue'),
          meta: { system: 'system10', requiresSuperAdmin: true },
        },
        {
          path: '/admin/departments',
          name: 'admin-departments',
          component: () => import('../views/admin/DepartmentManagement.vue'),
          meta: { system: 'system10', requiresSuperAdmin: true },
        },
        {
          path: '/admin/buildings',
          name: 'admin-buildings',
          component: () => import('../views/admin/BuildingManagement.vue'),
          meta: { system: 'system10', requiresSuperAdmin: true },
        },
        {
          path: '/admin/fuel-types',
          name: 'admin-fuel-types',
          component: () => import('../views/admin/FuelTypeManagement.vue'),
          meta: { system: 'system10', requiresSuperAdmin: true },
        },
        {
          path: '/admin/audit',
          name: 'admin-audit',
          component: () => import('../views/admin/AuditLog.vue'),
          meta: { system: 'system10', requiresSuperAdmin: true },
        },
        {
          path: '/admin/quota',
          name: 'admin-quota',
          component: () => import('../views/admin/SystemQuota.vue'),
          meta: { system: 'system10', requiresSuperAdmin: true },
        },
      ],
    },
  ],
})

// 💂‍♂️ Navigation Guard
router.beforeEach(async (to, _from) => {
  // ...existing code...

  // (ย้าย redirect ไปไว้ในบล็อก if (authStore.isAuthenticated && authStore.user) ด้านล่างเท่านั้น)
  const authStore = useAuthStore()

  if (authStore.isAuthenticated) {
    await authStore.syncCurrentUser()
  }

  const routeSystem = [...to.matched]
    .reverse()
    .find((record) => typeof record.meta.system === 'string')?.meta.system as string | undefined

  // 1. อนุญาต iframe เข้า Dashboard ได้
  if (to.query.embed === 'true' && to.path.endsWith('/dashboard')) {
    return true // คืนค่า true คืออนุญาตให้ผ่าน
  }

  const requiresAuth = to.matched.some((record) => record.meta.requiresAuth)
  const requiresAdmin = to.matched.some((record) => record.meta.requiresAdmin)
  const requiresSuperAdmin = to.matched.some((record) => record.meta.requiresSuperAdmin)
  const allowedRoles = [...to.matched]
    .reverse()
    .find((record) => Array.isArray(record.meta.allowedRoles))?.meta.allowedRoles as
    | string[]
    | undefined

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
    const userRole = (authStore.user.role ?? '').trim().toLowerCase()
    // Redirect: ถ้า userRole เป็น 'user' และเข้า /maintenance/dashboard ให้ไป /maintenance/service (ต้องมาก่อนทุกเงื่อนไข)
    if (to.path === '/maintenance/dashboard' && userRole === 'user') {
      return '/maintenance/service'
    }
    const userStatus = (authStore.user.status ?? '').trim().toLowerCase()
    const accessibleSystems = authStore.userProfile?.accessibleSystems ?? []
    const adminSystems = authStore.userProfile?.adminSystems ?? []
    const isSuperAdmin = userRole === 'superadmin'
    const isAdmin = userRole === 'admin'
    const hasMaintenancePermission = (permission: string): boolean =>
      adminSystems.includes(permission)
    const hasRouteLevelMaintenancePermission = (): boolean => {
      if (to.path === '/maintenance/dashboard') {
        return hasMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION)
      }
      if (to.path === '/maintenance/technician') {
        return hasMaintenancePermission(MAINTENANCE_TECHNICIAN_PERMISSION)
      }
      if (to.path === '/maintenance/supervisor-review') {
        return hasMaintenancePermission(MAINTENANCE_SUPERVISOR_PERMISSION)
      }
      if (to.path === '/maintenance/external-procurement') {
        return (
          hasMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_PERMISSION) ||
          hasMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION)
        )
      }
      if (to.path === '/maintenance/external-timeline') {
        return hasMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION)
      }
      if (to.path === '/maintenance/spare-parts') {
        return (
          hasMaintenancePermission(MAINTENANCE_TECHNICIAN_PERMISSION) ||
          hasMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION)
        )
      }
      return false
    }

    // SuperAdmin ทะลวงด่าน
    if (isSuperAdmin) {
      if (to.path === '/pending') return '/'
      return true
    }

    // ผู้ใช้สถานะ pending ให้ไปหน้ารออนุมัติ
    if (userStatus === 'pending' && to.path !== '/pending') {
      return '/pending'
    }

    // เช็คสิทธิ์หน้า SuperAdmin
    if (requiresSuperAdmin && !isSuperAdmin) {
      return '/'
    }

    // เช็คสิทธิ์เข้าถึงระบบรายระบบ (แยกสิทธิ์ ไม่ใช้ร่วมกัน)
    if (routeSystem && !isSuperAdmin) {
      const canAccessSystem =
        accessibleSystems.includes(routeSystem) || adminSystems.includes(routeSystem)
      if (!canAccessSystem) {
        const canAccessMaintenanceByPermission =
          routeSystem === 'system9' && hasRouteLevelMaintenancePermission()
        if (canAccessMaintenanceByPermission) {
          return true
        }
        return '/'
      }
    }

    // เช็คสิทธิ์ admin รายระบบ
    if (requiresAdmin && !isSuperAdmin) {
      if (!routeSystem) {
        return '/'
      }
      if (!adminSystems.includes(routeSystem)) {
        return '/'
      }
    }

    // เช็คสิทธิ์ role เฉพาะหน้า
    if (allowedRoles && allowedRoles.length > 0 && !allowedRoles.includes(userRole)) {
      // กรณีระบบซ่อม: ถ้ามีสิทธิ์ adminSystems:maintenance:adminbuilding ให้เข้าได้
      if (routeSystem === 'system9' && adminSystems.includes('maintenance:adminbuilding')) {
        return true
      }
      if (!hasRouteLevelMaintenancePermission()) {
        return '/'
      }
    }

    // กรณี route ต้องมีสิทธิ์ login แต่ไม่ผูกระบบ: ผ่านตามปกติ
    if (requiresAdmin && !isAdmin && !isSuperAdmin && !routeSystem) {
      return '/'
    }
  }

  return true // ผ่านทุกเงื่อนไข
})

export default router
