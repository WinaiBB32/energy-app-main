<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { collection, query, doc, addDoc, updateDoc, onSnapshot, serverTimestamp, orderBy, Timestamp, type QuerySnapshot, type QueryDocumentSnapshot, type DocumentSnapshot } from 'firebase/firestore'
import { db } from '@/firebase/config'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'
import { usePermissions } from '@/composables/usePermissions'

import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'

// ─── Types ────────────────────────────────────────────────────────────────────
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
  requesterUid?: string
  assignedTo?: string
  note?: string
  createdAt: Timestamp | null
  updatedAt?: Timestamp | null
}

interface ChatMessage {
  id: string
  text: string
  senderName: string
  senderEmail: string
  senderId: string
  senderRole: string
  createdAt: Timestamp | null
}

// ─── Setup ────────────────────────────────────────────────────────────────────
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const requestId = route.params.id as string

// ─── State ────────────────────────────────────────────────────────────────────
const request = ref<ServiceRequest | null>(null)
const messages = ref<ChatMessage[]>([])
const loadingRequest = ref(true)
const sending = ref(false)
const messageText = ref('')
const chatBottom = ref<HTMLElement | null>(null)

// Admin status update
const updatingStatus = ref(false)
const newStatus = ref('')

const statusOptions = [
  { label: 'รอดำเนินการ', value: 'pending' },
  { label: 'กำลังดำเนินการ', value: 'in_progress' },
  { label: 'เสร็จสิ้น', value: 'done' },
  { label: 'ยกเลิก', value: 'cancelled' },
]

let unsubReq: (() => void) | null = null
let unsubMsg: (() => void) | null = null

