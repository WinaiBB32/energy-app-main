<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'
import type { FetchedReceipt } from '@/types'

import Card from 'primevue/card'
import Chart from 'primevue/chart'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'

defineOptions({ name: 'FuelDashboard' })
const toast = useAppToast()

// 1. Interfaces
interface FetchedFuelRecord {
  departmentId: string
  vehiclePlate: string
  fuelTypeName: string
  totalAmount: number
  liters: number
  refuelDate: string | null
}
interface FuelType { id: string; name: string }

// 2. Auth & State
const authStore = useAuthStore()
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('fuel')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

// Data stores
const fuelTypes = ref<FuelType[]>([])
const rawFuelRecords = ref<FetchedFuelRecord[]>([])
const rawReceipts = ref<FetchedReceipt[]>([])

// KPI Refs
const totalExpense = ref<number>(0)
const totalLiters = ref<number>(0)
const avgPricePerLiter = ref<number>(0)
const totalReceiptExpense = ref<number>(0)
const totalReceiptEntries = ref<number>(0)
const avgReceiptExpense = computed(() => totalReceiptEntries.value > 0 ? totalReceiptExpense.value / totalReceiptEntries.value : 0)

const isLoading = ref<boolean>(true)

// -- Date Filter --
const getLastMonthRange = (): Date[] => {
  const now = new Date();
  const first = new Date(now.getFullYear(), now.getMonth() - 1, 1);
  const last = new Date(now.getFullYear(), now.getMonth(), 0);
  return [first, last]
}
const selectedDateRange = ref<Date[] | null>(getLastMonthRange())
const thaiMonthShort = ['ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.', 'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.']
const dateRangeLabel = computed(() => {
  const r = selectedDateRange.value;
  if (!r || r.length < 2 || !r[0] || !r[1]) return 'ทุกช่วงเวลา';
  const fmt = (d: Date) => `${thaiMonthShort[d.getMonth()]} ${d.getFullYear() + 543}`;
  const s = fmt(r[0]), e = fmt(r[1]);
  return s === e ? s : `${s} – ${e}`;
})
const clearDateFilter = (): void => { selectedDateRange.value = null }

// -- Chart State --
const trendChartData = ref(); const trendChartOptions = ref()
const fuelTypeChartData = ref(); const fuelTypeChartOptions = ref()
const vehicleChartData = ref(); const vehicleChartOptions = ref()
const receiptTrendChartData = ref(); const receiptTrendChartOptions = ref()
const receiptDetailChartData = ref(); const receiptDetailChartOptions = ref()
const receiptDriverChartData = ref(); const receiptDriverChartOptions = ref()

// 3. Data Fetching
const fetchData = async (): Promise<void> => {
  isLoading.value = true
  try {
    const fromDate = selectedDateRange.value?.[0]?.toISOString()
    const toDate = selectedDateRange.value?.[1]?.toISOString()

    const params = new URLSearchParams()
    if (fromDate) params.append('fromDate', fromDate)
    if (toDate) params.append('toDate', toDate)
    params.append('take', '10000')

    const [fuelTypesResponse, fuelRecordsResponse, fuelReceiptsResponse] = await Promise.all([
      api.get('/FuelType'),
      api.get('/FuelRecord', { params }),
      api.get('/FuelReceipt', { params })
    ])

    fuelTypes.value = fuelTypesResponse.data || []
    rawFuelRecords.value = fuelRecordsResponse.data.items || []
    rawReceipts.value = fuelReceiptsResponse.data.items || []

    processData()

  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูล Dashboard ได้')
  } finally {
    isLoading.value = false
  }
}

onMounted(fetchData)
watch(selectedDateRange, fetchData)

