<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import api from '@/services/api'

import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'

// ─── Types ────────────────────────────────────────────────────────────────────
interface LinkedDirectory {
  id: string
  ipPhoneNumber: string
  ownerName: string
  departmentId: string
  departmentName: string
  workgroup?: string
  linkedUsers?: { uid: string; displayName: string; email: string }[]
}

interface ServiceRequest {
  id: string
  title: string
  description: string
  category: string
  priority: string
  status: string
  extension: string
  requesterName: string
  requesterEmail: string
  assignedTo?: string
  note?: string
  createdAt: string | null
  updatedAt?: string | null
}

// ─── Setup ────────────────────────────────────────────────────────────────────
const router = useRouter()
const authStore = useAuthStore()

// ─── Sidebar state ────────────────────────────────────────────────────────────
const sidebarOpen = ref(true)
const isMobile = ref(false)

function checkMobile() {
  isMobile.value = window.innerWidth < 1024
  sidebarOpen.value = !isMobile.value
}
onMounted(() => { checkMobile(); window.addEventListener('resize', checkMobile) })
onUnmounted(() => window.removeEventListener('resize', checkMobile))

const categoryOptions = [
  { label: 'แจ้งซ่อมโทรศัพท์', value: 'repair' },
  { label: 'ขอติดตั้งสาย/เบอร์ใหม่', value: 'install' },
  { label: 'ขอย้ายสาย/เบอร์', value: 'move' },
  { label: 'ปัญหาคุณภาพเสียง', value: 'quality' },
  { label: 'อื่น ๆ', value: 'other' },
]
const priorityOptions = [
  { label: 'ปกติ', value: 'normal' },
  { label: 'เร่งด่วน', value: 'urgent' },
  { label: 'วิกฤต', value: 'critical' },
]

// ─── State ────────────────────────────────────────────────────────────────────
const currentTab = ref<'mine' | 'assigned'>('mine')

const requests = ref<ServiceRequest[]>([])
const loading = ref(true)

const linkedDirectories = ref<LinkedDirectory[]>([])
const loadingLinked = ref(true)
const unreadDirIds = ref<Set<string>>(new Set())

function getLastSeen(dirId: string): number {
  return parseInt(localStorage.getItem(`ipphone_chat_seen_${dirId}`) ?? '0', 10)
}

function markSeen(dirId: string): void {
  localStorage.setItem(`ipphone_chat_seen_${dirId}`, Date.now().toString())
  const next = new Set(unreadDirIds.value)
  next.delete(dirId)
  unreadDirIds.value = next
}

const saving = ref(false)
const successMsg = ref('')

const formVisible = ref(false)
const formError = ref('')
const form = ref({
  title: '',
  description: '',
  category: 'repair',
  priority: 'normal',
  extension: '',
})

// ─── Load data ─────────────────────────────────────────────────────────────────
onMounted(async () => {
  const email = authStore.user?.email
  if (!email) return

  // 1. โหลดคำร้องของตัวเอง
  try {
    const res = await api.get<ServiceRequest[]>('/IPPhoneServiceRequest', {
      params: { requesterEmail: email },
    })
    const list = (res.data ?? []).slice().sort((a, b) => {
      const ta = a.createdAt ? new Date(a.createdAt).getTime() : 0
      const tb = b.createdAt ? new Date(b.createdAt).getTime() : 0
      return tb - ta
    })
    requests.value = list
  } catch {
    // ignore — show empty list
  } finally {
    loading.value = false
  }

  // 2. โหลด IP Phone directories ที่ผูกกับ user นี้
  try {
    const res = await api.get<LinkedDirectory[]>('/IPPhoneDirectory', {
      params: { linkedEmail: email },
    })
    linkedDirectories.value = res.data ?? []
    if (linkedDirectories.value.length > 0) {
      currentTab.value = 'assigned'
    }
    // ตรวจสอบข้อความใหม่ (last-seen จาก localStorage)
    const currentUid = authStore.user?.uid ?? ''
    const unread = new Set<string>()
    await Promise.all(
      linkedDirectories.value.map(async (dir) => {
        const lastSeen = getLastSeen(dir.id)
        try {
          const msgRes = await api.get<{ senderId: string; createdAt: string | null }[]>(
            `/IPPhoneDirectory/${dir.id}/messages`,
            { params: { limit: 1, orderBy: 'createdAt_desc' } },
          )
          const latest = msgRes.data?.[0]
          if (latest) {
            const msgTime = latest.createdAt ? new Date(latest.createdAt).getTime() : 0
            if (msgTime > lastSeen + 5000 && latest.senderId !== currentUid) {
              unread.add(dir.id)
            }
          }
        } catch {
          // ignore per-directory errors
        }
      }),
    )
    unreadDirIds.value = unread
  } catch {
    // ignore
  } finally {
    loadingLinked.value = false
  }
})

