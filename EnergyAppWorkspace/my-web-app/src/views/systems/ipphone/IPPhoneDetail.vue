<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  doc,
  collection,
  query,
  orderBy,
  onSnapshot,
  addDoc,
  serverTimestamp,
  Timestamp,
} from 'firebase/firestore'
// Firebase Removed
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Tag from 'primevue/tag'

// ─── Interfaces ───────────────────────────────────────────────────────────────
interface LinkedUser {
  uid: string
  displayName: string
  email: string
}

interface IPPhoneDirectory {
  ownerName: string
  location: string
  ipPhoneNumber: string
  analogNumber: string
  deviceCode: string
  departmentId: string
  workgroup: string
  description: string
  keywords: string
  isPublished: boolean
  // ข้อมูลผูกผู้ใช้งาน (รองรับหลาย user)
  linkedUsers?: LinkedUser[]
  linkedUserEmails?: string[]
}

interface ChatMessage {
  id: string
  text: string
  senderUid: string
  senderName: string
  senderRole: string
  createdAt: Timestamp | null
}

interface Department {
  id: string
  name: string
}

// ─── Setup ────────────────────────────────────────────────────────────────────
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const directoryId = route.params.id as string
const directoryData = ref<IPPhoneDirectory | null>(null)
const departments = ref<Department[]>([])
const messages = ref<ChatMessage[]>([])
const newMessage = ref<string>('')
const isSending = ref<boolean>(false)
const chatContainer = ref<HTMLElement | null>(null)
const isLoading = ref<boolean>(true)
const toast = useAppToast()

let unsubDoc: (() => void) | null = null
let unsubMsgs: (() => void) | null = null
let unsubDepts: (() => void) | null = null

// ─── Auth ─────────────────────────────────────────────────────────────────────
const currentUid = computed(() => authStore.user?.uid ?? '')
const currentEmail = computed(() => authStore.user?.email ?? '')
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('ipphone')

// ผู้ใช้ที่ผูกกับเบอร์นี้
const isLinkedUser = computed(() => {
  const linked = directoryData.value?.linkedUsers ?? []
  return linked.some((u) => u.uid === currentUid.value || u.email === currentEmail.value)
})

// ทุกคนที่ login แล้วแชทได้ (ถามผู้ครอบครองเบอร์ได้เลย)
const canChat = computed(() => !!authStore.user)

// ─── Firestore ────────────────────────────────────────────────────────────────
onMounted(() => {
  // Mark this chat as seen so badge clears in UserSupportView
  localStorage.setItem(`ipphone_chat_seen_${directoryId}`, Date.now().toString())

  unsubDepts = onSnapshot(query(collection(db, 'departments')), (snap) => {
    departments.value = snap.docs.map((d) => ({ id: d.id, name: d.data().name as string }))
  })

  unsubDoc = onSnapshot(doc(db, 'ipphone_directory', directoryId), (docSnap) => {
    if (docSnap.exists()) {
      directoryData.value = docSnap.data() as IPPhoneDirectory
    } else {
      alert('ไม่พบข้อมูลหมายเลขนี้')
      router.push('/ipphone')
    }
    isLoading.value = false
  })

  const msgQuery = query(
    collection(db, 'ipphone_directory', directoryId, 'messages'),
    orderBy('createdAt', 'asc'),
  )
  unsubMsgs = onSnapshot(msgQuery, (snap) => {
    messages.value = snap.docs.map((d) => ({ id: d.id, ...d.data() } as ChatMessage))
    scrollToBottom()
  })
})

onUnmounted(() => {
  if (unsubDepts) unsubDepts()
  if (unsubDoc) unsubDoc()
  if (unsubMsgs) unsubMsgs()
})

// ─── Helpers ──────────────────────────────────────────────────────────────────
const getDeptName = (id: string): string =>
  departments.value.find((d) => d.id === id)?.name ?? id

const formatChatTime = (ts: Timestamp | null | undefined): string => {
  if (!ts) return 'กำลังส่ง...'
  const date = ts.toDate()
  return (
    date.toLocaleDateString('th-TH', { day: '2-digit', month: 'short' }) +
    ' ' +
    date.toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })
  )
}

const scrollToBottom = async (): Promise<void> => {
  await nextTick()
  if (chatContainer.value) {
    chatContainer.value.scrollTop = chatContainer.value.scrollHeight
  }
}

// ─── Actions ──────────────────────────────────────────────────────────────────
const sendMessage = async (): Promise<void> => {
  if (!newMessage.value.trim() || !canChat.value) return
  isSending.value = true
  try {
    await addDoc(collection(db, 'ipphone_directory', directoryId, 'messages'), {
      text: newMessage.value.trim(),
      senderUid: authStore.user?.uid ?? 'unknown',
      senderName: authStore.userProfile?.displayName ?? authStore.user?.email ?? 'ไม่ระบุชื่อ',
      senderRole: authStore.userProfile?.role ?? 'user',
      createdAt: serverTimestamp(),
    })
    newMessage.value = ''
    scrollToBottom()
  } catch (e: unknown) {
    toast.fromError(e, 'ไม่สามารถส่งข้อความได้ กรุณาลองใหม่')
  } finally {
    isSending.value = false
  }
}
</script>

