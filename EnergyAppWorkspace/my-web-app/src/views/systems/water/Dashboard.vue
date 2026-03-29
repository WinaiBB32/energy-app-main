<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import api from '@/services/api'
import { useAppToast } from '@/composables/useAppToast'

import Card from 'primevue/card'
import Chart from 'primevue/chart'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'

defineOptions({ name: 'WaterDashboard' })

const toast = useAppToast()

interface FetchedWaterRecord {
  buildingId?: string
  waterAmount?: number
  waterUnitUsed?: number
  rawWaterCharge?: number
  monthlyServiceFee?: number
  vatAmount?: number
  totalAmount?: number
  billingCycle?: string // API returns string
}

interface MonthlyData {
  expense: number
  unit: number
}

const rawRecords = ref<FetchedWaterRecord[]>([])

const totalExpense = ref<number>(0)
const totalUnit = ref<number>(0)
const avgCostPerUnit = ref<number>(0)

const sumRawWater = ref<number>(0)
const sumWaterAmount = ref<number>(0)
const sumServiceFee = ref<number>(0)
const sumVat = ref<number>(0)

const getLastMonthRange = (): Date[] => {
  const now = new Date()
  const first = new Date(now.getFullYear(), now.getMonth() - 1, 1)
  const last = new Date(now.getFullYear(), now.getMonth(), 0)
  return [first, last]
}
const selectedDateRange = ref<Date[] | null>(getLastMonthRange())

const thaiMonthShort = ['ม.ค.','ก.พ.','มี.ค.','เม.ย.','พ.ค.','มิ.ย.','ก.ค.','ส.ค.','ก.ย.','ต.ค.','พ.ย.','ธ.ค.']
const dateRangeLabel = computed(() => {
  const r = selectedDateRange.value
  if (!r || r.length < 2 || !r[0] || !r[1]) return 'ทุกช่วงเวลา'
  const fmt = (d: Date) => `${thaiMonthShort[d.getMonth()]} ${d.getFullYear() + 543}`
  const s = fmt(r[0]), e = fmt(r[1])
  return s === e ? s : `${s} – ${e}`
})

const trendChartData = ref()
const trendChartOptions = ref()
const breakdownChartData = ref()
const breakdownChartOptions = ref()
const buildingChartData = ref()
const buildingChartOptions = ref()

const isLoading = ref<boolean>(true)

const buildings = ref<{id: string, name: string}[]>([])

const getBuildingName = (id: string): string => buildings.value.find((x) => x.id === id)?.name || id

const fetchBuildings = async () => {
    try {
        const { data } = await api.get('/Building');
        buildings.value = data.items || [];
    } catch (error) {
        toast.fromError(error, 'ไม่สามารถโหลดข้อมูลอาคารได้')
    }
}

const fetchData = async (): Promise<void> => {
  isLoading.value = true
  try {
    const fromDate = selectedDateRange.value?.[0]?.toISOString()
    const toDate = selectedDateRange.value?.[1]?.toISOString()

    const params = new URLSearchParams()
    if (fromDate) params.append('fromDate', fromDate)
    if (toDate) params.append('toDate', toDate)
    params.append('take', '10000') // Fetch all for dashboard

    const { data } = await api.get('/WaterRecord', { params })
    
    rawRecords.value = data.items || []
    processData()
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูล Dashboard น้ำประปาได้')
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
    fetchBuildings();
    fetchData();
})
watch(selectedDateRange, () => fetchData())
const clearDateFilter = (): void => {
  selectedDateRange.value = getLastMonthRange()
}

const processData = (): void => {
  let tempExpense = 0
  let tempUnit = 0
  let tempRaw = 0
  let tempWater = 0
  let tempService = 0
  let tempVat = 0

  const monthlyData: Record<string, MonthlyData> = {}
  const buildingExpenses: Record<string, number> = {}

  rawRecords.value.forEach((data) => {
    if (!data.billingCycle) return
    const recordDateObj = new Date(data.billingCycle)

    const sortKey = `${recordDateObj.getFullYear()}-${String(recordDateObj.getMonth() + 1).padStart(2, '0')}`
    if (!monthlyData[sortKey]) monthlyData[sortKey] = { expense: 0, unit: 0 }

    const totalAmount = data.totalAmount || 0
    const unit = data.waterUnitUsed || 0

    tempExpense += totalAmount
    tempUnit += unit
    tempRaw += data.rawWaterCharge || 0
    tempWater += data.waterAmount || 0
    tempService += data.monthlyServiceFee || 0
    tempVat += data.vatAmount || 0

    monthlyData[sortKey].expense += totalAmount
    monthlyData[sortKey].unit += unit

    const bId = data.buildingId || 'Unknown'
    if (!buildingExpenses[bId]) buildingExpenses[bId] = 0
    buildingExpenses[bId] += totalAmount
  })

  totalExpense.value = tempExpense
  totalUnit.value = tempUnit
  avgCostPerUnit.value = tempUnit > 0 ? tempExpense / tempUnit : 0

  sumRawWater.value = tempRaw
  sumWaterAmount.value = tempWater
  sumServiceFee.value = tempService
  sumVat.value = tempVat

  setupCharts(monthlyData, buildingExpenses)
}

