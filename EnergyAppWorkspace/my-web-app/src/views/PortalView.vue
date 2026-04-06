<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useAuth } from '@/composables/useAuth'
import api from '@/services/api'


import { usePermissions } from '@/composables/usePermissions'
import { MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION } from '@/config/maintenancePermissions'

import Button from 'primevue/button'
import Tooltip from 'primevue/tooltip'

const vTooltip = Tooltip

const router = useRouter()
const authStore = useAuthStore()
const { logout } = useAuth()
const { isSuperAdmin } = usePermissions()
const hasMaintenanceAccess = computed(
  () =>
    isSuperAdmin.value ||
    (authStore.user?.role ?? '').trim().toLowerCase() === 'adminbuilding' ||
    (authStore.userProfile?.adminSystems ?? []).includes(MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION),
)
const hasAdminToolAccess = computed(
  () => hasAccess('system10'),
)

// ─── Sidebar state ────────────────────────────────────────────────────────────
const sidebarOpen = ref(true)
const isMobile = ref(false)

function checkMobile() {
  isMobile.value = window.innerWidth < 1024
  sidebarOpen.value = !isMobile.value
}

// ─── เบอร์โทรศัพท์ที่ผูกกับ user นี้ ─────────────────────────────────────────
interface LinkedExt { id: string; ipPhoneNumber: string; departmentName: string; workgroup?: string }
const myLinkedExtensions = ref<LinkedExt[]>([])

onMounted(async () => {
  checkMobile()
  window.addEventListener('resize', checkMobile)
  const email = authStore.user?.email
  if (!email) return
  try {
    const response = await api.get('/ipphone/my-extensions')
    myLinkedExtensions.value = Array.isArray(response.data) ? response.data : (response.data?.data || [])
  } catch (error) {
    console.error('Failed to load linked extensions:', error)
  }
})
onUnmounted(() => window.removeEventListener('resize', checkMobile))

/** เข้าใช้ระบบได้: สิทธิ์ผู้ใช้ (accessible) หรือผู้ดูแลระบบย่อย (adminSystems) */
const hasAccess = (systemId: string): boolean => {
  if (isSuperAdmin.value) return true
  const acc = authStore.userProfile?.accessibleSystems ?? []
  const adm = authStore.userProfile?.adminSystems ?? []
  return acc.includes(systemId) || adm.includes(systemId)
}

const navigateTo = (path: string) => {
  if (path === '') {
    alert('🚧 ระบบนี้กำลังอยู่ระหว่างการพัฒนา (Coming Soon)')
    return
  }
  router.push(path)
}

const systems = computed(() => [
  {
    id: 'system1',
    path: '/electricity/dashboard',
    label: 'ค่าไฟฟ้า & Solar',
    desc: 'บันทึกบิลค่าไฟ กฟภ./กฟน. และพลังงาน Solar Cell',
    icon: 'pi-bolt',
    color: 'amber',
    show: hasAccess('system1'),
  },
  {
    id: 'system2',
    path: '/water/dashboard',
    label: 'ค่าน้ำประปา',
    desc: 'บันทึกเลขมิเตอร์ คำนวณหน่วยน้ำ และสรุปค่าใช้จ่าย',
    icon: 'pi-tint',
    color: 'cyan',
    show: hasAccess('system2'),
  },
  {
    id: 'system3',
    path: '/fuel/dashboard',
    label: 'น้ำมันเชื้อเพลิง',
    desc: 'บันทึกการเบิกจ่ายน้ำมัน เลขไมล์ และวิเคราะห์การใช้รถ',
    icon: 'pi-car',
    color: 'rose',
    show: hasAccess('system3'),
  },
  {
    id: 'system4',
    path: '/telephone/dashboard',
    label: 'ค่าโทรศัพท์',
    desc: 'บันทึกและวิเคราะห์ค่าใช้จ่ายโทรศัพท์รายเดือน',
    icon: 'pi-phone',
    color: 'emerald',
    show: hasAccess('system4'),
  },
  {
    id: 'system5',
    path: '/saraban/dashboard',
    label: 'สถิติงานสารบรรณ',
    desc: 'บันทึกและรายงานสถิติงานรับ-ส่งเอกสาร',
    icon: 'pi-folder-open',
    color: 'violet',
    show: hasAccess('system5'),
  },
  {
    id: 'system6',
    path: '/ipphone/dashboard',
    label: 'ระบบ IP-Phone',
    desc: 'สมุดโทรศัพท์องค์กรและสถิติการโทรประจำเดือน',
    icon: 'pi-desktop',
    color: 'teal',
    show: hasAccess('system6'),
  },
  {
    id: 'system9',
    path: '/maintenance/dashboard',
    label: 'ระบบแจ้งซ่อมงานอาคาร',
    desc: 'แจ้งซ่อม ติดตามงาน ซ่อมภายใน/ภายนอก และประวัติสินทรัพย์',
    icon: 'pi-wrench',
    color: 'orange',
    show: hasMaintenanceAccess.value,
  },
  {
    id: 'system7',
    path: '/postal/dashboard',
    label: 'ระบบไปรษณีย์',
    desc: 'บันทึกสถิติการจัดส่งไปรษณีย์ ธรรมดา/ลงทะเบียน/EMS',
    icon: 'pi-envelope',
    color: 'blue',
    show: hasAccess('system7'),
  },
  {
    id: 'system8',
    path: '/meeting/dashboard',
    label: 'สถิติห้องประชุม',
    desc: 'บันทึกและตรวจสอบสถิติการใช้งานห้องประชุมส่วนกลาง',
    icon: 'pi-users',
    color: 'teal',
    show: hasAccess('system8'),
  },
  {
    id: 'system10',
    path: '/admin/system-management',
    label: 'Admin Tool',
    desc: 'ศูนย์รวมเครื่องมือผู้ดูแลระบบและการจัดการสิทธิ์ของทั้ง 9 ระบบ',
    icon: 'pi-shield',
    color: 'violet',
    show: hasAdminToolAccess.value,
  },
])

