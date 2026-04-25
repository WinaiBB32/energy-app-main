<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import api from '@/services/api'
import { onRealtimeEvent, offRealtimeEvent, startRealtimeConnection } from '@/services/realtime'
import { useAuthStore } from '@/stores/auth'
import { useAuth } from '@/composables/useAuth'
import { usePermissions } from '@/composables/usePermissions'
import {
  MAINTENANCE_ADMIN_BUILDING_PERMISSION,
  MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
  MAINTENANCE_SUPERVISOR_PERMISSION,
  MAINTENANCE_TECHNICIAN_PERMISSION,
} from '@/config/maintenancePermissions'
import Button from 'primevue/button'
// (ลบเมนู Timeline ช่างภายนอก ออก)
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const { logout } = useAuth()
const { isSystemAdmin, isSuperAdmin } = usePermissions()
const roleText = computed(() => (authStore.user?.role ?? '').trim().toLowerCase())
const hasMaintenancePermission = (permission: string): boolean =>
  (authStore.userProfile?.adminSystems ?? []).includes(permission)

const canOpenMaintenanceTechnician = computed(
  () =>
    isSuperAdmin.value ||
    roleText.value === 'technician' ||
    hasMaintenancePermission(MAINTENANCE_TECHNICIAN_PERMISSION),
)
const canOpenMaintenanceSupervisor = computed(
  () =>
    isSuperAdmin.value ||
    roleText.value === 'supervisor' ||
    roleText.value === 'admin' ||
    hasMaintenancePermission(MAINTENANCE_SUPERVISOR_PERMISSION),
)
const canOpenMaintenanceExternal = computed(
  () =>
    isSuperAdmin.value ||
    roleText.value === 'adminbuilding' ||
    roleText.value === 'admin' ||
    hasMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_PERMISSION) ||
    hasMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION),
)
const canOpenMaintenanceSpareParts = computed(
  () =>
    isSuperAdmin.value ||
    roleText.value === 'technician' ||
    roleText.value === 'adminbuilding' ||
    hasMaintenancePermission(MAINTENANCE_TECHNICIAN_PERMISSION) ||
    hasMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION),
)
const canOpenMaintenanceDashboard = computed(
  () =>
    isSuperAdmin.value ||
    roleText.value === 'adminbuilding' ||
    hasMaintenancePermission(MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION),
)

const isEmbed = computed(() => route.query.embed === 'true')

interface MaintenanceNotificationItem {
  requestId: string
  workOrderNo: string
  title: string
  type: 'chat' | 'timeline' | string
  message: string
  status: string
  isUnread: boolean
  createdAt: string
  path: string
}

interface MaintenanceNotificationSummary {
  unreadChatCount: number
  timelineUpdateCount: number
  total: number
  items: MaintenanceNotificationItem[]
}

// ─── Sidebar state ────────────────────────────────────────────────────────────
const sidebarOpen = ref(true)
const isMobile = ref(false)
const notificationOpen = ref(false)
const notificationLoading = ref(false)
const maintenanceNotification = ref<MaintenanceNotificationSummary>({
  unreadChatCount: 0,
  timelineUpdateCount: 0,
  total: 0,
  items: [],
})

function checkMobile() {
  isMobile.value = window.innerWidth < 1024
  sidebarOpen.value = !isMobile.value
}

onMounted(() => {
  checkMobile()
  window.addEventListener('resize', checkMobile)

  fetchMaintenanceNotifications()

  if (authStore.isAuthenticated) {
    startRealtimeConnection()
      .then(() => {
        onRealtimeEvent('MaintenanceNotification', handleRealtimeNotification)
      })
      .catch(() => {
        // ถ้าเชื่อม realtime ไม่สำเร็จ จะยังคงเห็น snapshot ล่าสุดจาก API
      })
  }
})
onUnmounted(() => {
  window.removeEventListener('resize', checkMobile)
  offRealtimeEvent('MaintenanceNotification', handleRealtimeNotification)
})