const formatChartLabel = (sortKey: string): string => {
  const [yearStr = '', monthStr = '1'] = sortKey.split('-')
  const monthNames = [
    'ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.',
    'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.',
  ]
  return `${monthNames[parseInt(monthStr, 10) - 1]} ${yearStr}`
}

const setupCharts = (
  monthlyData: Record<string, MonthlyData>,
  buildingExpenses: Record<string, number>,
): void => {
  const sortedKeys = Object.keys(monthlyData).sort()
  const labels = sortedKeys.map((key) => formatChartLabel(key))

  trendChartData.value = {
    labels,
    datasets: [
      {
        type: 'bar',
        label: 'ยอดรวมสุทธิ (บาท)',
        backgroundColor: '#0ea5e9',
        borderRadius: 4,
        yAxisID: 'y',
        data: sortedKeys.map((key) => monthlyData[key]?.expense ?? 0),
      },
      {
        type: 'line',
        label: 'ปริมาณน้ำที่ใช้ (ลบ.ม.)',
        borderColor: '#0284c7',
        backgroundColor: '#0284c7',
        borderWidth: 3,
        tension: 0.4,
        yAxisID: 'y1',
        data: sortedKeys.map((key) => monthlyData[key]?.unit ?? 0),
      },
    ],
  }
  trendChartOptions.value = {
    maintainAspectRatio: false,
    aspectRatio: 0.6,
    plugins: { legend: { labels: { color: '#495057' } } },
    scales: {
      x: { grid: { display: false } },
      y: {
        type: 'linear',
        display: true,
        position: 'left',
        title: { display: true, text: 'จำนวนเงิน (บาท)' },
      },
      y1: {
        type: 'linear',
        display: true,
        position: 'right',
        title: { display: true, text: 'ปริมาณ (ลบ.ม.)' },
        grid: { drawOnChartArea: false },
      },
    },
  }

  breakdownChartData.value = {
    labels: ['ค่าน้ำดิบ', 'ค่าน้ำประปา', 'ค่าบริการรายเดือน', 'ภาษีมูลค่าเพิ่ม (VAT)'],
    datasets: [
      {
        data: [sumRawWater.value, sumWaterAmount.value, sumServiceFee.value, sumVat.value],
        backgroundColor: ['#60a5fa', '#3b82f6', '#1d4ed8', '#93c5fd'],
        borderWidth: 0,
      },
    ],
  }
  breakdownChartOptions.value = {
    plugins: { legend: { position: 'bottom' } },
    cutout: '50%',
  }

  const sortedBuildings = Object.entries(buildingExpenses).sort((a, b) => b[1] - a[1])
  buildingChartData.value = {
    labels: sortedBuildings.map((item) => getBuildingName(item[0])),
    datasets: [
      {
        label: 'ค่าน้ำสะสมสุทธิ (บาท)',
        data: sortedBuildings.map((item) => item[1]),
        backgroundColor: '#38bdf8',
        borderRadius: 4,
      },
    ],
  }
  buildingChartOptions.value = {
    indexAxis: 'y',
    maintainAspectRatio: false,
    aspectRatio: 0.8,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { color: '#f3f4f6' } },
      y: { grid: { display: false }, ticks: { font: { weight: 'bold' } } },
    },
  }
}

const formatCurrency = (val: number): string =>
  new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val)