const colorMap: Record<string, { icon: string; bg: string; hover: string }> = {
  amber: { icon: 'text-amber-500', bg: 'bg-amber-50 group-hover:bg-amber-500', hover: 'group-hover:text-amber-600' },
  cyan: { icon: 'text-cyan-500', bg: 'bg-cyan-50 group-hover:bg-cyan-500', hover: 'group-hover:text-cyan-600' },
  rose: { icon: 'text-rose-500', bg: 'bg-rose-50 group-hover:bg-rose-500', hover: 'group-hover:text-rose-600' },
  emerald: { icon: 'text-emerald-500', bg: 'bg-emerald-50 group-hover:bg-emerald-500', hover: 'group-hover:text-emerald-600' },
  violet: { icon: 'text-violet-500', bg: 'bg-violet-50 group-hover:bg-violet-500', hover: 'group-hover:text-violet-600' },
  teal: { icon: 'text-teal-500', bg: 'bg-teal-50 group-hover:bg-teal-500', hover: 'group-hover:text-teal-600' },
  orange: { icon: 'text-orange-500', bg: 'bg-orange-50 group-hover:bg-orange-500', hover: 'group-hover:text-orange-600' },
}

</script>

<template>
  <div class="flex min-h-screen bg-gray-50 relative">

    <!-- Mobile backdrop -->
    <Transition name="fade">
      <div v-if="isMobile && sidebarOpen" class="fixed inset-0 bg-black/50 z-40" @click="sidebarOpen = false" />
    </Transition>

    <!-- Sidebar -->
    <aside class="bg-slate-900 flex flex-col z-50 transition-all duration-300 overflow-hidden shrink-0" :class="[
      isMobile ? 'fixed inset-y-0 left-0 shadow-2xl' : 'sticky top-0 h-screen',
      isMobile ? (sidebarOpen ? 'w-64 translate-x-0' : 'w-64 -translate-x-full') : (sidebarOpen ? 'w-64' : 'w-16'),
    ]">
      <!-- Brand -->
      <div class="border-b border-slate-700/50 shrink-0"
        :class="sidebarOpen || isMobile ? 'p-5' : 'py-4 flex justify-center'">
        <div :class="sidebarOpen || isMobile ? 'flex items-center gap-3' : ''">
          <div
            class="w-9 h-9 bg-indigo-600 rounded-xl flex items-center justify-center shadow-lg shadow-indigo-500/20 shrink-0"
            v-tooltip="!(sidebarOpen || isMobile) ? 'E-Portal' : ''">
            <i class="pi pi-building text-white text-sm"></i>
          </div>
          <div v-if="sidebarOpen || isMobile">
            <h1 class="text-white font-bold text-base leading-none">E-Portal</h1>
            <p class="text-slate-500 text-xs mt-0.5">ระบบบริหารทรัพยากร</p>
          </div>
        </div>
      </div>

      <!-- User card -->
      <div class="shrink-0" :class="sidebarOpen || isMobile ? 'p-4' : 'py-3 flex flex-col items-center gap-2'">
        <div v-if="sidebarOpen || isMobile" class="bg-slate-800 rounded-xl p-3 flex items-center gap-3">
          <div class="w-9 h-9 bg-indigo-500/20 rounded-lg flex items-center justify-center shrink-0">
            <i class="pi pi-user text-indigo-400 text-sm"></i>
          </div>
          <div class="flex-1 min-w-0">
            <p class="text-white text-sm font-semibold truncate">
              {{ authStore.user?.displayName || authStore.user?.email?.split('@')[0] || 'User' }}
            </p>
            <span class="text-xs text-emerald-400 flex items-center gap-1 mt-0.5">
              <span class="w-1.5 h-1.5 bg-emerald-400 rounded-full inline-block"></span>
              ออนไลน์
            </span>
          </div>
        </div>
        <div v-else class="w-9 h-9 bg-indigo-500/20 rounded-lg flex items-center justify-center"
          v-tooltip.right="authStore.user?.displayName || authStore.user?.email?.split('@')[0]">
          <i class="pi pi-user text-indigo-400 text-sm"></i>
        </div>
        <Button v-if="sidebarOpen || isMobile" label="แก้ไขโปรไฟล์" icon="pi pi-user-edit" text size="small"
          class="w-full mt-2 text-slate-400! hover:text-white! hover:bg-slate-800! justify-start"
          @click="router.push('/profile'); if (isMobile) sidebarOpen = false" />
        <button v-else @click="router.push('/profile')"
          class="p-2 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition-colors"
          v-tooltip.right="'แก้ไขโปรไฟล์'">
          <i class="pi pi-user-edit text-sm"></i>
        </button>
      </div>

      <div class="flex-1"></div>

      <!-- Toggle (desktop) -->
      <div v-if="!isMobile" class="shrink-0 px-3 py-2 flex" :class="sidebarOpen ? 'justify-end' : 'justify-center'">
        <button @click="sidebarOpen = !sidebarOpen"
          class="p-1.5 rounded-lg text-slate-500 hover:text-white hover:bg-slate-800 transition-colors"
          :title="sidebarOpen ? 'ย่อ sidebar' : 'ขยาย sidebar'">
          <i :class="sidebarOpen ? 'pi pi-chevron-left' : 'pi pi-chevron-right'" class="text-sm"></i>
        </button>
      </div>

      <!-- Logout -->
      <div class="shrink-0 border-t border-slate-700/50"
        :class="sidebarOpen || isMobile ? 'p-4' : 'py-3 flex justify-center'">
        <Button v-if="sidebarOpen || isMobile" label="ออกจากระบบ" icon="pi pi-sign-out" text
          class="w-full text-slate-400! hover:text-red-400! hover:bg-red-500/10! justify-start" @click="logout" />
        <button v-else @click="logout"
          class="p-2 rounded-lg text-slate-400 hover:text-red-400 hover:bg-red-500/10 transition-colors"
          v-tooltip.right="'ออกจากระบบ'">
          <i class="pi pi-sign-out text-sm"></i>
        </button>
      </div>
    </aside>

    <!-- Main -->
    <main class="flex-1 overflow-y-auto min-w-0">
      <!-- Mobile top bar -->
      <div class="lg:hidden sticky top-0 z-30 bg-white border-b border-gray-100 h-14 flex items-center px-4 gap-3">
        <button @click="sidebarOpen = true" class="p-2 rounded-lg text-gray-500 hover:bg-gray-100 transition-colors">
          <i class="pi pi-bars text-sm"></i>
        </button>
        <div class="flex items-center gap-2">
          <div class="w-7 h-7 bg-indigo-600 rounded-lg flex items-center justify-center">
            <i class="pi pi-building text-white text-xs"></i>
          </div>
          <span class="font-bold text-gray-800 text-sm">E-Portal</span>
        </div>
      </div>
      <div class="max-w-6xl mx-auto p-8">
        <!-- Header -->
        <header class="mb-10">
          <p class="text-xs text-indigo-500 font-semibold uppercase tracking-widest mb-2">ยินดีต้อนรับ</p>
          <h2 class="text-3xl font-bold text-gray-900">เลือกระบบที่ต้องการใช้งาน</h2>
          <p class="text-gray-400 mt-2">คลิกที่การ์ดเพื่อเข้าสู่ระบบบันทึกข้อมูลและดูสถิติ</p>
        </header>

        <!-- เบอร์โทรศัพท์ที่รับผิดชอบ (แสดงเฉพาะ user ที่ผูกเบอร์) -->
        <section v-if="myLinkedExtensions.length > 0" class="mb-10">
          <div class="flex items-center gap-3 mb-4">
            <span class="w-1 h-5 bg-teal-500 rounded-full"></span>
            <h3 class="text-base font-bold text-gray-700 uppercase tracking-wide">เบอร์โทรศัพท์ที่ฉันรับผิดชอบ</h3>
            <span class="text-xs bg-teal-100 text-teal-700 font-semibold px-2 py-0.5 rounded-full">
              {{ myLinkedExtensions.length }} เบอร์
            </span>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            <div v-for="ext in myLinkedExtensions" :key="ext.id" @click="router.push(`/ipphone/directory/${ext.id}`)"
              class="group bg-white rounded-2xl border border-teal-100 p-4 cursor-pointer hover:shadow-md hover:-translate-y-0.5 hover:border-teal-300 transition-all duration-200 flex items-center gap-4">
              <div
                class="w-12 h-12 bg-teal-50 rounded-xl flex items-center justify-center shrink-0 group-hover:bg-teal-600 transition-colors">
                <span class="font-mono font-black text-teal-600 text-sm group-hover:text-white transition-colors">
                  {{ ext.ipPhoneNumber }}
                </span>
              </div>
              <div class="flex-1 min-w-0">
                <p class="text-sm text-gray-700 truncate flex items-center gap-1">
                  <i class="pi pi-building text-xs text-gray-400"></i>
                  {{ ext.departmentName }}
                </p>
                <p class="text-xs text-gray-400 truncate mt-0.5 flex items-center gap-1">
                  <i class="pi pi-users text-xs"></i>
                  {{ ext.workgroup || 'ไม่ระบุกลุ่มงาน' }}
                </p>
              </div>
              <div class="flex flex-col items-end gap-1 shrink-0">
                <span
                  class="text-[10px] bg-teal-100 text-teal-700 px-1.5 py-0.5 rounded font-semibold">ผู้รับผิดชอบ</span>
                <i class="pi pi-comments text-gray-300 group-hover:text-teal-500 transition-colors text-sm"></i>
              </div>
            </div>
          </div>
        </section>

        <!-- Core systems -->
        <section class="mb-12">
          <div class="flex items-center gap-3 mb-6">
            <span class="w-1 h-5 bg-indigo-500 rounded-full"></span>
            <h3 class="text-base font-bold text-gray-700 uppercase tracking-wide">ระบบงานหลัก</h3>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
            <div v-for="sys in systems.filter(s => s.show)" :key="sys.id" @click="navigateTo(sys.path)"
              class="group bg-white rounded-2xl border border-gray-100 p-5 cursor-pointer hover:shadow-md hover:-translate-y-0.5 transition-all duration-200">
              <div class="flex items-start gap-4">
                <div
                  class="w-11 h-11 rounded-xl flex items-center justify-center shrink-0 transition-colors duration-200 group-hover:text-white"
                  :class="colorMap[sys.color]?.bg">
                  <i :class="`pi ${sys.icon} text-lg transition-colors`" :style="{ color: 'inherit' }"></i>
                </div>
                <div class="flex-1 min-w-0">
                  <h4 class="font-bold text-gray-800 transition-colors" :class="colorMap[sys.color]?.hover">
                    {{ sys.label }}
                  </h4>
                  <p class="text-xs text-gray-400 mt-1 leading-relaxed">{{ sys.desc }}</p>
                </div>
                <i
                  class="pi pi-chevron-right text-gray-200 group-hover:text-gray-400 text-sm transition-colors shrink-0 mt-0.5"></i>
              </div>
            </div>
          </div>
        </section>

      </div>
    </main>
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
</style>