// 4. Data Processing
const processData = (): void => {
  let tempExpense = 0, tempLiters = 0, tempReceiptExpense = 0, tempReceiptEntries = 0
  const monthlyFuelData: Record<string, { expense: number, liters: number }> = {}
  const monthlyReceiptData: Record<string, number> = {}

  rawFuelRecords.value.forEach((record) => {
    if (!isAdmin && record.departmentId !== currentUserDepartment.value) return

    const expense = record.totalAmount || 0
    const liters = record.liters || 0
    tempExpense += expense
    tempLiters += liters

    if (record.refuelDate) {
      const date = new Date(record.refuelDate)
      const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
      if (!monthlyFuelData[monthKey]) monthlyFuelData[monthKey] = { expense: 0, liters: 0 }
      monthlyFuelData[monthKey].expense += expense
      monthlyFuelData[monthKey].liters += liters
    }
  })
  
  rawReceipts.value.forEach((receipt) => {
    const expense = receipt.totalAmount || 0
    tempReceiptExpense += expense
    tempReceiptEntries += 1

     if (receipt.createdAt) {
      const date = new Date(receipt.createdAt)
      const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
      monthlyReceiptData[monthKey] = (monthlyReceiptData[monthKey] || 0) + expense
    }
  })

  totalExpense.value = tempExpense
  totalLiters.value = tempLiters
  avgPricePerLiter.value = tempLiters > 0 ? tempExpense / tempLiters : 0
  totalReceiptExpense.value = tempReceiptExpense
  totalReceiptEntries.value = tempReceiptEntries

  const vehicleExpenses: Record<string, number> = {}
  const fuelTypeExpenses: Record<string, number> = {}

  rawFuelRecords.value.forEach((record: FetchedFuelRecord) => {
    if (!isAdmin && record.departmentId !== currentUserDepartment.value) return
    const amount = record.totalAmount || 0
    vehicleExpenses[record.vehiclePlate || 'ไม่ระบุ'] = (vehicleExpenses[record.vehiclePlate || 'ไม่ระบุ'] || 0) + amount
    const fuelTypeName = record.fuelTypeName || 'ไม่ระบุ'
    fuelTypeExpenses[fuelTypeName] = (fuelTypeExpenses[fuelTypeName] || 0) + amount
  })

  const receiptDetailExpenses: Record<string, number> = {}
  const receiptDriverExpenses: Record<string, number> = {}
  rawReceipts.value.forEach((receipt: FetchedReceipt) => {
    receipt.entries?.forEach((entry) => {
      const amount = entry.amount || 0
      receiptDetailExpenses[entry.detail || 'ไม่ระบุ'] = (receiptDetailExpenses[entry.detail || 'ไม่ระบุ'] || 0) + amount
      receiptDriverExpenses[entry.driverName || 'ไม่ระบุ พขร.'] = (receiptDriverExpenses[entry.driverName || 'ไม่ระบุ พขร.'] || 0) + amount
    })
  })
  
  setupCharts(monthlyFuelData, vehicleExpenses, fuelTypeExpenses, monthlyReceiptData, receiptDetailExpenses, receiptDriverExpenses)
}

const formatChartLabel = (sortKey: string): string => {
  const parts = sortKey.split('-')
  const yearStr = parts[0] || ''
  const monthStr = parts[1] || '1'
  const monthNames = ['ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.', 'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.']
  return `${monthNames[parseInt(monthStr, 10) - 1]} ${yearStr}`
}

