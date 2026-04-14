<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import api from '@/services/api'
import Card from 'primevue/card'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Tag from 'primevue/tag'

interface MonitorSummary {
  generatedAtUtc: string
  api: {
    status: string
    environment: string
    machineName: string
    processId: number
    workingSetMb: number
    threadCount: number
    runtime: string
    uptimeSeconds: number
  }
  database: {
    status: string
    canConnect: boolean
    responseTimeMs: number
    provider: string
    error?: string | null
  }
  errors: {
    count24h: number
    recent: Array<{
      occurredAtUtc: string
      method: string
      path: string
      statusCode: number
      message: string
      exceptionType?: string | null
      traceId: string
    }>
  }
}

const loading = ref(false)
const lastError = ref('')
const summary = ref<MonitorSummary | null>(null)
const backendPingMs = ref<number | null>(null)
const timerId = ref<number | null>(null)

const frontendStatus = computed(() => ({
  online: navigator.onLine,
  language: navigator.language,
  platform: navigator.platform,
  userAgent: navigator.userAgent,
  deviceMemory: (navigator as Navigator & { deviceMemory?: number }).deviceMemory,
  hardwareConcurrency: navigator.hardwareConcurrency,
}))

const backendHealthSeverity = computed(() => {
  if (!summary.value) return 'secondary'
  if (summary.value.database.status !== 'healthy') return 'danger'
  if ((backendPingMs.value ?? 0) > 1500 || summary.value.database.responseTimeMs > 1000) return 'warn'
  return 'success'
})

const backendHealthLabel = computed(() => {
  if (!summary.value) return 'ยังไม่ตรวจสอบ'
  if (summary.value.database.status !== 'healthy') return 'พบปัญหา'
  if ((backendPingMs.value ?? 0) > 1500 || summary.value.database.responseTimeMs > 1000) return 'ช้ากว่าปกติ'
  return 'ปกติ'
})

const recentErrors = computed(() => summary.value?.errors.recent ?? [])