<template>
  <div class="max-w-6xl mx-auto pb-10 h-[calc(100vh-6rem)] flex flex-col">
    <!-- Header -->
    <div class="mb-4 flex items-center justify-between shrink-0">
      <div class="flex items-center gap-3">
        <Button icon="pi pi-arrow-left" severity="secondary" text rounded @click="router.back()" />
        <div>
          <h2 class="text-2xl font-bold text-gray-800">รายละเอียดหมายเลข IP-Phone</h2>
          <p class="text-sm text-gray-500">ข้อมูลเบอร์โทรศัพท์ และแชทกับผู้รับผิดชอบ</p>
        </div>
        <!-- badge: linked user -->
        <span
          v-if="isLinkedUser"
          class="ml-2 inline-flex items-center gap-1.5 bg-teal-100 text-teal-700 text-xs font-semibold px-3 py-1 rounded-full"
        >
          <i class="pi pi-check-circle text-xs"></i>
          คุณเป็นผู้รับผิดชอบเบอร์นี้
        </span>
      </div>
    </div>

    <div v-if="isLoading" class="flex justify-center items-center flex-1">
      <i class="pi pi-spin pi-spinner text-teal-500 text-4xl"></i>
    </div>

    <div v-else class="flex flex-col lg:flex-row gap-6 flex-1 min-h-0">
      <!-- ── Left panel: ข้อมูลเบอร์ ── -->
      <div class="w-full lg:w-1/3 flex flex-col gap-4 overflow-y-auto pr-2 custom-scrollbar">
        <Card class="shadow-sm border-none">
          <template #content>
            <!-- Avatar / เบอร์ -->
            <div class="flex flex-col items-center text-center pb-6 border-b border-gray-100">
              <div class="w-20 h-20 bg-teal-100 text-teal-600 rounded-full flex items-center justify-center text-3xl font-bold mb-3">
                <i class="pi pi-phone"></i>
              </div>
              <h3 class="text-3xl font-black text-teal-600 tracking-widest">
                {{ directoryData?.ipPhoneNumber }}
              </h3>
              <p class="text-lg font-bold text-gray-800 mt-1">{{ directoryData?.ownerName }}</p>
              <p class="text-sm text-gray-500">{{ getDeptName(directoryData?.departmentId ?? '') }}</p>
            </div>

            <!-- ข้อมูลเพิ่มเติม -->
            <div class="flex flex-col gap-4 mt-6">
              <div>
                <p class="text-xs text-gray-400 uppercase font-semibold">กลุ่มงาน / แผนก</p>
                <p class="font-medium text-gray-800">{{ directoryData?.workgroup || '-' }}</p>
              </div>
              <div>
                <p class="text-xs text-gray-400 uppercase font-semibold">สถานที่ติดตั้ง</p>
                <p class="font-medium text-gray-800">{{ directoryData?.location || '-' }}</p>
              </div>
              <div>
                <p class="text-xs text-gray-400 uppercase font-semibold">เบอร์ Analog</p>
                <p class="font-medium text-gray-800">{{ directoryData?.analogNumber || '-' }}</p>
              </div>
              <div>
                <p class="text-xs text-gray-400 uppercase font-semibold">รหัสเครื่อง (MAC/Serial)</p>
                <p class="font-medium text-gray-800 font-mono text-sm">{{ directoryData?.deviceCode || '-' }}</p>
              </div>
              <div>
                <p class="text-xs text-gray-400 uppercase font-semibold mb-1">Keywords</p>
                <div class="flex flex-wrap gap-1">
                  <Tag v-if="directoryData?.keywords" :value="directoryData.keywords" severity="secondary" rounded />
                  <span v-else class="text-gray-800 font-medium">-</span>
                </div>
              </div>
              <div v-if="directoryData?.description">
                <p class="text-xs text-gray-400 uppercase font-semibold">รายละเอียดเพิ่มเติม</p>
                <p class="text-sm text-gray-700 bg-gray-50 p-3 rounded-lg mt-1 border border-gray-100">
                  {{ directoryData.description }}
                </p>
              </div>

              <!-- ── ผู้รับผิดชอบเบอร์นี้ ── -->
              <div class="pt-2 border-t border-gray-100">
                <p class="text-xs text-gray-400 uppercase font-semibold mb-2">ผู้รับผิดชอบเบอร์นี้</p>
                <div v-if="(directoryData?.linkedUsers ?? []).length > 0" class="flex flex-col gap-1.5">
                  <div
                    v-for="u in directoryData!.linkedUsers"
                    :key="u.uid"
                    class="flex items-center gap-2 bg-indigo-50 rounded-xl px-3 py-2.5"
                  >
                    <div class="w-8 h-8 bg-indigo-200 rounded-full flex items-center justify-center shrink-0">
                      <i class="pi pi-user text-indigo-700 text-sm"></i>
                    </div>
                    <div>
                      <p class="text-sm font-semibold text-indigo-800">{{ u.displayName }}</p>
                      <p class="text-xs text-indigo-500">{{ u.email }}</p>
                    </div>
                  </div>
                </div>
                <div v-else class="flex items-center gap-2 bg-gray-50 rounded-xl px-3 py-2.5">
                  <i class="pi pi-user text-gray-300 text-sm"></i>
                  <p class="text-sm text-gray-400">ยังไม่ได้กำหนดผู้รับผิดชอบ</p>
                </div>
              </div>
            </div>
          </template>
        </Card>
      </div>

      <!-- ── Right panel: แชท ── -->
      <Card class="shadow-sm border-none w-full lg:w-2/3 flex flex-col min-h-0 bg-gray-50/50">
        <template #title>
          <div class="flex items-center justify-between border-b border-gray-200 pb-3">
            <div class="flex items-center gap-2">
              <i class="pi pi-comments text-teal-500"></i>
              <span class="text-lg font-bold text-gray-800">แชทกับผู้รับผิดชอบ</span>
            </div>
            <span
              v-if="isAdmin"
              class="text-xs bg-amber-100 text-amber-700 px-2 py-1 rounded-full font-medium"
            >Admin</span>
            <span
              v-else-if="isLinkedUser"
              class="text-xs bg-teal-100 text-teal-700 px-2 py-1 rounded-full font-medium"
            >ผู้รับผิดชอบ</span>
          </div>
        </template>
        <template #content>
          <div class="flex flex-col h-full h-[500px]">
            <!-- Messages -->
            <div
              class="flex-1 overflow-y-auto p-4 flex flex-col gap-4 custom-scrollbar"
              ref="chatContainer"
            >
              <div v-if="messages.length === 0" class="h-full flex flex-col items-center justify-center text-gray-400">
                <i class="pi pi-comment text-4xl mb-2 opacity-50"></i>
                <p class="text-sm">ยังไม่มีประวัติการสนทนา</p>
              </div>

              <div
                v-for="msg in messages"
                :key="msg.id"
                class="flex flex-col w-full"
                :class="msg.senderUid === currentUid ? 'items-end' : 'items-start'"
              >
                <div
                  class="text-[10px] text-gray-400 mb-1 flex items-center gap-1"
                  :class="msg.senderUid === currentUid ? 'flex-row-reverse' : 'flex-row'"
                >
                  <span class="font-bold text-gray-600">{{ msg.senderName }}</span>
                  <span
                    v-if="isSystemAdmin('ipphone')"
                    class="bg-amber-100 text-amber-700 px-1.5 rounded-sm text-[9px] font-semibold"
                  >Admin</span>
                  <span
                    v-else-if="(directoryData?.linkedUsers ?? []).some(u => u.uid === msg.senderUid)"
                    class="bg-teal-100 text-teal-700 px-1.5 rounded-sm text-[9px] font-semibold"
                  >ผู้รับผิดชอบ</span>
                  <span>•</span>
                  <span>{{ formatChatTime(msg.createdAt) }}</span>
                </div>
                <div
                  class="max-w-[80%] p-3 rounded-2xl text-sm shadow-sm"
                  :class="msg.senderUid === currentUid
                    ? 'bg-teal-500 text-white rounded-tr-none'
                    : 'bg-white border border-gray-200 text-gray-800 rounded-tl-none'"
                >
                  {{ msg.text }}
                </div>
              </div>
            </div>

            <!-- Input -->
            <div class="mt-4 pt-4 border-t border-gray-200 shrink-0">
              <form @submit.prevent="sendMessage" class="flex gap-2">
                <InputText
                  v-model="newMessage"
                  :placeholder="isLinkedUser || isAdmin ? 'ตอบคำถาม...' : 'ถามผู้รับผิดชอบเบอร์นี้...'"
                  class="flex-1 rounded-full px-4"
                />
                <Button
                  type="submit"
                  icon="pi pi-send"
                  severity="help"
                  rounded
                  :loading="isSending"
                  :disabled="!newMessage.trim()"
                  aria-label="Send"
                />
              </form>
            </div>
          </div>
        </template>
      </Card>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background-color: #cbd5e1; border-radius: 20px; }
:deep(.p-card-body) { height: 100%; display: flex; flex-direction: column; }
:deep(.p-card-content) { flex: 1; min-height: 0; padding-bottom: 0 !important; }
</style>