const toggleSidebar = () => (sidebarOpen.value = !sidebarOpen.value)

// ─── System info ──────────────────────────────────────────────────────────────
const currentSystemName = computed(() => {
  if (route.path.startsWith('/electricity')) return 'ค่าไฟฟ้า & Solar'
  if (route.path.startsWith('/water')) return 'ค่าน้ำประปา'
  if (route.path.startsWith('/fuel')) return 'น้ำมันเชื้อเพลิง'
  if (route.path.startsWith('/telephone')) return 'ค่าโทรศัพท์'
  if (route.path.startsWith('/saraban')) return 'งานสารบรรณ'
  if (route.path.startsWith('/ipphone')) return 'IP-Phone'
  if (route.path.startsWith('/directory')) return 'สมุดโทรศัพท์องค์กร'
  if (route.path.startsWith('/vehicle')) return 'ระบบรถยนต์สำนักงาน'
  if (route.path.startsWith('/maintenance')) return 'แจ้งซ่อมงานอาคาร'
  if (route.path.startsWith('/admin')) return 'ผู้ดูแลระบบ'
  return 'E-Portal'
})

const systemIcon = computed(() => {
  if (route.path.startsWith('/electricity')) return 'pi-bolt'
  if (route.path.startsWith('/water')) return 'pi-gauge'
  if (route.path.startsWith('/fuel')) return 'pi-car'
  if (route.path.startsWith('/telephone')) return 'pi-phone'
  if (route.path.startsWith('/saraban')) return 'pi-folder-open'
  if (route.path.startsWith('/ipphone')) return 'pi-desktop'
  if (route.path.startsWith('/directory')) return 'pi-address-book'
  if (route.path.startsWith('/vehicle')) return 'pi-car'
  if (route.path.startsWith('/maintenance')) return 'pi-wrench'
  if (route.path.startsWith('/admin')) return 'pi-shield'
  return 'pi-home'
})

