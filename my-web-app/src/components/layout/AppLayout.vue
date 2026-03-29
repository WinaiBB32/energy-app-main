<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { usePermissions } from '@/composables/usePermissions'
import Button from 'primevue/button'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()
const { isSystemAdmin, isSuperAdmin } = usePermissions()

const isEmbed = computed(() => route.query.embed === 'true')

// ─── Sidebar state ────────────────────────────────────────────────────────────
const sidebarOpen = ref(true)
const isMobile = ref(false)

function checkMobile() {
  isMobile.value = window.innerWidth < 1024
  if (isMobile.value) sidebarOpen.value = false
  else sidebarOpen.value = true
}

onMounted(() => {
  checkMobile()
  window.addEventListener('resize', checkMobile)
})
onUnmounted(() => window.removeEventListener('resize', checkMobile))

const toggleSidebar = () => (sidebarOpen.value = !sidebarOpen.value)

// ─── System info ──────────────────────────────────────────────────────────────
const currentSystemName = computed(() => {
  if (route.path.startsWith('/electricity')) return 'ค่าไฟฟ้า & Solar'
  if (route.path.startsWith('/water')) return 'ค่าน้ำประปา'
  if (route.path.startsWith('/fuel')) return 'น้ำมันเชื้อเพลิง'
  if (route.path.startsWith('/telephone')) return 'ค่าโทรศัพท์'
  if (route.path.startsWith('/saraban')) return 'งานสารบรรณ'
  if (route.path.startsWith('/ipphone')) return 'IP-Phone'
  if (route.path.startsWith('/admin')) return 'ผู้ดูแลระบบ'
  return 'E-Portal'
})