// 5. Chart Setup
const setupCharts = (
  monthlyFuelData: Record<string, { expense: number; liters: number }>,
  vehicleExpenses: Record<string, number>,
  fuelTypeExpenses: Record<string, number>,
  monthlyReceiptData: Record<string, number>,
  receiptDetailExpenses: Record<string, number>,
  receiptDriverExpenses: Record<string, number>,
): void => {
  const fuelSortedKeys = Object.keys(monthlyFuelData).sort()
  trendChartData.value = {
    labels: fuelSortedKeys.map(key => formatChartLabel(key)),
    datasets: [
      { type: 'bar', label: 'ค่าใช้จ่าย (บาท)', backgroundColor: '#ef4444', borderRadius: 4, yAxisID: 'y', data: fuelSortedKeys.map(key => monthlyFuelData[key]?.expense ?? 0) },
      { type: 'line', label: 'ปริมาณ (ลิตร)', borderColor: '#b91c1c', borderWidth: 3, tension: 0.4, yAxisID: 'y1', data: fuelSortedKeys.map(key => monthlyFuelData[key]?.liters ?? 0) },
    ],
  }
  trendChartOptions.value = { maintainAspectRatio: false, aspectRatio: 0.6, scales: { x: { grid: { display: false } }, y: { type: 'linear', position: 'left', title: { display: true, text: 'บาท' } }, y1: { type: 'linear', position: 'right', title: { display: true, text: 'ลิตร' }, grid: { drawOnChartArea: false } } } }

  fuelTypeChartData.value = { labels: Object.keys(fuelTypeExpenses), datasets: [{ data: Object.values(fuelTypeExpenses), backgroundColor: ['#ef4444', '#f97316', '#eab308', '#22c55e', '#3b82f6'], borderWidth: 0 }] }
  fuelTypeChartOptions.value = { plugins: { legend: { position: 'bottom' } }, cutout: '50%' }

  const sortedVehicles = Object.entries(vehicleExpenses).sort((a, b) => b[1] - a[1]).slice(0, 10)
  vehicleChartData.value = { labels: sortedVehicles.map(item => item[0]), datasets: [{ label: 'ค่าน้ำมัน (บาท)', data: sortedVehicles.map(item => item[1]), backgroundColor: '#f87171', borderRadius: 4 }] }
  vehicleChartOptions.value = { indexAxis: 'y', maintainAspectRatio: false, aspectRatio: 0.8, plugins: { legend: { display: false } } }

  const receiptSortedKeys = Object.keys(monthlyReceiptData).sort()
  receiptTrendChartData.value = {
    labels: receiptSortedKeys.map(key => formatChartLabel(key)),
    datasets: [{ type: 'bar', label: 'ค่าผ่านทาง/อื่นๆ (บาท)', backgroundColor: '#6366f1', borderRadius: 4, data: receiptSortedKeys.map(key => monthlyReceiptData[key] || 0) }],
  }
  receiptTrendChartOptions.value = { maintainAspectRatio: false, aspectRatio: 0.4, scales: { x: { grid: { display: false } }, y: { type: 'linear', position: 'left', title: { display: true, text: 'บาท' } } } }

  const topDetails = Object.entries(receiptDetailExpenses).sort((a, b) => b[1] - a[1]).slice(0, 5)
  if (Object.keys(receiptDetailExpenses).length > 5) {
    const otherAmount = Object.entries(receiptDetailExpenses).slice(5).reduce((sum, item) => sum + item[1], 0)
    topDetails.push(['อื่นๆ', otherAmount])
  }
  receiptDetailChartData.value = { labels: topDetails.map(item => item[0]), datasets: [{ data: topDetails.map(item => item[1]), backgroundColor: ['#6366f1', '#a855f7', '#ec4899', '#f43f5e', '#f97316', '#94a3b8'], borderWidth: 0 }] }
  receiptDetailChartOptions.value = { plugins: { legend: { position: 'bottom' } }, cutout: '60%' }

  const topDrivers = Object.entries(receiptDriverExpenses).sort((a, b) => b[1] - a[1]).slice(0, 10)
  receiptDriverChartData.value = { labels: topDrivers.map(item => item[0]), datasets: [{ label: 'ยอดเบิก (บาท)', data: topDrivers.map(item => item[1]), backgroundColor: '#8b5cf6', borderRadius: 4 }] }
  receiptDriverChartOptions.value = { indexAxis: 'y', maintainAspectRatio: false, aspectRatio: 0.8, plugins: { legend: { display: false } } }
}

const formatCurrency = (val: number): string => new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val)
</script>


