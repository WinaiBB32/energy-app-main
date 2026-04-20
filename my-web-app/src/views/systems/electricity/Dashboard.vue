<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import api from '@/services/api'

defineOptions({ name: 'ElectricityDashboard' })
import { useAppToast } from '@/composables/useAppToast'

import Card from 'primevue/card'
import Chart from 'primevue/chart'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import Select from 'primevue/select'

function notifySignage() {
  if (window.parent && window.parent !== window) {
    window.parent.postMessage('user_is_touching', '*')
  }
}

document.addEventListener('touchstart', notifySignage)
document.addEventListener('touchmove', notifySignage)
document.addEventListener('click', notifySignage)
document.addEventListener('wheel', notifySignage)

interface FetchedRecord {
  id?: string
  type: 'PEA_BILL' | 'SOLAR_PRODUCTION'
  buildingId?: string
  peaAmount?: number
  peaUnitUsed?: number
  onPeakUnits?: number
  offPeakUnits?: number
  ftAmount?: number
  monthlyServiceFee?: number
  solarUnitProduced?: number

  productionWh?: number
  toBatteryWh?: number
  toGridWh?: number
  toHomeWh?: number
  consumptionWh?: number
  fromBatteryWh?: number
  fromGridWh?: number
  fromSolarWh?: number

  billingCycle?: string
  recordDate?: string
}

interface MonthlyAggregatedData {
  expense: number
  peaUnit: number
  onPeak: number
  offPeak: number
  solar: number
}

interface Building {
  id: string
  name: string
}

const buildings = ref<Building[]>([])
const buildingOptions = computed(() => [{ id: null, name: 'ทุกอาคาร' }, ...buildings.value])
const getBuildingName = (id: string): string => buildings.value.find((x) => x.id === id)?.name || id
const rawRecords = ref<FetchedRecord[]>([])

const totalExpense = ref<number>(0)
const totalPeaUnit = ref<number>(0)
const totalSolarUnit = ref<number>(0)
const avgCostPerUnit = ref<number>(0)
const solarSavings = ref<number>(0)
const carbonSaved = ref<number>(0)

const sumConsumptionKwh = ref<number>(0)
const sumFromGridKwh = ref<number>(0)
const sumToGridKwh = ref<number>(0)
const sumToHomeKwh = ref<number>(0)
const sumFromBatteryKwh = ref<number>(0)
const sumToBatteryKwh = ref<number>(0)
const sumFromSolarKwh = ref<number>(0)
const sumOnPeakUnits = ref<number>(0)
const sumOffPeakUnits = ref<number>(0)
const sumFtAmountTotal = ref<number>(0)
const sumMonthlyFeeTotal = ref<number>(0)

const getYearToDateRange = (): Date[] => {
  const now = new Date()
  const first = new Date(now.getFullYear(), 0, 1) // 1 January of current year
  const last = new Date(now.getFullYear(), now.getMonth(), now.getDate()) // today
  return [first, last]
}
const selectedDateRange = ref<Date[] | null>(getYearToDateRange())
const selectedBuildingFilter = ref<string | null>(null)

const thaiMonthShort = [
  'ม.ค.',
  'ก.พ.',
  'มี.ค.',
  'เม.ย.',
  'พ.ค.',
  'มิ.ย.',
  'ก.ค.',
  'ส.ค.',
  'ก.ย.',
  'ต.ค.',
  'พ.ย.',
  'ธ.ค.',
]
const dateRangeLabel = computed(() => {
  const r = selectedDateRange.value
  if (!r || r.length < 2 || !r[0] || !r[1]) return 'ทุกช่วงเวลา'
  const fmt = (d: Date) => `${thaiMonthShort[d.getMonth()]} ${d.getFullYear() + 543}`
  const s = fmt(r[0]),
    e = fmt(r[1])
  return s === e ? s : `${s} – ${e}`
})

const expenseChartData = ref()
const expenseChartOptions = ref()
const solarChartData = ref()
const solarChartOptions = ref()
const mixChartData = ref()
const mixChartOptions = ref()
const buildingChartData = ref()
const buildingChartOptions = ref()
const solarUsageChartData = ref()
const solarBreakdownChartData = ref()
const overviewChartData = ref()
const overviewChartOptions = ref()
const unitTrendChartData = ref()
const unitTrendChartOptions = ref()
const percentDoughnutOptions = ref()

const onOffPeakChartData = ref()
const onOffPeakChartOptions = ref()
const onOffPeakDoughnutData = ref()

// --- เพิ่มสำหรับ PEA Unit Trend ---
const peaUnitTrendData = ref<any[]>([])
const peaUnitTrendChartData = ref()
const peaUnitTrendChartOptions = ref()
// --- เพิ่มสำหรับ PEA Amount Trend ---
const peaAmountTrendChartData = ref()
const peaAmountTrendChartOptions = ref()

const toast = useAppToast()
const isLoading = ref<boolean>(true)