// ─── Auth ─────────────────────────────────────────────────────────────────────
const currentUser = computed(() => authStore.user)
const { isSuperAdmin, isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('ipphone')

// Can send messages: requester, admin/superadmin, or the linked user of the extension
const canChat = computed(() => {
  if (!currentUser.value || !request.value) return false
  if (isAdmin) return true
  if (request.value.requesterEmail === currentUser.value.email) return true
  // Check if this user is linked to the extension in the request
  // We check via linkedUserEmail stored in the request's extension mapping
  // (handled via isLinkedUser computed below)
  return isLinkedUser.value
})

const isLinkedUser = ref(false)

// ─── Firestore ────────────────────────────────────────────────────────────────
onMounted(async () => {
  // Listen to request doc
  unsubReq = onSnapshot(doc(db, 'ipphone_service_requests', requestId), (snap: DocumentSnapshot) => {
    if (!snap.exists()) {
      goBack()
      return
    }
    request.value = { id: snap.id, ...snap.data() } as ServiceRequest
    newStatus.value = request.value.status
    loadingRequest.value = false

    // Check if current user is linked to this extension
    checkLinkedUser(request.value.extension)

    if (unsubMsg === null) {
      unsubMsg = onSnapshot(query(
        collection(db, 'ipphone_service_requests', requestId, 'messages'),
        orderBy('createdAt', 'asc')
      ), (snap: QuerySnapshot) => {
        messages.value = snap.docs.map((d: QueryDocumentSnapshot) => ({ id: d.id, ...d.data() } as ChatMessage))
        scrollToBottom()
      })
    }
  })
})

onUnmounted(() => {
  if (unsubReq) unsubReq()
  if (unsubMsg) unsubMsg()
})

async function checkLinkedUser(extension: string) {
  if (!extension || !currentUser.value) return
  try {
    const { getDocs, where, collection: col, query: q } = await import('firebase/firestore')
    const snap = await getDocs(
      q(col(db, 'ipphone_directory'), where('ipPhoneNumber', '==', extension)),
    )
    const email = currentUser.value.email ?? ''
    snap.forEach((d) => {
      const emails: string[] = d.data().linkedUserEmails ?? []
      if (emails.includes(email)) isLinkedUser.value = true
    })
  } catch {
    // ignore — default false
  }
}

// ─── Helpers ──────────────────────────────────────────────────────────────────
function formatDate(ts: Timestamp | null | undefined): string {
  if (!ts) return ''
  return ts.toDate().toLocaleString('th-TH', {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

function formatTime(ts: Timestamp | null | undefined): string {
  if (!ts) return ''
  return ts.toDate().toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })
}

function categoryLabel(val: string): string {
  const map: Record<string, string> = {
    repair: 'แจ้งซ่อมโทรศัพท์',
    install: 'ขอติดตั้งสาย/เบอร์ใหม่',
    move: 'ขอย้ายสาย/เบอร์',
    quality: 'ปัญหาคุณภาพเสียง',
    other: 'อื่น ๆ',
  }
  return map[val] ?? val
}

function priorityTag(val: string): { severity: 'info' | 'warn' | 'danger'; label: string } {
  if (val === 'urgent') return { severity: 'warn', label: 'เร่งด่วน' }
  if (val === 'critical') return { severity: 'danger', label: 'วิกฤต' }
  return { severity: 'info', label: 'ปกติ' }
}

function statusTag(val: string): { severity: 'secondary' | 'info' | 'success' | 'danger'; label: string } {
  if (val === 'pending') return { severity: 'secondary', label: 'รอดำเนินการ' }
  if (val === 'in_progress') return { severity: 'info', label: 'กำลังดำเนินการ' }
  if (val === 'done') return { severity: 'success', label: 'เสร็จสิ้น' }
  return { severity: 'danger', label: 'ยกเลิก' }
}

function isMine(msg: ChatMessage): boolean {
  return msg.senderId === currentUser.value?.uid
}

function scrollToBottom() {
  nextTick(() => {
    if (chatBottom.value) {
      chatBottom.value.scrollIntoView({ behavior: 'smooth' })
    }
  })
}

// ─── Actions ──────────────────────────────────────────────────────────────────
async function sendMessage() {
  const text = messageText.value.trim()
  if (!text || !currentUser.value) return
  sending.value = true
  try {
    await addDoc(
      collection(db, 'ipphone_service_requests', requestId, 'messages'),
      {
        text,
        senderName: currentUser.value.displayName ?? currentUser.value.email ?? '',
        senderEmail: currentUser.value.email ?? '',
        senderId: currentUser.value.uid,
        senderRole: authStore.userProfile?.role ?? 'user',
        createdAt: serverTimestamp(),
      },
    )
    // Update request updatedAt
    await updateDoc(doc(db, 'ipphone_service_requests', requestId), {
      updatedAt: serverTimestamp(),
    })
    messageText.value = ''
  } catch (e) {
    alert(`ส่งข้อความไม่สำเร็จ: ${String(e)}`)
  } finally {
    sending.value = false
  }
}

function handleEnter(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    sendMessage()
  }
}

function goBack() {
  // superadmin กลับไปหน้าจัดการ, user ทั่วไปกลับไปหน้าคำร้องของตัวเอง
  if (isSuperAdmin.value) {
    router.push('/ipphone/service')
  } else {
    router.push('/support')
  }
}

async function updateStatus() {
  if (!newStatus.value || !request.value) return
  updatingStatus.value = true
  try {
    const prevStatus = request.value.status
    await updateDoc(doc(db, 'ipphone_service_requests', requestId), {
      status: newStatus.value,
      updatedAt: serverTimestamp(),
    })
    logAudit(
      { uid: currentUser.value?.uid ?? '', displayName: authStore.userProfile?.displayName ?? currentUser.value?.email ?? '', email: currentUser.value?.email ?? '', role: authStore.userProfile?.role ?? 'user' },
      'UPDATE', 'ServiceChat', `อัปเดตสถานะ [${requestId}] ${prevStatus} → ${newStatus.value}`,
    )
    // Send system message
    await addDoc(
      collection(db, 'ipphone_service_requests', requestId, 'messages'),
      {
        text: `[ระบบ] อัปเดตสถานะเป็น: ${statusOptions.find((s) => s.value === newStatus.value)?.label}`,
        senderName: currentUser.value?.displayName ?? 'Admin',
        senderEmail: currentUser.value?.email ?? '',
        senderId: currentUser.value?.uid ?? '',
        senderRole: 'system',
        createdAt: serverTimestamp(),
      },
    )
  } catch (e) {
    alert(`อัปเดตสถานะไม่สำเร็จ: ${String(e)}`)
  } finally {
    updatingStatus.value = false
  }
}
</script>

<template>
  <div class="flex flex-col h-full -m-6">
    <!-- Loading -->
    <div v-if="loadingRequest" class="flex items-center justify-center h-64 text-gray-400">
      <i class="pi pi-spin pi-spinner mr-2"></i> กำลังโหลด...
    </div>

    <template v-else-if="request">
      <!-- ── Top bar ── -->
      <div class="bg-white border-b border-gray-100 px-6 py-4 shrink-0">
        <div class="flex items-start justify-between gap-4">
          <div class="flex items-center gap-3">
            <Button icon="pi pi-arrow-left" text rounded severity="secondary" @click="goBack()" />
            <div>
              <div class="flex items-center gap-2 flex-wrap">
                <h1 class="text-lg font-bold text-gray-900">{{ request.title }}</h1>
                <Tag :severity="priorityTag(request.priority).severity" :value="priorityTag(request.priority).label" />
                <Tag :severity="statusTag(request.status).severity" :value="statusTag(request.status).label" />
              </div>
              <p class="text-sm text-gray-400 mt-0.5">
                {{ categoryLabel(request.category) }}
                <span v-if="request.extension"> · เบอร์ <span class="font-mono font-semibold">{{ request.extension
                }}</span></span>
                · แจ้งโดย <strong>{{ request.requesterName }}</strong>
                · {{ formatDate(request.createdAt) }}
              </p>
            </div>
          </div>

          <!-- Admin: quick status update -->
          <div v-if="isAdmin" class="flex items-center gap-2 shrink-0">
            <Select v-model="newStatus" :options="statusOptions" option-label="label" option-value="value"
              class="w-44" />
            <Button label="อัปเดต" icon="pi pi-check" size="small" :loading="updatingStatus"
              :disabled="newStatus === request.status" @click="updateStatus" />
          </div>
        </div>

        <!-- Request description -->
        <div class="mt-3 bg-gray-50 rounded-xl px-4 py-3 text-sm text-gray-700 leading-relaxed">
          <span class="text-xs font-semibold text-gray-400 uppercase tracking-wide block mb-1">รายละเอียดปัญหา</span>
          {{ request.description }}
        </div>
      </div>

      <!-- ── Chat area ── -->
      <div class="flex-1 overflow-y-auto px-6 py-4 space-y-3 bg-gray-50/50">
        <!-- Empty state -->
        <div v-if="messages.length === 0" class="flex flex-col items-center justify-center py-16 text-gray-400">
          <i class="pi pi-comments text-4xl mb-3"></i>
          <p class="text-sm">ยังไม่มีข้อความ — เริ่มต้นสนทนาเพื่อติดตามงานนี้</p>
        </div>

        <!-- Messages -->
        <div v-for="msg in messages" :key="msg.id" :class="[
          'flex gap-2',
          msg.senderRole === 'system' ? 'justify-center' : isMine(msg) ? 'flex-row-reverse' : 'flex-row',
        ]">
          <!-- System message -->
          <div v-if="msg.senderRole === 'system'" class="w-full text-center">
            <span class="text-xs text-gray-400 bg-gray-200/70 rounded-full px-3 py-1">{{ msg.text }}</span>
          </div>

          <!-- Regular message -->
          <template v-else>
            <!-- Avatar -->
            <div class="w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold shrink-0 mt-1"
              :class="isMine(msg) ? 'bg-indigo-600 text-white' : 'bg-slate-200 text-slate-600'">
              {{ msg.senderName.charAt(0).toUpperCase() }}
            </div>

            <!-- Bubble -->
            <div :class="['max-w-sm lg:max-w-md', isMine(msg) ? 'items-end' : 'items-start', 'flex flex-col']">
              <div class="flex items-baseline gap-2 mb-1" :class="isMine(msg) ? 'flex-row-reverse' : ''">
                <span class="text-xs font-semibold text-gray-700">{{ msg.senderName }}</span>
                <span v-if="isSystemAdmin('ipphone')" class="text-xs text-indigo-500 font-medium">[Admin]</span>
                <span class="text-xs text-gray-400">{{ formatTime(msg.createdAt) }}</span>
              </div>
              <div class="rounded-2xl px-4 py-2.5 text-sm leading-relaxed whitespace-pre-wrap" :class="isMine(msg)
                ? 'bg-indigo-600 text-white rounded-tr-sm'
                : 'bg-white border border-gray-200 text-gray-800 rounded-tl-sm shadow-sm'">
                {{ msg.text }}
              </div>
            </div>
          </template>
        </div>

        <!-- Scroll anchor -->
        <div ref="chatBottom" class="h-1"></div>
      </div>

      <!-- ── Input area ── -->
      <div class="bg-white border-t border-gray-100 px-6 py-4 shrink-0">
        <!-- Cannot chat notice -->
        <div v-if="!canChat" class="text-center text-sm text-gray-400 py-2">
          <i class="pi pi-lock mr-1"></i>
          เฉพาะผู้แจ้ง, ผู้ดูแลระบบ และผู้ที่รับผิดชอบเบอร์นี้เท่านั้นที่สามารถส่งข้อความได้
        </div>

        <div v-else class="flex gap-3 items-end">
          <Textarea v-model="messageText" :rows="2"
            placeholder="พิมพ์ข้อความ... (Enter ส่ง, Shift+Enter ขึ้นบรรทัดใหม่)" class="flex-1 resize-none"
            @keydown="handleEnter" />
          <Button icon="pi pi-send" :loading="sending" :disabled="!messageText.trim()" @click="sendMessage"
            class="mb-0.5" />
        </div>
      </div>
    </template>
  </div>
</template>