<template>
  <div class="max-w-7xl mx-auto pb-10">
    <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
      <div>
        <h2 class="text-3xl font-bold text-gray-800">
          <i class="pi pi-chart-bar text-red-500 mr-2"></i>ภาพรวมน้ำมันเชื้อเพลิง
        </h2>
        <p class="text-gray-500 mt-1">วิเคราะห์แนวโน้มค่าใช้จ่ายและประสิทธิภาพการใช้รถยนต์</p>
      </div>
      <div class="flex items-center gap-3">
        <div class="bg-white p-3 rounded-lg shadow-sm border border-gray-100 flex items-center gap-3">
          <div class="flex flex-col">
            <label class="text-xs font-semibold text-gray-500 mb-1">กรองตามช่วงเวลา</label>
            <DatePicker v-model="selectedDateRange" selectionMode="range" dateFormat="dd/mm/yy"
              placeholder="ด/ว/ป - ด/ว/ป" class="w-64" :manualInput="false" showIcon />
          </div>
          <Button v-if="selectedDateRange && selectedDateRange.length > 0" icon="pi pi-times" severity="secondary" text
            rounded @click="clearDateFilter" class="mt-4" />
        </div>
      </div>
    </div>
    <!-- ช่วงเวลาที่แสดงอยู่ในขณะนี้ -->
    <div class="flex items-center gap-2 mb-4">
      <i class="pi pi-calendar-clock text-red-400 text-sm"></i>
      <span class="text-sm text-gray-500">กำลังแสดงข้อมูล: </span>
      <span class="text-sm font-bold text-red-600 bg-red-50 px-3 py-0.5 rounded-full border border-red-200">{{
        dateRangeLabel }}</span>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
      <Card class="shadow-sm border-t-4 border-red-500">
        <template #content>
          <div class="flex justify-between items-start">
            <div>
              <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ค่าน้ำมันรวม (บาท)</p>
              <h3 class="text-2xl font-bold text-gray-800">{{ formatCurrency(totalExpense) }}</h3>
            </div>
            <div class="w-10 h-10 bg-red-50 rounded-full flex items-center justify-center text-red-500">
              <i class="pi pi-money-bill"></i>
            </div>
          </div>
        </template>
      </Card>

      <Card class="shadow-sm border-t-4 border-orange-500">
        <template #content>
          <div class="flex justify-between items-start">
            <div>
              <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ปริมาณรวม (ลิตร)</p>
              <h3 class="text-2xl font-bold text-gray-800">
                {{ totalLiters.toLocaleString(undefined, { maximumFractionDigits: 2 }) }}
                <span class="text-sm font-normal text-gray-500">ลิตร</span>
              </h3>
            </div>
            <div class="w-10 h-10 bg-orange-50 rounded-full flex items-center justify-center text-orange-500">
              <i class="pi pi-car"></i>
            </div>
          </div>
        </template>
      </Card>

      <Card class="shadow-sm border-t-4 border-yellow-500">
        <template #content>
          <div class="flex justify-between items-start">
            <div>
              <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ราคาเฉลี่ยต่อลิตร</p>
              <h3 class="text-2xl font-bold text-gray-800">
                {{ avgPricePerLiter.toFixed(2) }}
                <span class="text-sm font-normal text-gray-500">บาท/ลิตร</span>
              </h3>
            </div>
            <div class="w-10 h-10 bg-yellow-50 rounded-full flex items-center justify-center text-yellow-600">
              <i class="pi pi-calculator"></i>
            </div>
          </div>
        </template>
      </Card>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
      <Card class="shadow-sm border-none lg:col-span-2">
        <template #title>
          <div class="text-lg font-bold text-gray-700">แนวโน้มการเติมน้ำมันรายเดือน</div>
        </template>
        <template #content>
          <div v-if="isLoading" class="h-80 flex items-center justify-center">
            <i class="pi pi-spin pi-spinner text-4xl text-red-500"></i>
          </div>
          <div v-else-if="trendChartData?.labels?.length === 0"
            class="h-80 flex flex-col items-center justify-center text-gray-400">
            <i class="pi pi-box text-3xl mb-2"></i>
            <p>ไม่มีข้อมูล</p>
          </div>
          <div v-else class="h-80 relative">
            <Chart type="bar" :data="trendChartData" :options="trendChartOptions" class="h-full w-full" />
          </div>
        </template>
      </Card>

      <Card class="shadow-sm border-none lg:col-span-1">
        <template #title>
          <div class="text-lg font-bold text-gray-700">สัดส่วนประเภทน้ำมัน</div>
        </template>
        <template #content>
          <div v-if="isLoading" class="h-80 flex items-center justify-center">
            <i class="pi pi-spin pi-spinner text-4xl text-red-500"></i>
          </div>
          <div v-else-if="totalExpense === 0" class="h-80 flex flex-col items-center justify-center text-gray-400">
            <i class="pi pi-chart-pie text-3xl mb-2"></i>
            <p>ไม่มีข้อมูล</p>
          </div>
          <div v-else class="h-80 relative flex items-center justify-center">
            <Chart type="doughnut" :data="fuelTypeChartData" :options="fuelTypeChartOptions" class="w-full max-w-xs" />
          </div>
        </template>
      </Card>
    </div>

    <Card class="shadow-sm border-none">
      <template #title>
        <div class="text-lg font-bold text-gray-700">
          จัดอันดับรถยนต์ที่ใช้น้ำมันสูงสุด (บาท)
        </div>
      </template>
      <template #content>
        <div v-if="isLoading" class="h-64 flex items-center justify-center">
          <i class="pi pi-spin pi-spinner text-4xl text-red-500"></i>
        </div>
        <div v-else-if="vehicleChartData?.labels?.length === 0"
          class="h-64 flex flex-col items-center justify-center text-gray-400">
          <i class="pi pi-align-left text-3xl mb-2"></i>
          <p>ไม่มีข้อมูล</p>
        </div>
        <div v-else class="h-64 relative">
          <Chart type="bar" :data="vehicleChartData" :options="vehicleChartOptions" class="h-full w-full" />
        </div>
      </template>
    </Card>

    <!-- SECTION 2: RECEIPTS (แยกใบรับรองแทนใบเสร็จ) -->
    <div class="mt-14 mb-6 border-t pt-8">
      <h2 class="text-2xl font-bold text-gray-800 mb-6">
        <i class="pi pi-receipt text-indigo-500 mr-2"></i>สรุปใบรับรองแทนใบเสร็จ
      </h2>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        <Card class="shadow-sm border-t-4 border-indigo-500">
          <template #content>
            <div class="flex justify-between items-start">
              <div>
                <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ยอดรวมสุทธิ (บาท)</p>
                <h3 class="text-2xl font-bold text-gray-800">
                  {{ formatCurrency(totalReceiptExpense) }}
                </h3>
              </div>
              <div class="w-10 h-10 bg-indigo-50 rounded-full flex items-center justify-center text-indigo-500">
                <i class="pi pi-wallet"></i>
              </div>
            </div>
          </template>
        </Card>

        <Card class="shadow-sm border-t-4 border-emerald-500">
          <template #content>
            <div class="flex justify-between items-start">
              <div>
                <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">จำนวนรายการทั้งหมด</p>
                <h3 class="text-2xl font-bold text-gray-800">
                  {{ totalReceiptEntries }}
                  <span class="text-sm font-normal text-gray-500">บิล</span>
                </h3>
              </div>
              <div class="w-10 h-10 bg-emerald-50 rounded-full flex items-center justify-center text-emerald-500">
                <i class="pi pi-tags"></i>
              </div>
            </div>
          </template>
        </Card>

        <Card class="shadow-sm border-t-4 border-fuchsia-500">
          <template #content>
            <div class="flex justify-between items-start">
              <div>
                <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">เฉลี่ยต่อรายการ</p>
                <h3 class="text-2xl font-bold text-gray-800">
                  {{ formatCurrency(avgReceiptExpense) }}
                </h3>
              </div>
              <div class="w-10 h-10 bg-fuchsia-50 rounded-full flex items-center justify-center text-fuchsia-500">
                <i class="pi pi-chart-pie"></i>
              </div>
            </div>
          </template>
        </Card>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
        <Card class="shadow-sm border-none lg:col-span-2">
          <template #title>
            <div class="text-lg font-bold text-gray-700">
              แนวโน้มค่าใช้จ่ายใบรับรองรายเดือน
            </div>
          </template>
          <template #content>
            <div v-if="isLoading" class="h-80 flex items-center justify-center">
              <i class="pi pi-spin pi-spinner text-4xl text-indigo-500"></i>
            </div>
            <div v-else-if="receiptTrendChartData?.labels?.length === 0"
              class="h-80 flex flex-col items-center justify-center text-gray-400">
              <i class="pi pi-box text-3xl mb-2"></i>
              <p>ไม่มีข้อมูล</p>
            </div>
            <div v-else class="h-80 relative">
              <Chart type="bar" :data="receiptTrendChartData" :options="receiptTrendChartOptions"
                class="h-full w-full" />
            </div>
          </template>
        </Card>

        <Card class="shadow-sm border-none lg:col-span-1">
          <template #title>
            <div class="text-lg font-bold text-gray-700">โดนัท: สัดส่วนและหมวดหมู่</div>
          </template>
          <template #content>
            <div v-if="isLoading" class="h-80 flex items-center justify-center">
              <i class="pi pi-spin pi-spinner text-4xl text-fuchsia-500"></i>
            </div>
            <div v-else-if="totalReceiptExpense === 0"
              class="h-80 flex flex-col items-center justify-center text-gray-400">
              <i class="pi pi-chart-pie text-3xl mb-2"></i>
              <p>ไม่มีข้อมูล</p>
            </div>
            <div v-else class="h-80 relative flex items-center justify-center">
              <Chart type="doughnut" :data="receiptDetailChartData" :options="receiptDetailChartOptions"
                class="w-full max-w-xs" />
            </div>
          </template>
        </Card>
      </div>

      <Card class="shadow-sm border-none">
        <template #title>
          <div class="text-lg font-bold text-gray-700">
            จัดอันดับยอดเบิกพขร. สูงสุด (บาท)
          </div>
        </template>
        <template #content>
          <div v-if="isLoading" class="h-64 flex items-center justify-center">
            <i class="pi pi-spin pi-spinner text-4xl text-purple-500"></i>
          </div>
          <div v-else-if="receiptDriverChartData?.labels?.length === 0"
            class="h-64 flex flex-col items-center justify-center text-gray-400">
            <i class="pi pi-align-left text-3xl mb-2"></i>
            <p>ไม่มีข้อมูล</p>
          </div>
          <div v-else class="h-64 relative">
            <Chart type="bar" :data="receiptDriverChartData" :options="receiptDriverChartOptions"
              class="h-full w-full" />
          </div>
        </template>
      </Card>
    </div>
  </div>
</template>

<style scoped>
:deep(.p-datepicker) {
  border: none;
}
</style>