</script>
<template>
  <div class="max-w-7xl mx-auto pb-10">
    <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
      <div>
        <h2 class="text-3xl font-bold text-gray-800">
          <i class="pi pi-chart-bar text-sky-500 mr-2"></i>ภาพรวมค่าน้ำประปา
        </h2>
        <p class="text-gray-500 mt-1">วิเคราะห์แนวโน้มการใช้น้ำและโครงสร้างค่าใช้จ่ายสุทธิ</p>
      </div>
      <div class="bg-white p-3 rounded-lg shadow-sm border border-gray-100 flex items-center gap-3">
        <div class="flex flex-col">
          <label class="text-xs font-semibold text-gray-500 mb-1">กรองตามช่วงเวลา</label>
          <DatePicker
            v-model="selectedDateRange"
            selectionMode="range"
            dateFormat="dd/mm/yy"
            placeholder="ด/ว/ป - ด/ว/ป"
            class="w-64"
            :manualInput="false"
            showIcon
          />
        </div>
        <Button
          v-if="selectedDateRange && selectedDateRange.length > 0"
          icon="pi pi-times"
          severity="secondary"
          text
          rounded
          @click="clearDateFilter"
          class="mt-4"
        />
      </div>
    </div>
    <!-- ช่วงเวลาที่แสดง -->
    <div class="flex items-center gap-2 mb-4">
      <i class="pi pi-calendar-clock text-sky-400 text-sm"></i>
      <span class="text-sm text-gray-500">กำลังแสดงข้อมูล: </span>
      <span class="text-sm font-bold text-sky-600 bg-sky-50 px-3 py-0.5 rounded-full border border-sky-200">{{ dateRangeLabel }}</span>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
      <Card class="shadow-sm border-t-4 border-sky-500">
        <template #content>
          <div class="flex justify-between items-start">
            <div>
              <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                ยอดรายจ่ายสุทธิรวม (รวม VAT)
              </p>
              <h3 class="text-2xl font-bold text-gray-800">{{ formatCurrency(totalExpense) }}</h3>
            </div>
            <div
              class="w-10 h-10 bg-sky-50 rounded-full flex items-center justify-center text-sky-500"
            >
              <i class="pi pi-money-bill"></i>
            </div>
          </div>
        </template>
      </Card>

      <Card class="shadow-sm border-t-4 border-blue-500">
        <template #content>
          <div class="flex justify-between items-start">
            <div>
              <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                ปริมาณน้ำที่ใช้ทั้งหมด
              </p>
              <h3 class="text-2xl font-bold text-gray-800">
                {{ totalUnit.toLocaleString(undefined, { maximumFractionDigits: 2 }) }}
                <span class="text-sm font-normal text-gray-500">ลบ.ม.</span>
              </h3>
            </div>
            <div
              class="w-10 h-10 bg-blue-50 rounded-full flex items-center justify-center text-blue-500"
            >
              <i class="pi pi-tint"></i>
            </div>
          </div>
        </template>
      </Card>

      <Card class="shadow-sm border-t-4 border-cyan-500">
        <template #content>
          <div class="flex justify-between items-start">
            <div>
              <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                ค่าน้ำสุทธิเฉลี่ยต่อหน่วย
              </p>
              <h3 class="text-2xl font-bold text-gray-800">
                {{ avgCostPerUnit.toFixed(2) }}
                <span class="text-sm font-normal text-gray-500">บาท/ลบ.ม.</span>
              </h3>
            </div>
            <div
              class="w-10 h-10 bg-cyan-50 rounded-full flex items-center justify-center text-cyan-500"
            >
              <i class="pi pi-calculator"></i>
            </div>
          </div>
        </template>
      </Card>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
      <Card class="shadow-sm border-none lg:col-span-2">
        <template #title
          ><div class="text-lg font-bold text-gray-700">
            แนวโน้มการใช้น้ำประปารายเดือน
          </div></template
        >
        <template #content>
          <div v-if="isLoading" class="h-80 flex items-center justify-center">
            <i class="pi pi-spin pi-spinner text-4xl text-sky-500"></i>
          </div>
          <div
            v-else-if="trendChartData?.labels?.length === 0"
            class="h-80 flex flex-col items-center justify-center text-gray-400"
          >
            <i class="pi pi-box text-3xl mb-2"></i>
            <p>ไม่มีข้อมูล</p>
          </div>
          <div v-else class="h-80 relative">
            <Chart
              type="bar"
              :data="trendChartData"
              :options="trendChartOptions"
              class="h-full w-full"
            />
          </div>
        </template>
      </Card>

      <Card class="shadow-sm border-none lg:col-span-1">
        <template #title
          ><div class="text-lg font-bold text-gray-700">โครงสร้างค่าใช้จ่ายบิลประปา</div></template
        >
        <template #content>
          <div v-if="isLoading" class="h-80 flex items-center justify-center">
            <i class="pi pi-spin pi-spinner text-4xl text-sky-500"></i>
          </div>
          <div
            v-else-if="totalExpense === 0"
            class="h-80 flex flex-col items-center justify-center text-gray-400"
          >
            <i class="pi pi-chart-pie text-3xl mb-2"></i>
            <p>ไม่มีข้อมูล</p>
          </div>
          <div v-else class="h-80 relative flex items-center justify-center">
            <Chart
              type="doughnut"
              :data="breakdownChartData"
              :options="breakdownChartOptions"
              class="w-full max-w-xs"
            />
          </div>
        </template>
      </Card>
    </div>

    <Card class="shadow-sm border-none">
      <template #title
        ><div class="text-lg font-bold text-gray-700">
          จัดอันดับอาคารที่ใช้น้ำสูงสุด (ยอดรวมสุทธิ)
        </div></template
      >
      <template #content>
        <div v-if="isLoading" class="h-64 flex items-center justify-center">
          <i class="pi pi-spin pi-spinner text-4xl text-sky-500"></i>
        </div>
        <div
          v-else-if="buildingChartData?.labels?.length === 0"
          class="h-64 flex flex-col items-center justify-center text-gray-400"
        >
          <i class="pi pi-align-left text-3xl mb-2"></i>
          <p>ไม่มีข้อมูล</p>
        </div>
        <div v-else class="h-64 relative">
          <Chart
            type="bar"
            :data="buildingChartData"
            :options="buildingChartOptions"
            class="h-full w-full"
          />
        </div>
      </template>
    </Card>
  </div>
</template>

<style scoped>
:deep(.p-datepicker) {
  border: none;
}
</style>