function formatDate(value?: string): string {
  if (!value) return '-'
  return new Date(value).toLocaleString('th-TH', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}

function formatUptime(seconds?: number): string {
  if (!seconds && seconds !== 0) return '-'
  const d = Math.floor(seconds / 86400)
  const h = Math.floor((seconds % 86400) / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  return `${d}d ${h}h ${m}m`
}

async function refreshMonitoring() {
  loading.value = true
  lastError.value = ''
  const start = performance.now()
  try {
    const response = await api.get('/SystemMonitor/summary')
    backendPingMs.value = Math.round(performance.now() - start)
    summary.value = response.data as MonitorSummary
  } catch (error) {
    backendPingMs.value = null
    summary.value = null
    lastError.value = `ไม่สามารถเชื่อมต่อ Backend Monitoring API ได้: ${String(error)}`
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await refreshMonitoring()
  timerId.value = window.setInterval(refreshMonitoring, 30000)
})

onUnmounted(() => {
  if (timerId.value) {
    window.clearInterval(timerId.value)
    timerId.value = null
  }
})
</script>

<template>
  <div class="max-w-6xl mx-auto pb-10 space-y-6">
    <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          <i class="pi pi-server text-indigo-500 mr-2"></i>ระบบตรวจสอบการใช้งาน Server
        </h2>
        <p class="text-gray-500 mt-1">มอนิเตอร์ Frontend + Backend + Database เพื่อเฝ้าระวังและรับมือข้อผิดพลาดของระบบ</p>
      </div>
      <Button icon="pi pi-refresh" label="รีเฟรชทันที" :loading="loading" @click="refreshMonitoring" />
    </div>

    <Message v-if="lastError" severity="error" :closable="false">{{ lastError }}</Message>

    <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
      <Card class="shadow-sm border-t-4 border-blue-200">
        <template #content>
          <p class="text-xs uppercase text-gray-500 font-semibold">Frontend Status</p>
          <div class="mt-2 flex items-center gap-2">
            <Tag :value="frontendStatus.online ? 'Online' : 'Offline'" :severity="frontendStatus.online ? 'success' : 'danger'" />
            <span class="text-sm text-gray-600">{{ frontendStatus.platform }}</span>
          </div>
          <p class="text-xs text-gray-500 mt-2">CPU Threads: {{ frontendStatus.hardwareConcurrency ?? '-' }}</p>
          <p class="text-xs text-gray-500">Device Memory: {{ frontendStatus.deviceMemory ?? '-' }} GB</p>
        </template>
      </Card>

      <Card class="shadow-sm border-t-4 border-indigo-200">
        <template #content>
          <p class="text-xs uppercase text-gray-500 font-semibold">Backend API</p>
          <div class="mt-2 flex items-center gap-2">
            <Tag :value="backendHealthLabel" :severity="backendHealthSeverity as any" />
            <span class="text-sm text-gray-600">Ping: {{ backendPingMs ?? '-' }} ms</span>
          </div>
          <p class="text-xs text-gray-500 mt-2">Environment: {{ summary?.api.environment ?? '-' }}</p>
          <p class="text-xs text-gray-500">Uptime: {{ formatUptime(summary?.api.uptimeSeconds) }}</p>
        </template>
      </Card>

      <Card class="shadow-sm border-t-4 border-emerald-200">
        <template #content>
          <p class="text-xs uppercase text-gray-500 font-semibold">Database Connectivity</p>
          <div class="mt-2 flex items-center gap-2">
            <Tag :value="summary?.database.canConnect ? 'Connected' : 'Disconnected'" :severity="summary?.database.canConnect ? 'success' : 'danger'" />
            <span class="text-sm text-gray-600">{{ summary?.database.responseTimeMs ?? '-' }} ms</span>
          </div>
          <p class="text-xs text-gray-500 mt-2">Provider: {{ summary?.database.provider ?? '-' }}</p>
          <p v-if="summary?.database.error" class="text-xs text-red-500">{{ summary.database.error }}</p>
        </template>
      </Card>
    </div>

    <Card>
      <template #title>รายละเอียดเชิงเทคนิค</template>
      <template #content>
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 text-sm">
          <div class="rounded-xl border border-gray-200 p-4">
            <h3 class="font-semibold text-gray-800 mb-2">Backend Runtime</h3>
            <p class="text-gray-600">Machine: {{ summary?.api.machineName ?? '-' }}</p>
            <p class="text-gray-600">Process ID: {{ summary?.api.processId ?? '-' }}</p>
            <p class="text-gray-600">Memory: {{ summary?.api.workingSetMb ?? '-' }} MB</p>
            <p class="text-gray-600">Threads: {{ summary?.api.threadCount ?? '-' }}</p>
            <p class="text-gray-600">.NET Runtime: {{ summary?.api.runtime ?? '-' }}</p>
            <p class="text-gray-500 text-xs mt-2">Updated: {{ formatDate(summary?.generatedAtUtc) }}</p>
          </div>
          <div class="rounded-xl border border-gray-200 p-4">
            <h3 class="font-semibold text-gray-800 mb-2">Frontend Diagnostics</h3>
            <p class="text-gray-600">Language: {{ frontendStatus.language }}</p>
            <p class="text-gray-600 break-all">User Agent: {{ frontendStatus.userAgent }}</p>
            <p class="text-gray-600 mt-2">แนวทางรับมือข้อผิดพลาด:</p>
            <ul class="text-gray-600 list-disc list-inside">
              <li>หาก Backend Ping > 1500 ms ให้ตรวจ log และ network</li>
              <li>หาก DB disconnected ให้ตรวจ connection string และ DB service</li>
              <li>หาก Frontend offline ให้ตรวจ reverse proxy หรือเครือข่ายองค์กร</li>
            </ul>
          </div>
        </div>
      </template>
    </Card>

    <Card>
      <template #title>Error Logs ล่าสุด</template>
      <template #content>
        <div class="mb-3 flex items-center gap-2">
          <Tag :value="`Errors 24h: ${summary?.errors.count24h ?? 0}`" severity="danger" />
          <span class="text-xs text-gray-500">เก็บไว้ในหน่วยความจำของ API (in-memory) ล่าสุดสูงสุด 500 รายการ</span>
        </div>

        <div v-if="recentErrors.length === 0" class="rounded-lg border border-green-200 bg-green-50 p-3 text-sm text-green-700">
          ยังไม่พบข้อผิดพลาดระดับเซิร์ฟเวอร์ในช่วงที่ระบบกำลังรัน
        </div>

        <div v-else class="space-y-2 max-h-[24rem] overflow-y-auto pr-1">
          <div v-for="(err, index) in recentErrors" :key="`${err.traceId}-${index}`" class="rounded-lg border border-red-200 bg-red-50 p-3">
            <div class="flex flex-wrap items-center gap-2 mb-1">
              <Tag :value="`${err.statusCode}`" severity="danger" />
              <span class="text-xs font-semibold text-red-800">{{ err.method }} {{ err.path }}</span>
              <span class="text-xs text-red-500">{{ formatDate(err.occurredAtUtc) }}</span>
            </div>
            <p class="text-sm text-red-700">{{ err.message }}</p>
            <p v-if="err.exceptionType" class="text-xs text-red-600 mt-1">Exception: {{ err.exceptionType }}</p>
            <p class="text-xs text-red-500 mt-1">Trace ID: {{ err.traceId }}</p>
          </div>
        </div>

        <div class="mt-4 rounded-lg border border-gray-200 bg-gray-50 p-3 text-xs text-gray-700">
          <p class="font-semibold mb-1">ข้อมูลที่ระบบเก็บเมื่อเกิดข้อผิดพลาด</p>
          <p>1. เวลาเกิดเหตุ (UTC)</p>
          <p>2. HTTP Method และ Path ที่เรียก</p>
          <p>3. Status Code (เช่น 500)</p>
          <p>4. ข้อความ error</p>
          <p>5. ชนิด Exception (ถ้ามี)</p>
          <p>6. Trace ID สำหรับตาม log ต่อในเซิร์ฟเวอร์</p>
        </div>
      </template>
    </Card>
  </div>
</template>