const fetchData = async (): Promise<void> => {
  isLoading.value = true
  try {
    const startDate = selectedDateRange.value?.[0] || null
    const endDate = selectedDateRange.value?.[1] || null

    const toLocalDateStr = (d: Date, eod = false): string => {
      const y = d.getFullYear()
      const m = String(d.getMonth() + 1).padStart(2, '0')
      const day = String(d.getDate()).padStart(2, '0')
      return `${y}-${m}-${day}T${eod ? '23:59:59' : '00:00:00'}`
    }
    // สำหรับ trend API: yyyy-MM-ddTHH:mm:ss (ปลอดภัยสุดสำหรับ .NET)
    const toDateTimeStr = (d: Date, eod = false): string => {
      const y = d.getFullYear()
      const m = String(d.getMonth() + 1).padStart(2, '0')
      const day = String(d.getDate()).padStart(2, '0')
      return `${y}-${m}-${day}T${eod ? '23:59:59' : '00:00:00'}`
    }

    const params: Record<string, unknown> = { take: 10000 }
    if (startDate) params.fromDate = toLocalDateStr(startDate)
    if (endDate) params.toDate = toLocalDateStr(endDate, true)

    // สร้าง params สำหรับ trend (ไม่ใส่ take/skip)
    const trendParams: Record<string, unknown> = {}
    if (startDate) trendParams.fromDate = toDateTimeStr(startDate)
    if (endDate) trendParams.toDate = toDateTimeStr(endDate, true)
    // ถ้ามี building filter ให้ส่งไปด้วย
    if (selectedBuildingFilter.value) trendParams.buildingId = selectedBuildingFilter.value

    const [buildingsRes, peaRes, solarRes, peaUnitTrendRes] = await Promise.all([
      api.get('/Building'),
      api.get('/ElectricityBill', { params }),
      api.get('/SolarProduction', { params }),
      api.get('/ElectricityBill/monthly-unit-trend', { params: trendParams }),
    ])

    const buildingData = buildingsRes.data as Building[] | { items: Building[] }
    buildings.value = Array.isArray(buildingData)
      ? buildingData
      : (buildingData as { items: Building[] }).items || []
    const peaData = peaRes.data as { items: FetchedRecord[] }
    const solarData = solarRes.data as { items: FetchedRecord[] }
    const peaRecords = (peaData.items || []).map((r) => ({
      ...r,
      type: 'PEA_BILL' as const,
    }))
    const solarRecords = (solarData.items || []).map((r) => ({
      ...r,
      type: 'SOLAR_PRODUCTION' as const,
    }))

    rawRecords.value = [...peaRecords, ...solarRecords]

    // --- จัดการข้อมูล trend ---
    peaUnitTrendData.value = Array.isArray(peaUnitTrendRes.data) ? peaUnitTrendRes.data : []
    setupPeaUnitTrendChart()

    processDashboardData()
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูล Dashboard ไฟฟ้าได้')
  } finally {
    isLoading.value = false
  }
}

function setupPeaUnitTrendChart() {
  // peaUnitTrendData.value: [{ year, month, label, totalUnit, totalAmount }]
  console.log('peaUnitTrendData', JSON.stringify(peaUnitTrendData.value, null, 2))
  const labels = peaUnitTrendData.value.map((x: any) => x.label)
  const dataUnit = peaUnitTrendData.value.map((x: any) => Number(x.totalUnit) || 0)
  const dataAmount = peaUnitTrendData.value.map((x: any) => parseFloat(x.totalAmount) || 0)
  console.log('dataUnit', dataUnit)
  console.log('dataAmount', dataAmount)
  peaUnitTrendChartData.value = {
    labels,
    datasets: [
      {
        type: 'bar',
        label: 'หน่วยที่ซื้อ กฟภ. (Unit)',
        backgroundColor: '#3b82f6',
        borderRadius: 4,
        data: dataUnit,
      },
    ],
  }
  peaUnitTrendChartOptions.value = getChartOptions('หน่วย (Unit)')

  peaAmountTrendChartData.value = {
    labels,
    datasets: [
      {
        type: 'bar',
        label: 'แนวโน้มหน่วยไฟฟ้าที่ซื้อ (บาท)',
        backgroundColor: '#f59e42',
        borderRadius: 4,
        data: dataAmount,
      },
    ],
  }
  peaAmountTrendChartOptions.value = getChartOptions('บาท')
}

onMounted(() => fetchData())

watch([selectedDateRange], () => fetchData())
watch([selectedBuildingFilter], () => processDashboardData())

const clearDateFilter = (): void => {
  selectedDateRange.value = getYearToDateRange()
  selectedBuildingFilter.value = null
}

const processDashboardData = (): void => {
  let sumExpense = 0,
    sumPeaUnit = 0,
    sumSolar = 0
  let tOnPeak = 0,
    tOffPeak = 0,
    tFtAmount = 0,
    tMonthlyFee = 0
  let tConsumption = 0,
    tFromGrid = 0,
    tToGrid = 0,
    tToHome = 0,
    tFromBat = 0,
    tToBat = 0,
    tFromSolar = 0

  const monthlyData: Record<string, MonthlyAggregatedData> = {}
  const buildingExpenses: Record<string, number> = {}

  rawRecords.value.forEach((data) => {
    let recordDateObj: Date | null = null
    if (data.type === 'PEA_BILL' && data.billingCycle) recordDateObj = new Date(data.billingCycle)
    else if (data.type === 'SOLAR_PRODUCTION' && data.recordDate)
      recordDateObj = new Date(data.recordDate)

    if (!recordDateObj || isNaN(recordDateObj.getTime())) return
    if (selectedBuildingFilter.value && data.buildingId !== selectedBuildingFilter.value) return

    const year = recordDateObj.getFullYear()
    const month = String(recordDateObj.getMonth() + 1).padStart(2, '0')
    const sortKey = `${year}-${month}`

    if (!monthlyData[sortKey])
      monthlyData[sortKey] = { expense: 0, peaUnit: 0, onPeak: 0, offPeak: 0, solar: 0 }
    const bucket = monthlyData[sortKey]!

    if (data.type === 'PEA_BILL') {
      const amount = data.peaAmount || 0
      sumExpense += amount
      bucket.expense += amount
      sumPeaUnit += data.peaUnitUsed || 0
      bucket.peaUnit += data.peaUnitUsed || 0
      bucket.onPeak += data.onPeakUnits || 0
      bucket.offPeak += data.offPeakUnits || 0
      tOnPeak += data.onPeakUnits || 0
      tOffPeak += data.offPeakUnits || 0
      tFtAmount += data.ftAmount || 0
      tMonthlyFee += data.monthlyServiceFee || 0

      const bId = data.buildingId || 'Unknown'
      if (!buildingExpenses[bId]) buildingExpenses[bId] = 0
      buildingExpenses[bId] += amount
    } else if (data.type === 'SOLAR_PRODUCTION') {
      const solarUnit = data.solarUnitProduced || 0
      sumSolar += solarUnit
      bucket.solar += solarUnit

      tConsumption += (data.consumptionWh || 0) / 1000
      tFromGrid += (data.fromGridWh || 0) / 1000
      tToGrid += (data.toGridWh || 0) / 1000
      tToHome += (data.toHomeWh || 0) / 1000
      tFromBat += (data.fromBatteryWh || 0) / 1000
      tToBat += (data.toBatteryWh || 0) / 1000
      tFromSolar += (data.fromSolarWh || 0) / 1000
    }
  })

  totalExpense.value = sumExpense
  totalPeaUnit.value = sumPeaUnit
  totalSolarUnit.value = sumSolar

  sumOnPeakUnits.value = tOnPeak
  sumOffPeakUnits.value = tOffPeak
  sumFtAmountTotal.value = tFtAmount
  sumMonthlyFeeTotal.value = tMonthlyFee

  sumConsumptionKwh.value = tConsumption
  sumFromGridKwh.value = tFromGrid
  sumToGridKwh.value = tToGrid
  sumToHomeKwh.value = tToHome
  sumFromBatteryKwh.value = tFromBat
  sumToBatteryKwh.value = tToBat
  sumFromSolarKwh.value = tFromSolar

  avgCostPerUnit.value = sumPeaUnit > 0 ? sumExpense / sumPeaUnit : 0
  solarSavings.value = sumSolar * avgCostPerUnit.value
  carbonSaved.value = sumSolar * 0.5

  setupCharts(monthlyData, sumPeaUnit, sumSolar, buildingExpenses)
}