const sidebarMenus = computed(() => {
  if (route.path.startsWith('/electricity')) {
    const menus = [{ name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/electricity/dashboard' }]
    if (isSystemAdmin('electricity')) {
      menus.push({ name: 'บันทึกบิลค่าไฟ', icon: 'pi pi-file-edit', path: '/electricity' })
      menus.push({ name: 'บันทึกพลังงาน Solar', icon: 'pi pi-sun', path: '/electricity/solar' })
    }
    return menus
  }
  if (route.path.startsWith('/water')) {
    const menus = [{ name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/water/dashboard' }]
    if (isSystemAdmin('water')) {
      menus.push({ name: 'บันทึกค่าน้ำประปา', icon: 'pi pi-file-edit', path: '/water' })
    }
    return menus
  }
  if (route.path.startsWith('/fuel')) {
    const menus = [{ name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/fuel/dashboard' }]
    if (isSystemAdmin('fuel')) {
      menus.push({ name: 'บันทึกการเติมน้ำมัน', icon: 'pi pi-plus', path: '/fuel' })
      menus.push({ name: 'ประวัติการเติมน้ำมัน', icon: 'pi pi-history', path: '/fuel/history' })
      menus.push({ name: 'พิมพ์หน้างบใบสำคัญฯ', icon: 'pi pi-print', path: '/fuel/print' })
      menus.push({ name: 'บันทึกใบรับรองแทนฯ', icon: 'pi pi-file-edit', path: '/fuel/receipt' })
      menus.push({
        name: 'ประวัติใบรับรองแทนฯ',
        icon: 'pi pi-history',
        path: '/fuel/receipt-history',
      })
      menus.push({ name: 'พิมพ์ใบรับรองแทนฯ', icon: 'pi pi-print', path: '/fuel/receipt-print' })
    }
    return menus
  }
  if (route.path.startsWith('/telephone')) {
    const menus = [{ name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/telephone/dashboard' }]
    if (isSystemAdmin('telephone')) {
      menus.push({ name: 'บันทึกค่าโทรศัพท์', icon: 'pi pi-phone', path: '/telephone' })
    }
    return menus
  }
  if (route.path.startsWith('/saraban')) {
    const menus = [{ name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/saraban/dashboard' }]
    if (isSystemAdmin('saraban')) {
      menus.push({ name: 'บันทึกงานสารบรรณ', icon: 'pi pi-folder-open', path: '/saraban' })
    }
    return menus
  }
  if (route.path.startsWith('/ipphone')) {
    const menus = [
      { name: 'ภาพรวมสถิติการโทร', icon: 'pi pi-chart-bar', path: '/ipphone/dashboard' },
    ]
    if (isSuperAdmin.value) {
      menus.push({ name: 'นำเข้าสถิติ (CSV)', icon: 'pi pi-cloud-upload', path: '/ipphone/upload' })
      menus.push({ name: 'ผูกผู้ใช้ - เบอร์โทร', icon: 'pi pi-link', path: '/ipphone/mapping' })
    }
    return menus
  }
  if (route.path.startsWith('/directory')) {
    const menus = [
      { name: 'สมุดโทรศัพท์องค์กร', icon: 'pi pi-address-book', path: '/directory' },
    ]
    if (isSystemAdmin('directory') || isSuperAdmin.value) {
      menus.push({ name: 'จัดการรายชื่อโทรศัพท์', icon: 'pi pi-phone', path: '/directory/manage' })
    }
    menus.push({ name: 'แชทกับผู้รับผิดชอบ', icon: 'pi pi-comments', path: '/support' })
    return menus
  }
  if (route.path.startsWith('/maintenance')) {
    const menus = [{ name: 'แจ้งซ่อมงานอาคาร', icon: 'pi pi-ticket', path: '/maintenance/service' }]

    if (canOpenMaintenanceDashboard.value) {
      menus.unshift({ name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/maintenance/dashboard' })
    }

    if (canOpenMaintenanceTechnician.value) {
      menus.push({ name: 'ช่างรับงาน', icon: 'pi pi-wrench', path: '/maintenance/technician' })
    }

    if (canOpenMaintenanceSupervisor.value) {
      menus.push({
        name: 'หัวหน้าตรวจสอบงาน',
        icon: 'pi pi-verified',
        path: '/maintenance/supervisor-review',
      })
    }

    if (canOpenMaintenanceExternal.value) {
      menus.push({
        name: 'รับเรื่องจ้างช่างนอก',
        icon: 'pi pi-briefcase',
        path: '/maintenance/external-procurement',
      })
    }

    // Timeline ช่างภายนอก menu removed as requested

    if (canOpenMaintenanceSpareParts.value) {
      menus.push({
        name: 'คลังอะไหล่งานอาคาร',
        icon: 'pi pi-box',
        path: '/maintenance/spare-parts',
      })
    }

    return menus
  }
  if (route.path.startsWith('/postal')) {
    const menus = [{ name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/postal/dashboard' }]
    if (isSystemAdmin('postal')) {
      menus.push({ name: 'ไปรษณีย์ออก', icon: 'pi pi-send', path: '/postal/outgoing' })
      menus.push({ name: 'ไปรษณีย์เข้า', icon: 'pi pi-inbox', path: '/postal/incoming' })
    }
    return menus
  }

  if (route.path.startsWith('/meeting')) {
    const menus = [{ name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/meeting/dashboard' }]
    if (isSystemAdmin('meeting')) {
      menus.push({ name: 'บันทึกสถิติรายเดือน', icon: 'pi pi-file-edit', path: '/meeting' })
    }
    return menus
  }

  if (route.path.startsWith('/vehicle')) {
    const menus = [{ name: 'ข้อมูลรถยนต์', icon: 'pi pi-car', path: '/vehicle' }]
    if (isSystemAdmin('system11')) {
      menus.push({ name: 'จัดการหน่วยงาน', icon: 'pi pi-sitemap', path: '/vehicle/departments' })
      menus.push({ name: 'จัดการจังหวัด', icon: 'pi pi-map-marker', path: '/vehicle/provinces' })
    }
    return menus
  }

  if (route.path.startsWith('/admin') && isSuperAdmin.value) {
    return [
      { name: 'Admin Tool', icon: 'pi pi-th-large', path: '/admin/system-management' },
      { name: 'จัดการผู้ใช้งาน', icon: 'pi pi-users', path: '/admin/users' },
      { name: 'ตั้งค่าหน่วยงาน', icon: 'pi pi-sitemap', path: '/admin/departments' },
      { name: 'ตั้งค่าอาคาร/มิเตอร์', icon: 'pi pi-building', path: '/admin/buildings' },
      { name: 'ตั้งค่าประเภทน้ำมัน', icon: 'pi pi-gauge', path: '/admin/fuel-types' },
      { name: 'ตั้งค่าห้องประชุม', icon: 'pi pi-objects-column', path: '/admin/meeting-rooms' },
      { name: 'Audit Log', icon: 'pi pi-shield', path: '/admin/audit' },
      { name: 'ตรวจสอบการใช้งาน Server', icon: 'pi pi-server', path: '/admin/quota' },
    ]
  }
  return []
})

const navigateTo = (path: string) => {
  notificationOpen.value = false
  router.push(path)
  if (isMobile.value) sidebarOpen.value = false
}

const parseBackendDate = (dateStr: string): Date | null => {
  if (!dateStr) return null
  const hasTimezone = /([zZ]|[+\-]\d{2}:\d{2})$/.test(dateStr)
  const normalized = hasTimezone ? dateStr : `${dateStr}Z`
  const parsed = new Date(normalized)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

const formatNotificationTime = (dateStr: string): string => {
  const parsed = parseBackendDate(dateStr)
  if (!parsed) return '-'
  return parsed.toLocaleString('th-TH', {
    year: '2-digit',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Asia/Bangkok',
  })
}

const fetchMaintenanceNotifications = async () => {
  if (!authStore.isAuthenticated) return
  notificationLoading.value = true
  try {
    const { data } = await api.get('/ServiceRequest/notifications', { params: { take: 20 } })
    maintenanceNotification.value = {
      unreadChatCount: data.unreadChatCount || 0,
      timelineUpdateCount: data.timelineUpdateCount || 0,
      total: data.total || 0,
      items: data.items || [],
    }
  } catch {
    // keep last successful snapshot
  } finally {
    notificationLoading.value = false
  }
}

const pushNotificationItem = (item: MaintenanceNotificationItem) => {
  const deduped = maintenanceNotification.value.items.filter(
    (existing) =>
      !(
        existing.requestId === item.requestId &&
        existing.type === item.type &&
        existing.createdAt === item.createdAt
      ),
  )

  const updated = [item, ...deduped]
  maintenanceNotification.value.items = updated.slice(0, 20)

  if (item.type === 'chat' && item.isUnread) {
    maintenanceNotification.value.unreadChatCount += 1
  } else if (item.type === 'timeline') {
    maintenanceNotification.value.timelineUpdateCount += 1
  }

  recomputeNotificationTotal()
}

const recomputeNotificationTotal = () => {
  maintenanceNotification.value.total =
    maintenanceNotification.value.unreadChatCount +
    maintenanceNotification.value.timelineUpdateCount
}

const clearNotificationItem = (item: MaintenanceNotificationItem) => {
  if (item.type === 'chat') {
    const removedChatCount = maintenanceNotification.value.items.filter(
      (existing) => existing.type === 'chat' && existing.requestId === item.requestId,
    ).length

    maintenanceNotification.value.items = maintenanceNotification.value.items.filter(
      (existing) => !(existing.type === 'chat' && existing.requestId === item.requestId),
    )

    maintenanceNotification.value.unreadChatCount = Math.max(
      0,
      maintenanceNotification.value.unreadChatCount - removedChatCount,
    )
  } else {
    const before = maintenanceNotification.value.items.length
    maintenanceNotification.value.items = maintenanceNotification.value.items.filter(
      (existing) =>
        !(
          existing.type === item.type &&
          existing.requestId === item.requestId &&
          existing.createdAt === item.createdAt
        ),
    )

    if (maintenanceNotification.value.items.length < before) {
      maintenanceNotification.value.timelineUpdateCount = Math.max(
        0,
        maintenanceNotification.value.timelineUpdateCount - 1,
      )
    }
  }

  recomputeNotificationTotal()
}

const handleNotificationClick = async (item: MaintenanceNotificationItem) => {
  if (item.type === 'chat') {
    try {
      await api.put(`/ServiceRequest/${item.requestId}/messages/read`)
    } catch {
      // ignore network error; still clear local bell item for better UX
    }
  } else if (item.type === 'timeline') {
    try {
      await api.put(`/ServiceRequest/${item.requestId}/timeline/read`, {
        createdAt: item.createdAt,
      })
    } catch {
      // ignore network error; still clear local bell item for better UX
    }
  }

  clearNotificationItem(item)
  navigateTo(item.path)
}

const handleRealtimeNotification = (payload: unknown) => {
  const data = payload as Partial<MaintenanceNotificationItem>
  if (!data.requestId || !data.createdAt || !data.path) return

  pushNotificationItem({
    requestId: data.requestId,
    workOrderNo: data.workOrderNo || '',
    title: data.title || '',
    type: (data.type as MaintenanceNotificationItem['type']) || 'timeline',
    message: data.message || '',
    status: data.status || '',
    isUnread: data.isUnread ?? false,
    createdAt: data.createdAt,
    path: data.path,
  })
}

const toggleNotifications = () => {
  notificationOpen.value = !notificationOpen.value
}
</script>

<template>
  <div class="flex h-screen bg-gray-50 overflow-hidden">
    <!-- ── Sidebar ── -->
    <!-- Mobile overlay -->
    <div
      v-if="isMobile && sidebarOpen && !isEmbed"
      class="fixed inset-0 bg-slate-900/40 z-20 transition-opacity backdrop-blur-sm"
      @click="sidebarOpen = false"
    ></div>

    <Transition name="slide">
      <aside
        v-if="(sidebarOpen || !isMobile) && !isEmbed"
        class="bg-slate-900 flex flex-col z-50 transition-all duration-300 shrink-0 overflow-hidden"
        :class="[
          isMobile ? 'fixed inset-y-0 left-0 w-64 shadow-2xl' : sidebarOpen ? 'w-56' : 'w-16',
        ]"
      >
        <!-- Back to portal -->
        <div
          class="h-14 flex items-center border-b border-slate-700/50 shrink-0"
          :class="sidebarOpen || isMobile ? 'px-4' : 'justify-center px-2'"
        >
          <button
            v-if="sidebarOpen || isMobile"
            @click="navigateTo('/')"
            class="flex items-center gap-2 text-slate-400 hover:text-white transition-colors text-sm"
          >
            <i class="pi pi-arrow-left text-xs"></i>
            <span>หน้าหลัก</span>
          </button>
          <button
            v-else
            @click="navigateTo('/')"
            v-tooltip.right="'หน้าหลัก'"
            class="text-slate-400 hover:text-white transition-colors"
          >
            <i class="pi pi-arrow-left text-sm"></i>
          </button>
        </div>

        <!-- System header -->
        <div
          class="border-b border-slate-700/50 shrink-0"
          :class="sidebarOpen || isMobile ? 'px-4 py-4' : 'py-3 flex justify-center'"
        >
          <div :class="sidebarOpen || isMobile ? 'flex items-center gap-2.5' : ''">
            <div
              class="w-8 h-8 bg-indigo-600/20 rounded-lg flex items-center justify-center shrink-0"
              v-tooltip="!(sidebarOpen || isMobile) ? currentSystemName : ''"
            >
              <i :class="`pi ${systemIcon} text-indigo-400 text-sm`"></i>
            </div>
            <div v-if="sidebarOpen || isMobile">
              <p class="text-xs text-slate-500 uppercase tracking-wider font-semibold">
                ระบบปัจจุบัน
              </p>
              <p class="text-white text-sm font-semibold leading-tight mt-0.5">
                {{ currentSystemName }}
              </p>
            </div>
          </div>
        </div>

        <!-- Navigation -->
        <nav class="flex-1 py-3 overflow-y-auto" :class="sidebarOpen || isMobile ? 'px-3' : 'px-2'">
          <ul class="space-y-0.5">
            <li v-for="item in sidebarMenus" :key="item.path">
              <button
                @click="navigateTo(item.path)"
                class="w-full flex items-center rounded-lg text-left text-sm transition-all duration-150"
                :class="[
                  sidebarOpen || isMobile ? 'gap-2.5 px-3 py-2.5' : 'justify-center py-2.5',
                  route.path === item.path
                    ? 'bg-indigo-600 text-white font-semibold shadow-sm'
                    : 'text-slate-400 hover:text-white hover:bg-slate-800',
                ]"
                v-tooltip="
                  !(sidebarOpen || isMobile) ? { value: item.name, position: 'right' } : ''
                "
              >
                <i
                  :class="[
                    item.icon,
                    'text-sm shrink-0',
                    sidebarOpen || isMobile ? 'w-4 text-center' : '',
                  ]"
                ></i>
                <span v-if="sidebarOpen || isMobile">{{ item.name }}</span>
              </button>
            </li>
          </ul>
        </nav>

        <!-- Toggle button (desktop only) -->
        <div
          v-if="!isMobile"
          class="shrink-0 border-t border-slate-700/50 p-2 flex"
          :class="sidebarOpen ? 'justify-end' : 'justify-center'"
        >
          <button
            @click="toggleSidebar"
            class="p-1.5 rounded-lg text-slate-500 hover:text-white hover:bg-slate-800 transition-colors"
            :title="sidebarOpen ? 'ย่อ sidebar' : 'ขยาย sidebar'"
          >
            <i
              :class="sidebarOpen ? 'pi pi-chevron-left' : 'pi pi-chevron-right'"
              class="text-sm"
            ></i>
          </button>
        </div>

        <!-- User (when open) -->
        <div v-if="sidebarOpen || isMobile" class="p-3 border-t border-slate-700/50 shrink-0">
          <div class="flex items-center gap-2 px-2 py-2">
            <div
              class="w-7 h-7 bg-slate-700 rounded-full flex items-center justify-center shrink-0"
            >
              <i class="pi pi-user text-slate-400 text-xs"></i>
            </div>
            <p class="text-slate-400 text-xs truncate flex-1">
              {{ authStore.user?.displayName || authStore.user?.email }}
            </p>
          </div>
        </div>
        <!-- User icon only (when collapsed) -->
        <div v-else class="p-3 border-t border-slate-700/50 flex justify-center shrink-0">
          <div
            class="w-7 h-7 bg-slate-700 rounded-full flex items-center justify-center"
            v-tooltip.right="authStore.user?.displayName || authStore.user?.email"
          >
            <i class="pi pi-user text-slate-400 text-xs"></i>
          </div>
        </div>
      </aside>
    </Transition>

    <!-- ── Content area ── -->
    <div
      class="flex-1 flex flex-col overflow-hidden min-w-0"
      :class="isEmbed ? 'bg-transparent' : ''"
    >
      <!-- Header -->
      <header
        v-if="!isEmbed"
        class="h-14 bg-white border-b border-gray-100 flex items-center justify-between px-4 shrink-0"
      >
        <div class="flex items-center gap-3">
          <!-- Hamburger / toggle -->
          <button
            @click="toggleSidebar"
            class="p-2 rounded-lg text-gray-500 hover:text-gray-800 hover:bg-gray-100 transition-colors"
          >
            <i class="pi pi-bars text-sm"></i>
          </button>
          <p class="text-sm text-gray-400 hidden sm:block">ระบบการจัดการทรัพยากรภายในองค์กร</p>
        </div>

        <div class="flex items-center gap-1 relative">
          <button
            @click="toggleNotifications"
            class="relative p-2 rounded-lg text-gray-500 hover:text-gray-800 hover:bg-gray-100 transition-colors"
            title="แจ้งเตือนงานซ่อม"
          >
            <i class="pi pi-bell text-sm"></i>
            <span
              v-if="maintenanceNotification.total > 0"
              class="absolute -top-0.5 -right-0.5 min-w-4 h-4 px-1 rounded-full bg-red-500 text-white text-[10px] leading-4 text-center font-bold"
            >
              {{ maintenanceNotification.total > 99 ? '99+' : maintenanceNotification.total }}
            </span>
          </button>

          <div
            v-if="notificationOpen"
            class="absolute right-0 top-12 w-[24rem] bg-white border border-gray-200 rounded-xl shadow-xl z-50 overflow-hidden"
          >
            <div class="px-4 py-3 border-b border-gray-100 bg-slate-50">
              <p class="text-sm font-bold text-gray-800">แจ้งเตือนงานซ่อม</p>
              <p class="text-xs text-gray-500 mt-1">
                แชทใหม่ {{ maintenanceNotification.unreadChatCount }} รายการ · อัปเดตไทม์ไลน์
                {{ maintenanceNotification.timelineUpdateCount }} รายการ
              </p>
            </div>

            <div class="max-h-96 overflow-y-auto">
              <div v-if="notificationLoading" class="px-4 py-6 text-center text-sm text-gray-400">
                <i class="pi pi-spin pi-spinner mr-2"></i>กำลังโหลดแจ้งเตือน...
              </div>
              <div
                v-else-if="maintenanceNotification.items.length === 0"
                class="px-4 py-8 text-center text-sm text-gray-400"
              >
                ไม่มีแจ้งเตือนใหม่
              </div>
              <button
                v-for="item in maintenanceNotification.items"
                :key="`${item.type}-${item.requestId}-${item.createdAt}`"
                @click="handleNotificationClick(item)"
                class="w-full text-left px-4 py-3 border-b border-gray-100 hover:bg-gray-50 transition-colors"
              >
                <div class="flex items-start justify-between gap-3">
                  <div class="min-w-0">
                    <p
                      class="text-xs font-semibold"
                      :class="item.type === 'chat' ? 'text-indigo-600' : 'text-emerald-600'"
                    >
                      {{ item.type === 'chat' ? 'ข้อความใหม่' : 'อัปเดตไทม์ไลน์' }}
                    </p>
                    <p class="text-sm font-semibold text-gray-800 truncate">
                      {{ item.workOrderNo || 'ใบงาน' }} - {{ item.title }}
                    </p>
                    <p class="text-xs text-gray-500 truncate">{{ item.message }}</p>
                  </div>
                  <div class="text-[11px] text-gray-400 shrink-0">
                    {{ formatNotificationTime(item.createdAt) }}
                  </div>
                </div>
              </button>
            </div>
          </div>

          <button
            @click="navigateTo('/profile')"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg hover:bg-gray-50 transition-colors"
          >
            <div class="w-7 h-7 bg-indigo-100 rounded-full flex items-center justify-center">
              <i class="pi pi-user text-indigo-600 text-xs"></i>
            </div>
            <span class="text-sm font-medium text-gray-600 hidden sm:block">
              {{ authStore.user?.displayName || authStore.user?.email?.split('@')[0] }}
            </span>
          </button>
          <Button
            icon="pi pi-sign-out"
            severity="danger"
            text
            rounded
            size="small"
            aria-label="Logout"
            @click="logout"
          />
        </div>
      </header>

      <!-- Page content -->
      <main class="flex-1 overflow-y-auto" :class="isEmbed ? 'p-2 sm:p-4' : 'p-4 lg:p-6'">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.25s;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.slide-enter-active,
.slide-leave-active {
  transition: transform 0.25s ease;
}

.slide-enter-from,
.slide-leave-to {
  transform: translateX(-100%);
}
</style>