// ─── Computed ─────────────────────────────────────────────────────────────────
const statCounts = computed(() => ({
  pending:     requests.value.filter((r) => r.status === 'pending').length,
  in_progress: requests.value.filter((r) => r.status === 'in_progress').length,
  done:        requests.value.filter((r) => r.status === 'done').length,
}))

const linkedCount = computed(() => unreadDirIds.value.size)

// ─── Helpers ──────────────────────────────────────────────────────────────────
function formatDate(ts: string | null | undefined): string {
  if (!ts) return '-'
  return new Date(ts).toLocaleString('th-TH', {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

function categoryLabel(val: string): string {
  return categoryOptions.find((c) => c.value === val)?.label ?? val
}

function priorityTag(val: string): { severity: 'info' | 'warn' | 'danger'; label: string } {
  if (val === 'urgent')   return { severity: 'warn',   label: 'เร่งด่วน' }
  if (val === 'critical') return { severity: 'danger', label: 'วิกฤต' }
  return                         { severity: 'info',   label: 'ปกติ' }
}

function statusTag(val: string): { severity: 'secondary' | 'info' | 'success' | 'danger'; label: string } {
  if (val === 'pending')     return { severity: 'secondary', label: 'รอดำเนินการ' }
  if (val === 'in_progress') return { severity: 'info',      label: 'กำลังดำเนินการ' }
  if (val === 'done')        return { severity: 'success',   label: 'เสร็จสิ้น' }
  return                            { severity: 'danger',    label: 'ยกเลิก' }
}

// ─── Actions ──────────────────────────────────────────────────────────────────
function openForm() {
  form.value = { title: '', description: '', category: 'repair', priority: 'normal', extension: '' }
  formError.value = ''
  formVisible.value = true
}

async function submitRequest() {
  if (!form.value.title.trim())       { formError.value = 'กรุณาระบุหัวข้อ'; return }
  if (!form.value.description.trim()) { formError.value = 'กรุณาระบุรายละเอียด'; return }
  saving.value = true
  try {
    const res = await api.post<ServiceRequest>('/IPPhoneServiceRequest', {
      ...form.value,
      status: 'pending',
      requesterName:  authStore.user?.displayName ?? authStore.user?.email ?? '',
      requesterEmail: authStore.user?.email ?? '',
      requesterUid:   authStore.user?.uid ?? authStore.user?.id ?? '',
    })
    formVisible.value = false
    if (res.data) {
      requests.value = [res.data, ...requests.value]
    }
    successMsg.value = 'ส่งคำร้องเรียบร้อยแล้ว ทีมงานจะติดต่อกลับโดยเร็ว'
    setTimeout(() => (successMsg.value = ''), 6000)
  } catch (e) {
    formError.value = `เกิดข้อผิดพลาด: ${String(e)}`
  } finally {
    saving.value = false
  }
}

function openChat(r: ServiceRequest) {
  router.push(`/ipphone/service/${r.id}`)
}
</script>

<template>
  <div class="flex min-h-screen bg-gray-50 relative">

    <!-- Mobile backdrop -->
    <Transition name="fade">
      <div v-if="isMobile && sidebarOpen" class="fixed inset-0 bg-black/50 z-40" @click="sidebarOpen = false" />
    </Transition>

    <!-- Sidebar -->
    <aside
      class="bg-slate-900 flex flex-col z-50 transition-all duration-300 overflow-hidden shrink-0"
      :class="[
        isMobile ? 'fixed inset-y-0 left-0 shadow-2xl' : 'sticky top-0 h-screen',
        isMobile ? (sidebarOpen ? 'w-64 translate-x-0' : 'w-64 -translate-x-full') : (sidebarOpen ? 'w-64' : 'w-16'),
      ]"
    >
      <!-- Brand -->
      <div class="border-b border-slate-700/50 shrink-0"
        :class="sidebarOpen || isMobile ? 'p-5' : 'py-4 flex justify-center'">
        <div :class="sidebarOpen || isMobile ? 'flex items-center gap-3' : ''">
          <div class="w-9 h-9 bg-orange-600 rounded-xl flex items-center justify-center shadow-lg shadow-orange-500/20 shrink-0"
            v-tooltip="!(sidebarOpen || isMobile) ? 'แจ้งปัญหา / ขอบริการ' : ''">
            <i class="pi pi-ticket text-white text-sm"></i>
          </div>
          <div v-if="sidebarOpen || isMobile">
            <h1 class="text-white font-bold text-base leading-none">แจ้งปัญหา / ขอบริการ</h1>
            <p class="text-slate-500 text-xs mt-0.5">ระบบสนับสนุนผู้ใช้งาน</p>
          </div>
        </div>
      </div>

      <!-- User card -->
      <div class="shrink-0" :class="sidebarOpen || isMobile ? 'p-4' : 'py-3 flex justify-center'">
        <div v-if="sidebarOpen || isMobile" class="bg-slate-800 rounded-xl p-3 flex items-center gap-3">
          <div class="w-9 h-9 bg-orange-500/20 rounded-lg flex items-center justify-center shrink-0">
            <i class="pi pi-user text-orange-400 text-sm"></i>
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
        <div v-else class="w-9 h-9 bg-orange-500/20 rounded-lg flex items-center justify-center"
          v-tooltip.right="authStore.user?.displayName || authStore.user?.email?.split('@')[0]">
          <i class="pi pi-user text-orange-400 text-sm"></i>
        </div>
      </div>

      <!-- Nav menu -->
      <nav class="flex-1 py-2 overflow-y-auto" :class="sidebarOpen || isMobile ? 'px-3' : 'px-2'">
        <ul class="space-y-0.5">
          <!-- คำร้องของฉัน -->
          <li>
            <button
              @click="currentTab = 'mine'; if(isMobile) sidebarOpen = false"
              class="w-full flex items-center rounded-lg text-left text-sm transition-all duration-150"
              :class="[
                sidebarOpen || isMobile ? 'gap-2.5 px-3 py-2.5' : 'justify-center py-2.5',
                currentTab === 'mine'
                  ? 'bg-orange-600 text-white font-semibold'
                  : 'text-slate-400 hover:text-white hover:bg-slate-800',
              ]"
              v-tooltip="!(sidebarOpen || isMobile) ? { value: 'คำร้องของฉัน', position: 'right' } : ''"
            >
              <i class="pi pi-ticket text-sm shrink-0"></i>
              <span v-if="sidebarOpen || isMobile">คำร้องของฉัน</span>
            </button>
          </li>
          <!-- แชทที่รอตอบ -->
          <li>
            <button
              @click="currentTab = 'assigned'; if(isMobile) sidebarOpen = false"
              class="w-full flex items-center rounded-lg text-left text-sm transition-all duration-150"
              :class="[
                sidebarOpen || isMobile ? 'gap-2.5 px-3 py-2.5' : 'justify-center py-2.5',
                currentTab === 'assigned'
                  ? 'bg-orange-600 text-white font-semibold'
                  : 'text-slate-400 hover:text-white hover:bg-slate-800',
              ]"
              v-tooltip="!(sidebarOpen || isMobile) ? { value: 'แชทกับผู้รับผิดชอบ', position: 'right' } : ''"
            >
              <i class="pi pi-comments text-sm shrink-0"></i>
              <span v-if="sidebarOpen || isMobile" class="flex-1">แชทกับผู้รับผิดชอบ</span>
              <span
                v-if="linkedCount > 0 && (sidebarOpen || isMobile)"
                class="ml-auto bg-teal-500 text-white text-[10px] font-bold px-1.5 py-0.5 rounded-full leading-none"
              >{{ linkedCount }}</span>
            </button>
          </li>
        </ul>
      </nav>

      <!-- Toggle (desktop) -->
      <div v-if="!isMobile" class="shrink-0 px-3 py-2 flex"
        :class="sidebarOpen ? 'justify-end' : 'justify-center'">
        <button @click="sidebarOpen = !sidebarOpen"
          class="p-1.5 rounded-lg text-slate-500 hover:text-white hover:bg-slate-800 transition-colors">
          <i :class="sidebarOpen ? 'pi pi-chevron-left' : 'pi pi-chevron-right'" class="text-sm"></i>
        </button>
      </div>

      <!-- Back to portal -->
      <div class="shrink-0 border-t border-slate-700/50"
        :class="sidebarOpen || isMobile ? 'p-4' : 'py-3 flex justify-center'">
        <button
          v-if="sidebarOpen || isMobile"
          @click="router.push('/')"
          class="flex items-center gap-2 text-slate-400 hover:text-white transition-colors text-sm w-full px-2 py-2"
        >
          <i class="pi pi-arrow-left text-xs"></i>
          <span>กลับหน้าหลัก</span>
        </button>
        <button v-else @click="router.push('/')"
          class="p-2 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition-colors"
          v-tooltip.right="'กลับหน้าหลัก'">
          <i class="pi pi-arrow-left text-sm"></i>
        </button>
      </div>
    </aside>

    <!-- Main -->
    <main class="flex-1 overflow-y-auto min-w-0">
      <!-- Mobile top bar -->
      <div class="lg:hidden sticky top-0 z-30 bg-white border-b border-gray-100 h-14 flex items-center px-4 gap-3">
        <button @click="sidebarOpen = true"
          class="p-2 rounded-lg text-gray-500 hover:bg-gray-100 transition-colors">
          <i class="pi pi-bars text-sm"></i>
        </button>
        <div class="flex items-center gap-2">
          <div class="w-7 h-7 bg-orange-600 rounded-lg flex items-center justify-center">
            <i class="pi pi-ticket text-white text-xs"></i>
          </div>
          <span class="font-bold text-gray-800 text-sm">แจ้งปัญหา / ขอบริการ</span>
        </div>
      </div>
      <div class="max-w-4xl mx-auto p-8 space-y-6">

        <!-- Success -->
        <Message v-if="successMsg" severity="success" :closable="false">{{ successMsg }}</Message>

        <!-- ══ แท็บ: คำร้องของฉัน ══ -->
        <template v-if="currentTab === 'mine'">
          <div class="flex items-start justify-between">
            <div>
              <h2 class="text-2xl font-bold text-gray-900">คำร้องของฉัน</h2>
              <p class="text-sm text-gray-400 mt-1">แจ้งปัญหาหรือขอรับบริการเกี่ยวกับระบบโทรศัพท์</p>
            </div>
            <Button label="แจ้งปัญหา / ขอรับบริการ" icon="pi pi-plus" @click="openForm" />
          </div>

          <!-- Stat cards -->
          <div class="grid grid-cols-3 gap-4">
            <div class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3">
              <div class="w-10 h-10 bg-yellow-100 rounded-lg flex items-center justify-center">
                <i class="pi pi-clock text-yellow-600"></i>
              </div>
              <div>
                <p class="text-2xl font-bold text-gray-900">{{ statCounts.pending }}</p>
                <p class="text-xs text-gray-400">รอดำเนินการ</p>
              </div>
            </div>
            <div class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3">
              <div class="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
                <i class="pi pi-sync text-blue-600"></i>
              </div>
              <div>
                <p class="text-2xl font-bold text-gray-900">{{ statCounts.in_progress }}</p>
                <p class="text-xs text-gray-400">กำลังดำเนินการ</p>
              </div>
            </div>
            <div class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3">
              <div class="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
                <i class="pi pi-check-circle text-green-600"></i>
              </div>
              <div>
                <p class="text-2xl font-bold text-gray-900">{{ statCounts.done }}</p>
                <p class="text-xs text-gray-400">เสร็จสิ้น</p>
              </div>
            </div>
          </div>

          <!-- Request list -->
          <div class="space-y-3">
            <div v-if="loading" class="flex justify-center py-12 text-gray-400">
              <i class="pi pi-spin pi-spinner mr-2"></i> กำลังโหลด...
            </div>
            <div v-else-if="requests.length === 0"
              class="bg-white rounded-2xl border border-gray-100 p-12 flex flex-col items-center text-gray-400">
              <i class="pi pi-inbox text-5xl mb-3 opacity-40"></i>
              <p class="font-medium">ยังไม่มีคำร้อง</p>
              <p class="text-sm mt-1">กดปุ่ม "แจ้งปัญหา / ขอรับบริการ" เพื่อส่งคำร้องแรก</p>
            </div>
            <div v-for="req in requests" :key="req.id"
              class="bg-white rounded-2xl border border-gray-100 p-5 hover:shadow-md hover:-translate-y-0.5 transition-all duration-200 cursor-pointer group"
              @click="openChat(req)">
              <div class="flex items-start gap-4">
                <div class="w-10 h-10 bg-orange-50 rounded-xl flex items-center justify-center shrink-0 group-hover:bg-orange-500 transition-colors">
                  <i class="pi pi-ticket text-orange-500 group-hover:text-white transition-colors"></i>
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2 flex-wrap">
                    <h3 class="font-bold text-gray-800 group-hover:text-orange-600 transition-colors">{{ req.title }}</h3>
                    <Tag :severity="priorityTag(req.priority).severity" :value="priorityTag(req.priority).label" class="text-xs" />
                    <Tag :severity="statusTag(req.status).severity" :value="statusTag(req.status).label" class="text-xs" />
                  </div>
                  <p class="text-sm text-gray-500 mt-1 truncate">{{ req.description }}</p>
                  <div class="flex items-center gap-3 mt-2 text-xs text-gray-400">
                    <span><i class="pi pi-tag mr-1"></i>{{ categoryLabel(req.category) }}</span>
                    <span v-if="req.extension"><i class="pi pi-phone mr-1"></i>เบอร์ {{ req.extension }}</span>
                    <span><i class="pi pi-calendar mr-1"></i>{{ formatDate(req.createdAt) }}</span>
                  </div>
                  <div v-if="req.note" class="mt-2 bg-blue-50 rounded-lg px-3 py-2 text-sm text-blue-700">
                    <i class="pi pi-comment mr-1.5 text-blue-400"></i>
                    <strong>ผู้ดูแล:</strong> {{ req.note }}
                  </div>
                </div>
                <div class="flex items-center gap-1.5 shrink-0 text-gray-300 group-hover:text-orange-500 transition-colors">
                  <span class="text-xs font-medium text-gray-400 group-hover:text-orange-500">ดูการสนทนา</span>
                  <i class="pi pi-chevron-right text-sm"></i>
                </div>
              </div>
            </div>
          </div>
        </template>

        <!-- ══ แท็บ: แชทกับผู้รับผิดชอบ ══ -->
        <template v-else>
          <div>
            <h2 class="text-2xl font-bold text-gray-900">แชทกับผู้รับผิดชอบ</h2>
            <p class="text-sm text-gray-400 mt-1">เบอร์โทรศัพท์ที่คุณรับผิดชอบ — ผู้ใช้งานสามารถแชทถามคำถามได้โดยตรง</p>
          </div>

          <div class="space-y-3">
            <div v-if="loadingLinked" class="flex justify-center py-12 text-gray-400">
              <i class="pi pi-spin pi-spinner mr-2"></i> กำลังโหลด...
            </div>
            <div v-else-if="linkedDirectories.length === 0"
              class="bg-white rounded-2xl border border-gray-100 p-12 flex flex-col items-center text-gray-400">
              <i class="pi pi-link text-5xl mb-3 opacity-40"></i>
              <p class="font-medium">ยังไม่ได้ผูกเบอร์โทรศัพท์</p>
              <p class="text-sm mt-1">ผู้ดูแลระบบต้องผูกบัญชีของคุณกับเบอร์โทรศัพท์ก่อน</p>
            </div>
            <div
              v-for="dir in linkedDirectories"
              :key="dir.id"
              class="bg-white rounded-2xl border p-5 hover:shadow-md hover:-translate-y-0.5 transition-all duration-200 cursor-pointer group"
              :class="unreadDirIds.has(dir.id) ? 'border-teal-300 bg-teal-50/30' : 'border-gray-100'"
              @click="markSeen(dir.id); router.push(`/ipphone/directory/${dir.id}`)"
            >
              <div class="flex items-center gap-4">
                <!-- เบอร์โทร -->
                <div class="w-14 h-14 bg-teal-50 rounded-xl flex items-center justify-center shrink-0 group-hover:bg-teal-600 transition-colors">
                  <span class="font-mono font-black text-teal-600 text-base group-hover:text-white transition-colors">
                    {{ dir.ipPhoneNumber }}
                  </span>
                </div>
                <!-- ข้อมูล -->
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="flex items-center gap-1 text-sm text-gray-600">
                      <i class="pi pi-building text-xs text-gray-400"></i>
                      {{ dir.departmentName }}
                    </span>
                    <span v-if="dir.workgroup" class="flex items-center gap-1 text-sm text-gray-600">
                      <i class="pi pi-users text-xs text-gray-400"></i>
                      {{ dir.workgroup }}
                    </span>
                    <span v-if="unreadDirIds.has(dir.id)"
                      class="text-[10px] bg-teal-500 text-white font-bold px-2 py-0.5 rounded-full">
                      ข้อความใหม่
                    </span>
                  </div>
                </div>
                <!-- arrow -->
                <div class="flex items-center gap-1.5 shrink-0 text-gray-300 group-hover:text-teal-500 transition-colors">
                  <span class="text-xs font-medium text-gray-400 group-hover:text-teal-500">เปิดแชท</span>
                  <i class="pi pi-chevron-right text-sm"></i>
                </div>
              </div>
            </div>
          </div>
        </template>

      </div>
    </main>

    <!-- ── Submit Dialog ── -->
    <Dialog v-model:visible="formVisible" header="แจ้งปัญหา / ขอรับบริการ" modal :style="{ width: '40rem' }">
      <div class="space-y-4">
        <Message v-if="formError" severity="error" :closable="false">{{ formError }}</Message>

        <div class="grid grid-cols-2 gap-4">
          <div class="col-span-2 flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">หัวข้อ <span class="text-red-500">*</span></label>
            <InputText v-model="form.title" placeholder="สรุปปัญหาหรือสิ่งที่ต้องการ" />
          </div>

          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">ประเภท</label>
            <Select
              v-model="form.category"
              :options="categoryOptions"
              option-label="label"
              option-value="value"
            />
          </div>

          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">ความสำคัญ</label>
            <Select
              v-model="form.priority"
              :options="priorityOptions"
              option-label="label"
              option-value="value"
            />
          </div>

          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">เบอร์ต่อที่เกี่ยวข้อง (ถ้ามี)</label>
            <InputText v-model="form.extension" placeholder="เช่น 1234" />
          </div>

          <div class="col-span-2 flex flex-col gap-1.5">
            <label class="text-sm font-medium text-gray-700">รายละเอียด <span class="text-red-500">*</span></label>
            <Textarea
              v-model="form.description"
              :rows="4"
              placeholder="อธิบายปัญหาหรือสิ่งที่ต้องการให้ชัดเจน..."
              class="w-full"
            />
          </div>
        </div>

        <div class="bg-orange-50 rounded-xl p-3 flex items-start gap-2 text-sm text-orange-700">
          <i class="pi pi-info-circle mt-0.5 shrink-0"></i>
          <p>คำร้องจะถูกส่งถึงผู้ดูแลระบบทันที คุณสามารถติดตามสถานะและสนทนาเพิ่มเติมได้ในหน้า "ดูการสนทนา"</p>
        </div>
      </div>

      <template #footer>
        <Button label="ยกเลิก" text @click="formVisible = false" />
        <Button label="ส่งคำร้อง" icon="pi pi-send" :loading="saving" @click="submitRequest" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