const formatChartLabel = (sortKey: string): string => {
  const [yearStr = '', monthStr = '1'] = sortKey.split('-')
  const monthNames = [
    'ม.ค.',
    'ก.พ.',
    'มี.ค.',
    'เม.ย.',
    'พ.ค.',
    'มิ.ย.',
    'ก.ค.',
    'ส.ค.',
    'ก.ย.',
    'ต.ค.',
    'พ.ย.',
    'ธ.ค.',
  ]
  return `${monthNames[parseInt(monthStr, 10) - 1]} ${yearStr}`
}

const setupCharts = (
  monthlyData: Record<string, MonthlyAggregatedData>,
  totalPea: number,
  totalSolar: number,
  buildingExpenses: Record<string, number>,
): void => {
  const sortedKeys = Object.keys(monthlyData).sort()
  const labels = sortedKeys.map((key) => formatChartLabel(key))

  expenseChartData.value = {
    labels,
    datasets: [
      {
        type: 'bar',
        label: 'ค่าไฟฟ้า (บาท)',
        backgroundColor: '#3b82f6',
        borderRadius: 4,
        data: sortedKeys.map((key) => monthlyData[key]?.expense ?? 0),
      },
    ],
  }
  expenseChartOptions.value = getChartOptions('ค่าไฟฟ้า (บาท)')

  solarChartData.value = {
    labels,
    datasets: [
      {
        type: 'line',
        label: 'Solar ที่ผลิตได้ (kWh)',
        borderColor: '#22c55e',
        backgroundColor: '#22c55e',
        borderWidth: 3,
        tension: 0.4,
        data: sortedKeys.map((key) => monthlyData[key]?.solar ?? 0),
      },
    ],
  }
  solarChartOptions.value = getChartOptions('พลังงาน Solar (kWh)')

  mixChartData.value = {
    labels: ['ซื้อไฟฟ้า (กฟภ.)', 'ผลิตเอง (Solar)'],
    datasets: [
      {
        data: [totalPea, totalSolar],
        backgroundColor: ['#3b82f6', '#22c55e'],
        hoverBackgroundColor: ['#2563eb', '#16a34a'],
        borderWidth: 0,
      },
    ],
  }
  mixChartOptions.value = {
    plugins: { legend: { position: 'bottom' } },
    cutout: '60%',
    maintainAspectRatio: false,
  }

  solarUsageChartData.value = {
    labels: ['ดึงจากสายส่ง (From Grid)', 'พลังงานแสงอาทิตย์ (Solar)', 'พลังงานในแบต (Battery)'],
    datasets: [
      {
        data: [sumFromGridKwh.value, sumFromSolarKwh.value, sumFromBatteryKwh.value],
        backgroundColor: ['#f43f5e', '#10b981', '#f59e0b'],
        borderWidth: 0,
      },
    ],
  }

  // Row 3 Left — หน่วยที่ซื้อ กฟน. รายเดือน (Unit)
  overviewChartData.value = {
    labels,
    datasets: [
      {
        type: 'bar',
        label: 'หน่วยที่ซื้อ กฟน. (Unit)',
        backgroundColor: '#3b82f6',
        borderRadius: 4,
        data: sortedKeys.map((key) => monthlyData[key]?.peaUnit ?? 0),
      },
    ],
  }
  overviewChartOptions.value = getChartOptions('หน่วย (Unit)')

  solarBreakdownChartData.value = {
    labels: ['ใช้ในอาคาร (To Home)', 'ส่งขายคืนสายส่ง (To Grid)', 'ชาร์จแบตฯ (To Battery)'],
    datasets: [
      {
        data: [sumToHomeKwh.value, sumToGridKwh.value, sumToBatteryKwh.value],
        backgroundColor: ['#3b82f6', '#8b5cf6', '#14b8a6'],
        borderWidth: 0,
      },
    ],
  }

  // Row 3 Right — Solar ที่ผลิตรายเดือน (kWh)
  unitTrendChartData.value = {
    labels,
    datasets: [
      {
        type: 'bar',
        label: 'Solar ที่ผลิต (kWh)',
        backgroundColor: '#22c55e',
        borderRadius: 4,
        data: sortedKeys.map((key) => monthlyData[key]?.solar ?? 0),
      },
    ],
  }
  unitTrendChartOptions.value = getChartOptions('หน่วย (kWh)')

  // On/Off Peak monthly stacked bar
  onOffPeakChartData.value = {
    labels,
    datasets: [
      {
        type: 'bar',
        label: 'On Peak (หน่วย)',
        backgroundColor: '#f97316',
        borderRadius: 4,
        data: sortedKeys.map((key) => monthlyData[key]?.onPeak ?? 0),
      },
      {
        type: 'bar',
        label: 'Off Peak (หน่วย)',
        backgroundColor: '#3b82f6',
        borderRadius: 4,
        data: sortedKeys.map((key) => monthlyData[key]?.offPeak ?? 0),
      },
    ],
  }
  onOffPeakChartOptions.value = {
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' as const } },
    scales: {
      x: { ticks: { color: '#6b7280' }, grid: { display: false } },
      y: {
        title: { display: true, text: 'หน่วย (Unit)' },
        ticks: { color: '#6b7280' },
        grid: { color: '#f3f4f6' },
      },
    },
  }

  // Donut สัดส่วน On Peak / Off Peak รวม
  const totalOnPeak = sortedKeys.reduce((s, k) => s + (monthlyData[k]?.onPeak ?? 0), 0)
  const totalOffPeak = sortedKeys.reduce((s, k) => s + (monthlyData[k]?.offPeak ?? 0), 0)
  onOffPeakDoughnutData.value = {
    labels: ['On Peak', 'Off Peak'],
    datasets: [
      {
        data: [totalOnPeak, totalOffPeak],
        backgroundColor: ['#f97316', '#3b82f6'],
        hoverBackgroundColor: ['#ea580c', '#2563eb'],
        borderWidth: 2,
        borderColor: '#ffffff',
      },
    ],
  }

  // Row 4 — % doughnut options
  percentDoughnutOptions.value = {
    plugins: {
      legend: { position: 'bottom' },
      tooltip: {
        callbacks: {
          label: (ctx: { label: string; parsed: number; dataset: { data: number[] } }) => {
            const total = ctx.dataset.data.reduce((a, b) => a + b, 0)
            const pct = total > 0 ? ((ctx.parsed / total) * 100).toFixed(1) : '0.0'
            return ` ${ctx.label}: ${pct}%`
          },
        },
      },
    },
    cutout: '60%',
    maintainAspectRatio: false,
  }

  const sortedBuildings = Object.entries(buildingExpenses).sort((a, b) => b[1] - a[1])
  buildingChartData.value = {
    labels: sortedBuildings.map((item) => getBuildingName(item[0])),
    datasets: [
      {
        label: 'ค่าไฟฟ้าสะสม (บาท)',
        data: sortedBuildings.map((item) => item[1]),
        backgroundColor: '#f97316',
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
      x: { grid: { color: '#f3f4f6' }, ticks: { color: '#6b7280' } },
      y: { grid: { display: false }, ticks: { color: '#4b5563', font: { weight: 'bold' } } },
    },
  }
}

const getChartOptions = (yAxisTitle: string) => ({
  maintainAspectRatio: false,
  aspectRatio: 0.8,
  plugins: { legend: { display: false } },
  scales: {
    x: { ticks: { color: '#6b7280' }, grid: { display: false } },
    y: {
      title: { display: true, text: yAxisTitle },
      ticks: { color: '#6b7280' },
      grid: { color: '#f3f4f6' },
    },
  },
})

const formatCurrency = (val: number): string =>
  new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val)
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10">
    <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
      <div>
        <h2 class="text-3xl font-bold text-gray-800">ภาพรวมระบบไฟฟ้า & Solar</h2>
        <p class="text-gray-500 mt-1">
          วิเคราะห์แนวโน้มการใช้พลังงาน และความคุ้มค่าของการผลิต Solar
        </p>
      </div>
      <div class="bg-white p-3 rounded-lg shadow-sm border border-gray-100 flex items-center gap-3">
        <div class="flex flex-col">
          <label class="text-xs font-semibold text-gray-500 mb-1">เลือกอาคาร/จุดติดตั้ง</label>
          <Select v-model="selectedBuildingFilter" :options="buildingOptions" optionLabel="name" optionValue="id"
            placeholder="ทุกอาคาร" class="w-48" />
        </div>
        <div class="flex flex-col">
          <label class="text-xs font-semibold text-gray-500 mb-1">กรองตามช่วงเวลา</label>
          <DatePicker v-model="selectedDateRange" selectionMode="range" dateFormat="dd/mm/yy"
            placeholder="ด/ว/ป - ด/ว/ป" class="w-64" :manualInput="false" showIcon />
        </div>
        <Button v-if="(selectedDateRange && selectedDateRange.length > 0) || selectedBuildingFilter" icon="pi pi-times"
          severity="secondary" text rounded @click="clearDateFilter" class="mt-4" />
      </div>
    </div>
    <!-- ช่วงเวลาที่แสดงอยู่ -->
    <div class="flex items-center gap-2 mb-4">
      <i class="pi pi-filter text-blue-400 text-sm"></i>
      <span class="text-sm text-gray-500">กำลังแสดงข้อมูล: </span>
      <span class="text-sm font-bold text-blue-600 bg-blue-50 px-3 py-0.5 rounded-full border border-blue-200">{{
        selectedBuildingFilter ? getBuildingName(selectedBuildingFilter) : 'ทุกอาคาร' }}</span>
      <span class="text-sm text-gray-500 mx-1">|</span>
      <i class="pi pi-calendar-clock text-blue-400 text-sm ml-1"></i>
      <span class="text-sm font-bold text-blue-600 bg-blue-50 px-3 py-0.5 rounded-full border border-blue-200">{{
        dateRangeLabel }}</span>
    </div>

    <Tabs value="0" lazy>
      <TabList>
        <Tab value="0"><i class="pi pi-th-large mr-2"></i>ภาพรวม (Overview)</Tab>
        <Tab value="1"><i class="pi pi-bolt mr-2"></i>บิลค่าไฟฟ้า (PEA)</Tab>
        <Tab value="2"><i class="pi pi-sun mr-2"></i>พลังงาน Solar</Tab>
      </TabList>

      <TabPanels class="px-0 py-4">
        <!-- Tab: ภาพรวมทั้งหมด (Overview) -->
        <TabPanel value="0">
          <!-- Row 1: KPI Cards — ค่าใช้จ่าย & พลังงาน (4 cards) -->
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
            <Card class="shadow-sm border-t-4 border-blue-500 bg-blue-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      ค่าไฟรวม (กฟน.)
                    </p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ formatCurrency(totalExpense) }}
                    </h3>
                  </div>
                  <div class="w-9 h-9 bg-blue-100 rounded-full flex items-center justify-center text-blue-600">
                    <i class="pi pi-money-bill text-sm"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-indigo-500 bg-indigo-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      ซื้อไฟฟ้า (กฟน.)
                    </p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ totalPeaUnit.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">Unit</span>
                    </h3>
                  </div>
                  <div class="w-9 h-9 bg-indigo-100 rounded-full flex items-center justify-center text-indigo-600">
                    <i class="pi pi-bolt text-sm"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-emerald-500 bg-emerald-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ผลิตจาก Solar</p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ totalSolarUnit.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">kWh</span>
                    </h3>
                  </div>
                  <div class="w-9 h-9 bg-emerald-100 rounded-full flex items-center justify-center text-emerald-600">
                    <i class="pi pi-sun text-sm"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-green-500 bg-green-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      ประหยัดเงินสุทธิ
                    </p>
                    <h3 class="text-xl font-bold text-green-700">
                      {{ formatCurrency(solarSavings) }}
                    </h3>
                    <p class="text-[10px] text-green-600/80 mt-1">
                      Solar × {{ avgCostPerUnit.toFixed(2) }} ฿/kWh
                    </p>
                  </div>
                  <div class="w-9 h-9 bg-green-100 rounded-full flex items-center justify-center text-green-600">
                    <i class="pi pi-check-circle text-sm"></i>
                  </div>
                </div>
              </template>
            </Card>
          </div>

          <!-- Row 2: Energy Flow Cards — Consumption / From Grid / From Solar (3 cards) -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
            <Card class="shadow-sm border-t-4 border-violet-500 bg-violet-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ความต้องการไฟฟ้า</p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{
                        sumConsumptionKwh.toLocaleString(undefined, { maximumFractionDigits: 2 })
                      }}
                      <span class="text-sm font-normal text-gray-500">kWh</span>
                    </h3>
                    <p class="text-[10px] text-violet-600/80 mt-1">ปริมาณการใช้พลังงานรวม</p>
                  </div>
                  <div class="w-9 h-9 bg-violet-100 rounded-full flex items-center justify-center text-violet-600">
                    <i class="pi pi-home text-sm"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-rose-500 bg-rose-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ดึงจากสายส่ง</p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ sumFromGridKwh.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">kWh</span>
                    </h3>
                    <p class="text-[10px] text-rose-600/80 mt-1">พลังงานจากสายส่ง กฟภ.</p>
                  </div>
                  <div class="w-9 h-9 bg-rose-100 rounded-full flex items-center justify-center text-rose-600">
                    <i class="pi pi-bolt text-sm"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-yellow-500 bg-yellow-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ดึงจาก Solar</p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ sumFromSolarKwh.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">kWh</span>
                    </h3>
                    <p class="text-[10px] text-yellow-600/80 mt-1">พลังงานแสงอาทิตย์ที่ใช้โดยตรง</p>
                  </div>
                  <div class="w-9 h-9 bg-yellow-100 rounded-full flex items-center justify-center text-yellow-600">
                    <i class="pi pi-sun text-sm"></i>
                  </div>
                </div>
              </template>
            </Card>
          </div>

          <!-- Row 2: แนวโน้มค่าไฟฟ้ารายเดือน (full width) -->
          <Card class="shadow-sm border-none mb-6">
            <template #title>
              <div class="text-lg font-bold text-gray-700">แนวโน้มค่าไฟฟ้ารายเดือน (บาท)</div>
            </template>
            <template #content>
              <div v-if="isLoading" class="h-64 flex items-center justify-center">
                <i class="pi pi-spin pi-spinner text-4xl text-blue-500"></i>
              </div>
              <div v-else-if="!expenseChartData?.labels?.length"
                class="h-64 flex flex-col items-center justify-center text-gray-400">
                <i class="pi pi-box text-3xl mb-2"></i>
                <p>ไม่มีข้อมูล</p>
              </div>
              <div v-else class="h-64 relative">
                <Chart type="bar" :data="expenseChartData" :options="expenseChartOptions" class="h-full w-full" />
              </div>
            </template>
          </Card>

          <!-- Row 3: หน่วยที่ซื้อ (Unit) | Solar ที่ผลิต (kWh) -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
            <Card class="shadow-sm border-none">
              <template #title>
                <div class="text-base font-bold text-gray-700">
                  <span class="inline-block w-3 h-3 rounded-sm bg-blue-500 mr-2"></span>
                  หน่วยที่ซื้อ กฟน. รายเดือน (Unit)
                </div>
              </template>
              <template #content>
                <div v-if="isLoading" class="h-52 flex items-center justify-center">
                  <i class="pi pi-spin pi-spinner text-3xl text-blue-400"></i>
                </div>
                <div v-else-if="!overviewChartData?.labels?.length"
                  class="h-52 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-box text-2xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-52 relative">
                  <Chart type="bar" :data="overviewChartData" :options="overviewChartOptions" class="h-full w-full" />
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-none">
              <template #title>
                <div class="text-base font-bold text-gray-700">
                  <span class="inline-block w-3 h-3 rounded-sm bg-emerald-500 mr-2"></span>
                  Solar ที่ผลิตรายเดือน (kWh)
                </div>
              </template>
              <template #content>
                <div v-if="isLoading" class="h-52 flex items-center justify-center">
                  <i class="pi pi-spin pi-spinner text-3xl text-emerald-400"></i>
                </div>
                <div v-else-if="!unitTrendChartData?.labels?.length"
                  class="h-52 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-box text-2xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-52 relative">
                  <Chart type="bar" :data="unitTrendChartData" :options="unitTrendChartOptions" class="h-full w-full" />
                </div>
              </template>
            </Card>
          </div>

          <!-- Row 4: แหล่งพลังงาน % | การจัดสรร Solar % -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
            <Card class="shadow-sm border-none">
              <template #title>
                <div class="text-base font-bold text-gray-700">
                  แหล่งที่มาพลังงาน (Demand Mix %)
                </div>
                <p class="text-xs text-gray-400 font-normal mt-0.5">
                  From Grid / From Solar / From Battery
                </p>
              </template>
              <template #content>
                <div v-if="sumConsumptionKwh === 0"
                  class="h-56 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-chart-pie text-3xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-56 relative flex items-center justify-center pb-4">
                  <Chart type="doughnut" :data="solarUsageChartData" :options="percentDoughnutOptions"
                    class="w-full h-52 max-w-56" />
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-none">
              <template #title>
                <div class="text-base font-bold text-gray-700">
                  การจัดสรรพลังงาน Solar (Supply Mix %)
                </div>
                <p class="text-xs text-gray-400 font-normal mt-0.5">
                  To Home / To Grid / To Battery
                </p>
              </template>
              <template #content>
                <div v-if="totalSolarUnit === 0" class="h-56 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-chart-pie text-3xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-56 relative flex items-center justify-center pb-4">
                  <Chart type="doughnut" :data="solarBreakdownChartData" :options="percentDoughnutOptions"
                    class="w-full h-52 max-w-56" />
                </div>
              </template>
            </Card>
          </div>
        </TabPanel>

        <!-- Tab: ค่าไฟฟ้า (PEA) -->
        <TabPanel value="1">
          <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-3 gap-4 mb-4">
            <Card class="shadow-sm border-t-4 border-blue-500">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      ยอดค่าไฟฟ้ารวม (กฟภ.)
                    </p>
                    <h3 class="text-2xl font-bold text-gray-800">
                      {{ formatCurrency(totalExpense) }}
                    </h3>
                    <p class="text-xs text-blue-600 mt-2 font-medium">
                      <i class="pi pi-calculator mr-1"></i>เฉลี่ย
                      {{ avgCostPerUnit.toFixed(2) }} บาท/หน่วย
                    </p>
                  </div>
                  <div class="w-10 h-10 bg-blue-50 rounded-full flex items-center justify-center text-blue-500">
                    <i class="pi pi-money-bill"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-orange-500">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      รวมหน่วย (กฟภ.)
                    </p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ totalPeaUnit.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">Unit</span>
                    </h3>
                    <p class="text-xs text-orange-600 mt-1 font-medium">
                      <i class="pi pi-bolt mr-1"></i>นำเข้าจากสายส่ง
                    </p>
                  </div>
                  <div class="w-10 h-10 bg-orange-50 rounded-full flex items-center justify-center text-orange-500">
                    <i class="pi pi-building"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-teal-400">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      ค่าบริการรายเดือนรวม
                    </p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ formatCurrency(sumMonthlyFeeTotal) }}
                    </h3>
                    <p class="text-xs text-teal-600 mt-1 font-medium">Monthly Service Fee</p>
                  </div>
                  <div class="w-10 h-10 bg-teal-50 rounded-full flex items-center justify-center text-teal-500">
                    <i class="pi pi-receipt"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-rose-400">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">On Peak</p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ sumOnPeakUnits.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">Unit</span>
                    </h3>
                    <p class="text-xs text-rose-500 mt-1 font-medium">
                      <i class="pi pi-sun mr-1"></i>ช่วงชั่วโมงเร่งด่วน
                    </p>
                    <p class="text-xs text-rose-500 mt-1 font-medium">
                      (จันทร์ - ศุกร์ เวลา 09.00 - 22.00 น.)
                    </p>
                  </div>
                  <div class="w-10 h-10 bg-rose-50 rounded-full flex items-center justify-center text-rose-500">
                    <i class="pi pi-bolt"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-indigo-400">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">Off Peak</p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ sumOffPeakUnits.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">Unit</span>
                    </h3>
                    <p class="text-xs text-indigo-500 mt-1 font-medium">
                      <i class="pi pi-moon mr-1"></i>ช่วงนอกเวลาเร่งด่วน
                    </p>
                    <p class="text-xs text-indigo-500 mt-1 font-medium">
                      (จันทร์ - ศุกร์ เวลา 22.00 - 09.00 น. และวันเสาร์-อาทิตย์ วันหยุดราชการ
                      (ไม่รวมวันหยุดชดเชย) ตลอด 24)
                    </p>
                  </div>
                  <div class="w-10 h-10 bg-indigo-50 rounded-full flex items-center justify-center text-indigo-500">
                    <i class="pi pi-bolt"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-amber-400">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ค่า Ft รวม</p>
                    <h3 class="text-xl font-bold text-gray-800">
                      {{ formatCurrency(sumFtAmountTotal) }}
                    </h3>
                    <p class="text-xs text-amber-600 mt-1 font-medium">ค่าไฟฟ้าผันแปร (Ft)</p>
                  </div>
                  <div class="w-10 h-10 bg-amber-50 rounded-full flex items-center justify-center text-amber-500">
                    <i class="pi pi-chart-line"></i>
                  </div>
                </div>
              </template>
            </Card>
          </div>

          <!-- กราฟ Unit และ Amount รายเดือน (PEA) -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
            <Card class="shadow-sm border-none">
              <template #title>
                <div class="text-lg font-bold text-gray-700">
                  แนวโน้มหน่วยไฟฟ้าที่ซื้อ (Unit) รายเดือน
                </div>
              </template>
              <template #content>
                <div v-if="isLoading" class="h-64 flex items-center justify-center">
                  <i class="pi pi-spin pi-spinner text-4xl text-blue-500"></i>
                </div>
                <div v-else-if="!peaUnitTrendChartData?.labels?.length"
                  class="h-64 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-box text-3xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-64 relative">
                  <Chart type="bar" :data="peaUnitTrendChartData" :options="peaUnitTrendChartOptions"
                    class="h-full w-full" />
                </div>
              </template>
            </Card>
            <Card class="shadow-sm border-none">
              <template #title>
                <div class="text-lg font-bold text-gray-700">
                  แนวโน้มหน่วยไฟฟ้าที่ซื้อ (บาท) รายเดือน
                </div>
              </template>
              <template #content>
                <div v-if="isLoading" class="h-64 flex items-center justify-center">
                  <i class="pi pi-spin pi-spinner text-4xl text-orange-500"></i>
                </div>
                <div v-else-if="!expenseChartData?.labels?.length"
                  class="h-64 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-box text-3xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-64 relative">
                  <Chart type="bar" :data="expenseChartData" :options="expenseChartOptions" class="h-full w-full" />
                </div>
              </template>
            </Card>
          </div>

          <!-- On/Off Peak Chart -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
            <!-- Stacked Bar รายเดือน -->
            <Card class="shadow-sm border-none lg:col-span-2">
              <template #title>
                <div class="text-lg font-bold text-gray-700">
                  หน่วยไฟฟ้า On Peak / Off Peak รายเดือน
                </div>
              </template>
              <template #content>
                <div v-if="isLoading" class="h-64 flex items-center justify-center">
                  <i class="pi pi-spin pi-spinner text-4xl text-orange-400"></i>
                </div>
                <div v-else-if="!onOffPeakChartData?.labels?.length"
                  class="h-64 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-box text-3xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-64 relative">
                  <Chart type="bar" :data="onOffPeakChartData" :options="onOffPeakChartOptions" class="h-full w-full" />
                </div>
              </template>
            </Card>

            <!-- Donut สัดส่วนรวม -->
            <Card class="shadow-sm border-none">
              <template #title>
                <div class="text-lg font-bold text-gray-700">สัดส่วน On / Off Peak</div>
              </template>
              <template #content>
                <div v-if="isLoading" class="h-64 flex items-center justify-center">
                  <i class="pi pi-spin pi-spinner text-4xl text-orange-400"></i>
                </div>
                <div v-else-if="!onOffPeakDoughnutData?.datasets?.[0]?.data?.some((v: number) => v > 0)"
                  class="h-64 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-box text-3xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="relative">
                  <div class="h-52 relative">
                    <Chart type="doughnut" :data="onOffPeakDoughnutData" :options="percentDoughnutOptions"
                      class="h-full w-full" />
                  </div>
                  <!-- ตัวเลขสรุปใต้ donut -->
                  <div class="grid grid-cols-2 gap-2 mt-3 text-center text-sm">
                    <div class="rounded-lg bg-orange-50 py-2">
                      <p class="text-[11px] text-orange-600 font-semibold uppercase">On Peak</p>
                      <p class="font-bold text-orange-700">
                        {{ sumOnPeakUnits.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                        <span class="text-xs font-normal">Unit</span>
                      </p>
                      <p class="text-[11px] text-orange-500 font-semibold">
                        {{
                          sumOnPeakUnits + sumOffPeakUnits > 0
                            ? (
                              (sumOnPeakUnits / (sumOnPeakUnits + sumOffPeakUnits)) *
                              100
                            ).toFixed(1)
                            : '0.0'
                        }}%
                      </p>
                    </div>
                    <div class="rounded-lg bg-blue-50 py-2">
                      <p class="text-[11px] text-blue-600 font-semibold uppercase">Off Peak</p>
                      <p class="font-bold text-blue-700">
                        {{ sumOffPeakUnits.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                        <span class="text-xs font-normal">Unit</span>
                      </p>
                      <p class="text-[11px] text-blue-500 font-semibold">
                        {{
                          sumOnPeakUnits + sumOffPeakUnits > 0
                            ? (
                              (sumOffPeakUnits / (sumOnPeakUnits + sumOffPeakUnits)) *
                              100
                            ).toFixed(1)
                            : '0.0'
                        }}%
                      </p>
                    </div>
                  </div>
                </div>
              </template>
            </Card>
          </div>

          <div class="grid grid-cols-1 gap-6 mb-6">
            <Card class="shadow-sm border-none">
              <template #title>
                <div class="text-lg font-bold text-gray-700">
                  จัดอันดับอาคารที่เสียค่าไฟฟ้าติดอันดับ (บาท)
                </div>
              </template>
              <template #content>
                <div v-if="isLoading" class="h-64 flex items-center justify-center">
                  <i class="pi pi-spin pi-spinner text-4xl text-orange-500"></i>
                </div>
                <div v-else-if="buildingChartData?.labels?.length === 0"
                  class="h-64 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-align-left text-3xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-64 relative">
                  <Chart type="bar" :data="buildingChartData" :options="buildingChartOptions" class="h-full w-full" />
                </div>
              </template>
            </Card>
          </div>
        </TabPanel>

        <!-- Tab: Solar -->
        <TabPanel value="2">
          <!-- แถว 1: การผลิต / การใช้พลังงาน / ดึงจาก Solar (3 cards) -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4 mt-2">
            <Card class="shadow-sm border-t-4 border-emerald-500 bg-emerald-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      การผลิตพลังงานรวม
                    </p>
                    <h3 class="text-2xl font-bold text-gray-800">
                      {{ totalSolarUnit.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">kWh</span>
                    </h3>
                    <p class="text-[10px] text-emerald-600/80 mt-1">พลังงานที่ผลิตได้จากแผงโซลาร์</p>
                  </div>
                  <div class="w-10 h-10 bg-emerald-100 rounded-full flex items-center justify-center text-emerald-600">
                    <i class="pi pi-sun"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-indigo-500 bg-indigo-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      ความต้องการไฟฟ้ารวม
                    </p>
                    <h3 class="text-2xl font-bold text-gray-800">
                      {{
                        sumConsumptionKwh.toLocaleString(undefined, { maximumFractionDigits: 0 })
                      }}
                      <span class="text-sm font-normal text-gray-500">kWh</span>
                    </h3>
                    <p class="text-[10px] text-indigo-600/80 mt-1">ปริมาณการใช้พลังงานทั้งหมด</p>
                  </div>
                  <div class="w-10 h-10 bg-indigo-100 rounded-full flex items-center justify-center text-indigo-600">
                    <i class="pi pi-home"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-rose-500 bg-rose-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      ดึงจากสายส่ง กฟภ.
                    </p>
                    <h3 class="text-2xl font-bold text-gray-800">
                      {{ sumFromGridKwh.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">kWh</span>
                    </h3>
                    <p class="text-[10px] text-rose-600/80 mt-1">พลังงานที่ซื้อจากสายส่งไฟฟ้า</p>
                  </div>
                  <div class="w-10 h-10 bg-rose-100 rounded-full flex items-center justify-center text-rose-600">
                    <i class="pi pi-bolt"></i>
                  </div>
                </div>
              </template>
            </Card>


          </div>

          <!-- แถว 2: ดึงจากสายส่ง / พลังงานส่วนเกิน (2 cards) -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
            <Card class="shadow-sm border-t-4 border-green-500 bg-green-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      ดึงจาก Solar
                    </p>
                    <h3 class="text-2xl font-bold text-gray-800">
                      {{ sumFromSolarKwh.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                      <span class="text-sm font-normal text-gray-500">kWh</span>
                    </h3>
                    <p class="text-[10px] text-green-600/80 mt-1">Solar ที่ใช้ในอาคารโดยตรง</p>
                  </div>
                  <div class="w-10 h-10 bg-green-100 rounded-full flex items-center justify-center text-green-600">
                    <i class="pi pi-sun text-sm"></i>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-amber-500 bg-amber-50/20">
              <template #content>
                <div class="flex justify-between items-start">
                  <div>
                    <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                      พลังงานส่วนเกิน
                    </p>
                    <h3 class="text-2xl font-bold text-gray-800">
                      {{
                        (sumToGridKwh + sumToBatteryKwh).toLocaleString(undefined, {
                          maximumFractionDigits: 0,
                        })
                      }}
                      <span class="text-sm font-normal text-gray-500">kWh</span>
                    </h3>
                    <p class="text-[10px] text-amber-600/80 mt-1">จ่ายคืนสายส่ง + เก็บในแบตเตอรี่</p>
                  </div>
                  <div class="w-10 h-10 bg-amber-100 rounded-full flex items-center justify-center text-amber-600">
                    <i class="pi pi-upload"></i>
                  </div>
                </div>
              </template>
            </Card>
          </div>

          <!-- ESG and Charts in 3 Columns -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
            <Card
              class="shadow-sm border-none lg:col-span-1 border-t-4 border-teal-500 bg-teal-50/20 flex flex-col items-center justify-center relative overflow-hidden">
              <template #content>
                <i
                  class="pi pi-globe absolute -bottom-10 -right-10 text-[10rem] text-teal-500/10 pointer-events-none"></i>
                <div class="text-center py-8">
                  <p class="text-sm text-teal-600 uppercase font-bold tracking-wider mb-2">
                    รักษ์โลก (ESG Impact)
                  </p>
                  <h3 class="text-5xl font-extrabold text-teal-900 mb-2">
                    {{ carbonSaved.toLocaleString(undefined, { maximumFractionDigits: 0 }) }}
                    <span class="text-xl font-normal text-teal-700">kgCO₂e</span>
                  </h3>
                  <p class="text-sm text-teal-600 bg-teal-100/50 inline-block px-3 py-1 rounded-full mb-4">
                    ลดการปล่อยก๊าซเรือนกระจก
                  </p>
                  <br />
                  <div class="inline-flex items-center gap-2 mt-2">
                    <i class="pi pi-check-circle text-green-500"></i>
                    <p class="text-sm font-semibold text-gray-700">
                      ประหยัดเงินแล้ว
                      <span class="text-green-600 text-lg">{{ formatCurrency(solarSavings) }}</span>
                    </p>
                  </div>

                  <p class="text-[10px] text-teal-600/70 mt-4 leading-tight text-center space-y-1">
                    <span>* การลดคาร์บอน: ผลิตจาก Solar (kWh) × 0.5 kgCO₂e/kWh</span><br />
                    <span>* ประหยัดเงิน: ผลิตจาก Solar (kWh) × ค่าไฟเฉลี่ย กฟภ. ({{
                      avgCostPerUnit.toFixed(2)
                    }}
                      ฿/kWh)</span>
                  </p>
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-none lg:col-span-1">
              <template #title>
                <div class="text-sm font-bold text-gray-700 text-center">
                  แหล่งที่มาของพลังงานที่ใช้ (Demand Mix)
                </div>
              </template>
              <template #content>
                <div v-if="sumConsumptionKwh === 0"
                  class="h-64 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-chart-pie text-3xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-64 relative flex items-center justify-center pb-4">
                  <Chart type="doughnut" :data="solarUsageChartData" :options="mixChartOptions"
                    class="w-full h-56 max-w-60" />
                </div>
              </template>
            </Card>

            <Card class="shadow-sm border-none lg:col-span-1">
              <template #title>
                <div class="text-sm font-bold text-gray-700 text-center">
                  การจัดสรรพลังงาน Solar (Supply Mix)
                </div>
              </template>
              <template #content>
                <div v-if="totalSolarUnit === 0" class="h-64 flex flex-col items-center justify-center text-gray-400">
                  <i class="pi pi-chart-pie text-3xl mb-2"></i>
                  <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-64 relative flex items-center justify-center pb-4">
                  <Chart type="doughnut" :data="solarBreakdownChartData" :options="mixChartOptions"
                    class="w-full h-56 max-w-60" />
                </div>
              </template>
            </Card>
          </div>

          <Card class="shadow-sm border-none mb-6">
            <template #title>
              <div class="text-lg font-bold text-gray-700">แนวโน้มการผลิต Solar รายเดือน</div>
            </template>
            <template #content>
              <div v-if="isLoading" class="h-64 flex items-center justify-center">
                <i class="pi pi-spin pi-spinner text-green-500 text-4xl"></i>
              </div>
              <div v-else-if="solarChartData?.labels?.length === 0"
                class="h-64 flex flex-col items-center justify-center text-gray-400">
                <i class="pi pi-box text-3xl mb-2"></i>
                <p>ไม่มีข้อมูล</p>
              </div>
              <div v-else class="h-64 relative">
                <Chart type="line" :data="solarChartData" :options="solarChartOptions" class="h-full w-full" />
              </div>
            </template>
          </Card>
        </TabPanel>
      </TabPanels>
    </Tabs>
  </div>
</template>