const systemIcon = computed(() => {
  if (route.path.startsWith('/electricity')) return 'pi-bolt'
  if (route.path.startsWith('/water')) return 'pi-tint'
  if (route.path.startsWith('/fuel')) return 'pi-car'
  if (route.path.startsWith('/telephone')) return 'pi-phone'
  if (route.path.startsWith('/saraban')) return 'pi-folder-open'
  if (route.path.startsWith('/ipphone')) return 'pi-desktop'
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
      menus.push({ name: 'ประวัติใบรับรองแทนฯ', icon: 'pi pi-history', path: '/fuel/receipt-history' })
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
      { name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/ipphone/dashboard' },
      { name: 'สมุดโทรศัพท์องค์กร (Directory)', icon: 'pi pi-address-book', path: '/ipphone' },
      { name: 'แชทกับผู้รับผิดชอบ', icon: 'pi pi-comments', path: '/support' },
    ]
    if (isSystemAdmin('ipphone')) {
      menus.push({ name: 'แจ้งปัญหา / บริการ', icon: 'pi pi-ticket', path: '/ipphone/service' })
      menus.push({ name: 'นำเข้าสถิติ (CSV)', icon: 'pi pi-cloud-upload', path: '/ipphone/upload' })
      menus.push({ name: 'ผูกผู้ใช้ - เบอร์โทร', icon: 'pi pi-link', path: '/ipphone/mapping' })
    }
    return menus
  }
  if (route.path.startsWith('/postal')) {
    const menus = [{ name: 'ภาพรวม', icon: 'pi pi-chart-bar', path: '/postal/dashboard' }]
    if (isSystemAdmin('postal')) {
      menus.push({ name: 'บันทึกข้อมูลไปรษณีย์', icon: 'pi pi-file-edit', path: '/postal' })
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

  if (route.path.startsWith('/admin') && isSuperAdmin.value) {
    return [
      { name: 'จัดการผู้ใช้งาน', icon: 'pi pi-users', path: '/admin/users' },
      { name: 'ตั้งค่าหน่วยงาน', icon: 'pi pi-sitemap', path: '/admin/departments' },
      { name: 'Audit Log', icon: 'pi pi-shield', path: '/admin/audit' },
      { name: 'เช็คโควตาระบบ (Quota)', icon: 'pi pi-server', path: '/admin/quota' },
    ]
  }
  return []
})

const navigateTo = (path: string) => {
  router.push(path)
  if (isMobile.value) sidebarOpen.value = false
}
</script>

<template>
  <div class="flex h-screen bg-gray-50 overflow-hidden">

    <!-- ── Sidebar ── -->
    <!-- Mobile overlay -->
    <div v-if="isMobile && sidebarOpen && !isEmbed"
      class="fixed inset-0 bg-slate-900/40 z-20 transition-opacity backdrop-blur-sm" @click="sidebarOpen = false"></div>

    <Transition name="slide">
      <aside v-if="(sidebarOpen || !isMobile) && !isEmbed"
        class="bg-slate-900 flex flex-col z-50 transition-all duration-300 shrink-0 overflow-hidden" :class="[
          isMobile
            ? 'fixed inset-y-0 left-0 w-64 shadow-2xl'
            : sidebarOpen ? 'w-56' : 'w-16',
        ]">
        <!-- Back to portal -->
        <div class="h-14 flex items-center border-b border-slate-700/50 shrink-0"
          :class="sidebarOpen || isMobile ? 'px-4' : 'justify-center px-2'">
          <button v-if="sidebarOpen || isMobile" @click="navigateTo('/')"
            class="flex items-center gap-2 text-slate-400 hover:text-white transition-colors text-sm">
            <i class="pi pi-arrow-left text-xs"></i>
            <span>หน้าหลัก</span>
          </button>
          <button v-else @click="navigateTo('/')" v-tooltip.right="'หน้าหลัก'"
            class="text-slate-400 hover:text-white transition-colors">
            <i class="pi pi-arrow-left text-sm"></i>
          </button>
        </div>

        <!-- System header -->
        <div class="border-b border-slate-700/50 shrink-0"
          :class="sidebarOpen || isMobile ? 'px-4 py-4' : 'py-3 flex justify-center'">
          <div :class="sidebarOpen || isMobile ? 'flex items-center gap-2.5' : ''">
            <div class="w-8 h-8 bg-indigo-600/20 rounded-lg flex items-center justify-center shrink-0"
              :class="!(sidebarOpen || isMobile) ? '' : ''"
              v-tooltip="!(sidebarOpen || isMobile) ? currentSystemName : ''">
              <i :class="`pi ${systemIcon} text-indigo-400 text-sm`"></i>
            </div>
            <div v-if="sidebarOpen || isMobile">
              <p class="text-xs text-slate-500 uppercase tracking-wider font-semibold">ระบบปัจจุบัน</p>
              <p class="text-white text-sm font-semibold leading-tight mt-0.5">{{ currentSystemName }}</p>
            </div>
          </div>
        </div>

        <!-- Navigation -->
        <nav class="flex-1 py-3 overflow-y-auto" :class="sidebarOpen || isMobile ? 'px-3' : 'px-2'">
          <ul class="space-y-0.5">
            <li v-for="item in sidebarMenus" :key="item.path">
              <button @click="navigateTo(item.path)"
                class="w-full flex items-center rounded-lg text-left text-sm transition-all duration-150" :class="[
                  sidebarOpen || isMobile ? 'gap-2.5 px-3 py-2.5' : 'justify-center py-2.5',
                  route.path === item.path
                    ? 'bg-indigo-600 text-white font-semibold shadow-sm'
                    : 'text-slate-400 hover:text-white hover:bg-slate-800',
                ]" v-tooltip="!(sidebarOpen || isMobile) ? { value: item.name, position: 'right' } : ''">
                <i :class="[item.icon, 'text-sm shrink-0', sidebarOpen || isMobile ? 'w-4 text-center' : '']"></i>
                <span v-if="sidebarOpen || isMobile">{{ item.name }}</span>
              </button>
            </li>
          </ul>
        </nav>

        <!-- Toggle button (desktop only) -->
        <div v-if="!isMobile" class="shrink-0 border-t border-slate-700/50 p-2 flex"
          :class="sidebarOpen ? 'justify-end' : 'justify-center'">
          <button @click="toggleSidebar"
            class="p-1.5 rounded-lg text-slate-500 hover:text-white hover:bg-slate-800 transition-colors"
            :title="sidebarOpen ? 'ย่อ sidebar' : 'ขยาย sidebar'">
            <i :class="sidebarOpen ? 'pi pi-chevron-left' : 'pi pi-chevron-right'" class="text-sm"></i>
          </button>
        </div>

        <!-- User (when open) -->
        <div v-if="sidebarOpen || isMobile" class="p-3 border-t border-slate-700/50 shrink-0">
          <div class="flex items-center gap-2 px-2 py-2">
            <div class="w-7 h-7 bg-slate-700 rounded-full flex items-center justify-center shrink-0">
              <i class="pi pi-user text-slate-400 text-xs"></i>
            </div>
            <p class="text-slate-400 text-xs truncate flex-1">
              {{ authStore.user?.displayName || authStore.user?.email }}
            </p>
          </div>
        </div>
        <!-- User icon only (when collapsed) -->
        <div v-else class="p-3 border-t border-slate-700/50 flex justify-center shrink-0">
          <div class="w-7 h-7 bg-slate-700 rounded-full flex items-center justify-center"
            v-tooltip.right="authStore.user?.displayName || authStore.user?.email">
            <i class="pi pi-user text-slate-400 text-xs"></i>
          </div>
        </div>
      </aside>
    </Transition>

    <!-- ── Content area ── -->
    <div class="flex-1 flex flex-col overflow-hidden min-w-0" :class="isEmbed ? 'bg-transparent' : ''">
      <!-- Header -->
      <header v-if="!isEmbed"
        class="h-14 bg-white border-b border-gray-100 flex items-center justify-between px-4 shrink-0">
        <div class="flex items-center gap-3">
          <!-- Hamburger / toggle -->
          <button @click="toggleSidebar"
            class="p-2 rounded-lg text-gray-500 hover:text-gray-800 hover:bg-gray-100 transition-colors">
            <i class="pi pi-bars text-sm"></i>
          </button>
          <p class="text-sm text-gray-400 hidden sm:block">ระบบการจัดการทรัพยากรภายในองค์กร</p>
        </div>

        <div class="flex items-center gap-1">
          <button @click="navigateTo('/profile')"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg hover:bg-gray-50 transition-colors">
            <div class="w-7 h-7 bg-indigo-100 rounded-full flex items-center justify-center">
              <i class="pi pi-user text-indigo-600 text-xs"></i>
            </div>
            <span class="text-sm font-medium text-gray-600 hidden sm:block">
              {{ authStore.user?.displayName || authStore.user?.email?.split('@')[0] }}
            </span>
          </button>
          <Button icon="pi pi-sign-out" severity="danger" text rounded size="small" aria-label="Logout"
            @click="authStore.logout" />
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
