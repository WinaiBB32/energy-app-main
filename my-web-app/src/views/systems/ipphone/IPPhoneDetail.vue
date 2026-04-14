<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import Button from 'primevue/button'
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
  linkedUsers?: LinkedUser[]
  linkedUserEmails?: string[]
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
const isLoading = ref<boolean>(true)
const toast = useAppToast()

// ─── Auth ─────────────────────────────────────────────────────────────────────
const currentUid = computed(() => authStore.user?.uid ?? '')
const currentEmail = computed(() => authStore.user?.email ?? '')
const { isSystemAdmin } = usePermissions()

const isLinkedUser = computed(() => {
  const linked = directoryData.value?.linkedUsers ?? []
  return linked.some((u) => u.uid === currentUid.value || u.email === currentEmail.value)
})

// ─── Data Fetching ────────────────────────────────────────────────────────────────
onMounted(async () => {
  isLoading.value = true;
  try {
    const [deptsResponse, dirResponse] = await Promise.all([
        api.get('/Department'),
        api.get(`/IPPhoneDirectory/${directoryId}`)
    ]);

    departments.value = deptsResponse.data || [];
    
    if (dirResponse.data) {
        directoryData.value = dirResponse.data;
    } else {
        toast.error('ไม่พบข้อมูลหมายเลขนี้');
        router.push('/ipphone');
    }

  } catch (error) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูลได้');
    router.push('/ipphone');
  } finally {
    isLoading.value = false;
  }
})


// ─── Helpers ──────────────────────────────────────────────────────────────────
const getDeptName = (id: string): string =>
  departments.value.find((d) => d.id === id)?.name ?? id

// Chat functionality has been removed as it requires a real-time backend implementation or a dedicated chat API,
// which is not available in the current REST API.
</script>

<template>
  <div class="max-w-xl mx-auto pb-10 flex flex-col">
    <!-- Header -->
    <div class="mb-4 flex items-center justify-between shrink-0">
      <div class="flex items-center gap-3">
        <Button icon="pi pi-arrow-left" severity="secondary" text rounded @click="router.back()" />
        <div>
          <h2 class="text-2xl font-bold text-gray-800">รายละเอียดหมายเลข IP-Phone</h2>
        </div>
        <span
          v-if="isLinkedUser"
          class="ml-2 inline-flex items-center gap-1.5 bg-teal-100 text-teal-700 text-xs font-semibold px-3 py-1 rounded-full"
        >
          <i class="pi pi-check-circle text-xs"></i>
          คุณเป็นผู้รับผิดชอบเบอร์นี้
        </span>
      </div>
    </div>

    <div v-if="isLoading" class="flex justify-center items-center flex-1 py-20">
      <i class="pi pi-spin pi-spinner text-teal-500 text-4xl"></i>
    </div>

    <div v-else class="flex flex-col gap-6 flex-1 min-h-0">
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
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background-color: #cbd5e1; border-radius: 20px; }
:deep(.p-card-body) { height: 100%; display: flex; flex-direction: column; }
:deep(.p-card-content) { flex: 1; min-height: 0; padding-bottom: 0 !important; }
</style>
