<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import KpiCard from '@/components/dashboard/KpiCard.vue'
import Chart from 'primevue/chart'
import { useRoute, useRouter } from 'vue-router'
import api from '@/services/api'

defineOptions({ name: 'TvDisplay' })

const route = useRoute()
const router = useRouter()
const dashboardId = route.params.id as string

// --------------- Types ---------------
interface WidgetData {
  widgetType: string
  label: string
  sortOrder: number
  data: Record<string, number>
  [key: string]: any
}

interface SummaryResponse {
  id: string
  name: string
  description?: string
  refreshIntervalSeconds: number
  slideDurationSeconds: number
  generatedAt: string
  widgets: WidgetData[]
}

const WIDGET_META: Record<
  string,
  { icon: string; color: string; gradientFrom: string; gradientTo: string }
> = {
  electricity: {
    icon: 'pi-bolt',
    color: '#FBBF24',
    gradientFrom: '#78350F',
    gradientTo: '#1C1917',
  },
  solar: { icon: 'pi-sun', color: '#FDE047', gradientFrom: '#713F12', gradientTo: '#1C1917' },
  water: {
    icon: 'pi-wave-pulse',
    color: '#22D3EE',
    gradientFrom: '#164E63',
    gradientTo: '#0C1A2E',
  },
  fuel: { icon: 'pi-car', color: '#FB7185', gradientFrom: '#881337', gradientTo: '#1C1917' },
  maintenance: {
    icon: 'pi-wrench',
    color: '#FB923C',
    gradientFrom: '#7C2D12',
    gradientTo: '#1C1917',
  },
  meeting: { icon: 'pi-users', color: '#818CF8', gradientFrom: '#1E1B4B', gradientTo: '#0F0F23' },
  postal: { icon: 'pi-envelope', color: '#60A5FA', gradientFrom: '#1E3A5F', gradientTo: '#0C1A2E' },
  saraban: {
    icon: 'pi-folder-open',
    color: '#C084FC',
    gradientFrom: '#3B0764',
    gradientTo: '#1C1917',
  },
}

// --------------- State ---------------
// KPI Card & Trend Chart
const kpiByType = ref<Record<string, any>>({})
const chartDataByType = ref<Record<string, any>>({})
const chartOptionsByType = ref<Record<string, any>>({})
const summary = ref<SummaryResponse | null>(null)
const isLoading = ref(true)
const hasError = ref(false)
const currentSlide = ref(0)
const progress = ref(0)

// --- Widget Types ทั้งหมด ---
const ALL_WIDGET_TYPES = [
  'electricity',
  'solar',
  'water',
  'fuel',
  'maintenance',
  'meeting',
  'postal',
  'saraban',
]

// --- Widget Types ที่เลือกให้แสดง (อ่านจาก localStorage หรือแสดงทั้งหมดถ้าไม่มี config) ---
const selectedWidgetTypes = ref<string[]>([])
function loadSelectedWidgets() {
  const raw = localStorage.getItem('tv_selected_widgets')
  if (raw) {
    try {
      const arr = JSON.parse(raw)
      if (Array.isArray(arr)) {
        selectedWidgetTypes.value = arr.filter((t) => ALL_WIDGET_TYPES.includes(t))
        return
      }
    } catch {}
  }
  // ถ้าไม่มี config ให้แสดงทั้งหมด
  selectedWidgetTypes.value = [...ALL_WIDGET_TYPES]
}
loadSelectedWidgets()

// --- Widgets แยกตามระบบ (Section) ---
const SYSTEMS = [
  { type: 'electricity', label: 'ระบบไฟฟ้า', color: 'border-blue-500' },
  { type: 'solar', label: 'โซลาร์เซลล์', color: 'border-yellow-400' },
  { type: 'water', label: 'ระบบน้ำประปา', color: 'border-cyan-500' },
  { type: 'fuel', label: 'ระบบน้ำมันเชื้อเพลิง', color: 'border-pink-500' },
  { type: 'maintenance', label: 'ซ่อมบำรุง', color: 'border-orange-500' },
  { type: 'meeting', label: 'ห้องประชุม', color: 'border-indigo-500' },
  { type: 'postal', label: 'ไปรษณีย์', color: 'border-blue-400' },
  { type: 'saraban', label: 'สารบรรณ', color: 'border-purple-400' },
]

const widgetsByType = computed<Record<string, WidgetData>>(() => {
  if (!summary.value) return {}
  const widgets = summary.value.widgets || []
  const map: Record<string, WidgetData> = {}
  widgets.forEach((w) => {
    map[w.widgetType] = w
  })
  return map
})

// --- ดึง label widget ตามประเภท ---
function getWidgetLabel(type: string): string {
  switch (type) {
    case 'electricity':
      return 'ค่าไฟฟ้า'
    case 'solar':
      return 'โซลาร์เซลล์'
    case 'water':
      return 'ค่าน้ำประปา'
    case 'fuel':
      return 'น้ำมันเชื้อเพลิง'
    case 'maintenance':
      return 'ซ่อมบำรุง'
    case 'meeting':
      return 'ห้องประชุม'
    case 'postal':
      return 'ไปรษณีย์'
    case 'saraban':
      return 'สารบรรณ'
    default:
      return type
  }
}

// --------------- Clock ---------------
const now = ref(new Date())
let clockTimer: ReturnType<typeof setInterval> | null = null

const THAI_MONTHS = [
  'มกราคม',
  'กุมภาพันธ์',
  'มีนาคม',
  'เมษายน',
  'พฤษภาคม',
  'มิถุนายน',
  'กรกฎาคม',
  'สิงหาคม',
  'กันยายน',
  'ตุลาคม',
  'พฤศจิกายน',
  'ธันวาคม',
]
const THAI_DAYS = ['อาทิตย์', 'จันทร์', 'อังคาร', 'พุธ', 'พฤหัสบดี', 'ศุกร์', 'เสาร์']

const timeStr = computed(() =>
  now.value.toLocaleTimeString('th-TH', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }),
)
const dateStr = computed(() => {
  const d = now.value
  return `วัน${THAI_DAYS[d.getDay()]}ที่ ${d.getDate()} ${THAI_MONTHS[d.getMonth()]} ${d.getFullYear() + 543}`
})
const currentMonthLabel = computed(() => {
  const d = now.value
  return `${THAI_MONTHS[d.getMonth()]} ${d.getFullYear() + 543}`
})

// --------------- Data Fetch ---------------
let refreshTimer: ReturnType<typeof setInterval> | null = null
let slideTimer: ReturnType<typeof setInterval> | null = null
let progressTimer: ReturnType<typeof setInterval> | null = null

// --- ดึงข้อมูลแต่ละระบบ, KPI, และแนวโน้ม ---
const fetchSummary = async () => {
  isLoading.value = true
  try {
    // ดึงข้อมูลย้อนหลังสูงสุด 12 เดือน (ล่าสุดที่มีในแต่ละระบบ)
    const nowDate = new Date()
    const getMonthRange = (date: Date) => {
      const year = date.getFullYear()
      const month = (date.getMonth() + 1).toString().padStart(2, '0')
      const fromDate = `${year}-${month}-01T00:00:00`
      const toDate = `${year}-${month}-31T23:59:59`
      return { fromDate, toDate }
    }
    async function fetchLatest(apiPath: string) {
      let date = new Date(nowDate)
      for (let i = 0; i < 12; i++) {
        const { fromDate, toDate } = getMonthRange(date)
        const res = await api.get(apiPath, { params: { fromDate, toDate } })
        if (res.data.items && res.data.items.length > 0) return res.data.items
        date.setMonth(date.getMonth() - 1)
      }
      return []
    }
    // ดึงข้อมูลแต่ละระบบ (ย้อนหลัง)
    const [elecItems, solarItems, waterItems, fuelItems, peaTrendRes] = await Promise.all([
      fetchLatest('/ElectricityBill'),
      fetchLatest('/SolarProduction'),
      fetchLatest('/WaterRecord'),
      fetchLatest('/FuelRecord'),
      api.get('/ElectricityBill/monthly-unit-trend'),
    ])
    // KPI & Chart per system
    // Electricity
    let totalExpense = 0,
      totalPeaUnit = 0,
      onPeak = 0,
      offPeak = 0
    elecItems.forEach((r: any) => {
      totalExpense += r.peaAmount || 0
      totalPeaUnit += r.peaUnitUsed || 0
      onPeak += r.onPeakUnits || 0
      offPeak += r.offPeakUnits || 0
    })
    kpiByType.value['electricity'] = { totalExpense, totalPeaUnit, onPeak, offPeak }
    const elecTrend = peaTrendRes.data || []
    chartDataByType.value['electricity'] = {
      labels: elecTrend.map((x: any) => x.label),
      datasets: [
        {
          type: 'bar',
          label: 'หน่วยที่ซื้อ กฟภ. (Unit)',
          backgroundColor: '#3b82f6',
          borderRadius: 4,
          data: elecTrend.map((x: any) => Number(x.totalUnit) || 0),
        },
      ],
    }
    chartOptionsByType.value['electricity'] = {
      maintainAspectRatio: false,
      aspectRatio: 0.8,
      plugins: { legend: { display: false } },
      scales: {
        x: { ticks: { color: '#6b7280' }, grid: { display: false } },
        y: {
          title: { display: true, text: 'หน่วย (Unit)' },
          ticks: { color: '#6b7280' },
          grid: { color: '#f3f4f6' },
        },
      },
    }
    // Solar
    let totalSolar = 0,
      totalSelfUseKwh = 0,
      toGrid = 0
    solarItems.forEach((r: any) => {
      totalSolar += r.solarUnitProduced || 0
      totalSelfUseKwh += r.toHomeWh ? r.toHomeWh / 1000 : 0
      toGrid += r.toGridWh ? r.toGridWh / 1000 : 0
    })
    kpiByType.value['solar'] = { totalSolar, totalSelfUseKwh, toGrid }
    // (ตัวอย่าง: ไม่มี trend chart สำหรับ solar, ถ้ามีข้อมูลเพิ่มค่อยเติม)
    // Water
    let waterTotalAmount = 0,
      waterTotalUnit = 0
    waterItems.forEach((r: any) => {
      waterTotalAmount += r.totalAmount || 0
      waterTotalUnit += r.totalUnit || 0
    })
    kpiByType.value['water'] = { waterTotalAmount, waterTotalUnit }
    // Fuel
    let fuelTotalAmount = 0,
      fuelTotalLiters = 0
    fuelItems.forEach((r: any) => {
      fuelTotalAmount += r.totalAmount || 0
      fuelTotalLiters += r.totalLiters || 0
    })
    kpiByType.value['fuel'] = { fuelTotalAmount, fuelTotalLiters }
    // Map ข้อมูลเป็น widget (เหมือนเดิม)
    const widgets = []
    let totalAmount = 0,
      totalKwh = 0
    elecItems.forEach((r: any) => {
      totalAmount += r.peaAmount || 0
      totalKwh += r.peaUnitUsed || 0
    })
    widgets.push({
      widgetType: 'electricity',
      label: 'ค่าไฟฟ้า',
      sortOrder: 0,
      data: { totalAmount, totalKwh },
    })
    let totalProductionKwhWidget = 0,
      solarSelfUseKwhWidget = 0,
      solarToGridWidget = 0
    solarItems.forEach((r: any) => {
      totalProductionKwhWidget += r.solarUnitProduced || 0
      solarSelfUseKwhWidget += r.toHomeWh ? r.toHomeWh / 1000 : 0
      solarToGridWidget += r.toGridWh ? r.toGridWh / 1000 : 0
    })
    widgets.push({
      widgetType: 'solar',
      label: 'โซลาร์เซลล์',
      sortOrder: 1,
      data: {
        totalProductionKwh: totalProductionKwhWidget,
        totalSelfUseKwh: solarSelfUseKwhWidget,
        toGrid: solarToGridWidget,
      },
    })
    let waterWidgetAmount = 0,
      waterWidgetUnit = 0
    waterItems.forEach((r: any) => {
      waterWidgetAmount += r.totalAmount || 0
      waterWidgetUnit += r.totalUnit || 0
    })
    widgets.push({
      widgetType: 'water',
      label: 'ค่าน้ำประปา',
      sortOrder: 2,
      data: { totalAmount: waterWidgetAmount, totalUnit: waterWidgetUnit },
    })
    let fuelWidgetAmount = 0,
      fuelWidgetLiters = 0
    fuelItems.forEach((r: any) => {
      fuelWidgetAmount += r.totalAmount || 0
      fuelWidgetLiters += r.totalLiters || 0
    })
    widgets.push({
      widgetType: 'fuel',
      label: 'น้ำมันเชื้อเพลิง',
      sortOrder: 3,
      data: { totalAmount: fuelWidgetAmount, totalLiters: fuelWidgetLiters },
    })
    summary.value = {
      id: 'tv-dashboard',
      name: 'TV Dashboard',
      refreshIntervalSeconds: 60,
      slideDurationSeconds: 10,
      generatedAt: new Date().toISOString(),
      widgets,
    }
    hasError.value = false
    if (currentSlide.value >= (summary.value?.widgets.length ?? 1)) {
      currentSlide.value = 0
    }
  } catch (e) {
    hasError.value = true
  } finally {
    isLoading.value = false
  }
}

// --------------- Slide Timer ---------------
const startSlideTimer = () => {
  if (slideTimer) clearInterval(slideTimer)
  if (progressTimer) clearInterval(progressTimer)

  const duration = (summary.value?.slideDurationSeconds ?? 10) * 1000
  progress.value = 0
  const step = 100 / (duration / 50)

  progressTimer = setInterval(() => {
    progress.value += step
    if (progress.value >= 100) {
      progress.value = 0
    }
  }, 50)

  slideTimer = setInterval(() => {
    const len = summary.value?.widgets.length ?? 1
    currentSlide.value = (currentSlide.value + 1) % len
    progress.value = 0
  }, duration)
}

// --------------- Lifecycle ---------------
onMounted(async () => {
  clockTimer = setInterval(() => {
    now.value = new Date()
  }, 1000)
  await fetchSummary()
  startSlideTimer()

  const refreshMs = (summary.value?.refreshIntervalSeconds ?? 60) * 1000
  refreshTimer = setInterval(fetchSummary, refreshMs)
})

onUnmounted(() => {
  if (clockTimer) clearInterval(clockTimer)
  if (refreshTimer) clearInterval(refreshTimer)
  if (slideTimer) clearInterval(slideTimer)
  if (progressTimer) clearInterval(progressTimer)
})

// --------------- Navigation ---------------
const goTo = (index: number) => {
  currentSlide.value = index
  progress.value = 0
  startSlideTimer()
}

const goBack = () => router.push('/tv-dashboard')

// --------------- Widget Renderer Helpers ---------------
const fmt = (n: number | undefined, decimals = 0) => {
  if (n === undefined || n === null) return '0'
  return n.toLocaleString('th-TH', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })
}
const fmtBaht = (n: number | undefined) => `฿${fmt(n)}`

interface Metric {
  label: string
  value: string
  unit?: string
  accent?: boolean
}

const widgetHasError = (widget: WidgetData) => 'error' in widget.data

const getMetrics = (widget: WidgetData): Metric[] => {
  if (widgetHasError(widget)) return []
  const d = widget.data || {}
  switch (widget.widgetType) {
    case 'electricity':
      return [
        { label: 'ค่าไฟฟ้ารวม', value: fmtBaht(d.totalAmount ?? 0), accent: true },
        { label: 'ใช้ไฟฟ้า', value: fmt(d.totalKwh ?? 0, 0), unit: 'kWh' },
        { label: 'On Peak', value: fmt(d.onPeak ?? 0, 0), unit: 'kWh' },
        { label: 'Off Peak', value: fmt(d.offPeak ?? 0, 0), unit: 'kWh' },
      ]
    case 'solar':
      return [
        { label: 'ผลิตไฟฟ้า', value: fmt(d.totalProductionKwh ?? 0, 1), unit: 'kWh', accent: true },
        { label: 'ใช้เอง', value: fmt(d.totalSelfUseKwh ?? 0, 1), unit: 'kWh' },
        { label: 'ส่งขาย', value: fmt(d.toGrid ?? 0, 1), unit: 'kWh' },
      ]
    case 'water':
      return [
        { label: 'ค่าน้ำรวม', value: fmtBaht(d.totalAmount ?? 0), accent: true },
        { label: 'ใช้น้ำ', value: fmt(d.totalUnit ?? 0, 0), unit: 'หน่วย' },
      ]
    case 'fuel':
      return [
        { label: 'ค่าน้ำมันรวม', value: fmtBaht(d.totalAmount ?? 0), accent: true },
        { label: 'ใช้น้ำมัน', value: fmt(d.totalLiters ?? 0, 1), unit: 'ลิตร' },
      ]
    case 'maintenance':
      return [
        { label: 'รอดำเนินการ', value: fmt(d.pendingCount ?? 0), unit: 'งาน', accent: true },
        { label: 'กำลังดำเนินการ', value: fmt(d.inProgressCount ?? 0), unit: 'งาน' },
        { label: 'เสร็จสิ้น', value: fmt(d.completedCount ?? 0), unit: 'งาน' },
      ]
    case 'meeting':
      return [
        { label: 'ใช้ห้องประชุม', value: fmt(d.totalUsageCount ?? 0), unit: 'ครั้ง', accent: true },
        { label: 'จำนวนห้อง', value: fmt(d.roomCount ?? 0), unit: 'ห้อง' },
      ]
    case 'postal':
      return [
        { label: 'รับเข้า', value: fmt(d.incomingTotal ?? 0), unit: 'ฉบับ', accent: true },
        { label: 'ส่งออก', value: fmt(d.outgoingTotal ?? 0), unit: 'ฉบับ' },
      ]
    case 'saraban':
      return [
        { label: 'รับเข้า', value: fmt(d.receivedCount ?? 0), unit: 'ฉบับ', accent: true },
        { label: 'ส่งต่อ', value: fmt(d.forwardedCount ?? 0), unit: 'ฉบับ' },
      ]
    default:
      return []
  }
}

// Slide dots: เฉพาะ widget ที่เลือกและมีข้อมูลจริง
const displayedWidgetTypes = computed(() => {
  return SYSTEMS.map((sys) => sys.type).filter(
    (type) => selectedWidgetTypes.value.includes(type) && widgetsByType.value[type],
  )
})
const currentMeta = computed(() => {
  const type = displayedWidgetTypes.value[currentSlide.value]
  return type ? (WIDGET_META[type] ?? WIDGET_META.electricity) : null
})
</script>

<template>
  <div class="tv-bg min-h-screen flex flex-col">
    <!-- Top Bar -->
    <header
      class="w-full flex items-center justify-between px-4 md:px-10 py-3 md:py-5 bg-black/30 backdrop-blur-md"
    >
      <div class="flex items-center gap-3">
        <button class="text-white/50 hover:text-white transition-colors text-lg" @click="goBack">
          <span class="pi pi-arrow-left" />
        </button>
        <span class="text-white font-bold text-xl md:text-2xl tracking-wide drop-shadow">{{
          summary?.name ?? 'TV Dashboard'
        }}</span>
      </div>
      <div class="text-right">
        <div
          class="text-white text-2xl md:text-4xl font-mono font-bold tracking-widest drop-shadow"
        >
          {{ timeStr }}
        </div>
        <div class="text-white/70 text-xs md:text-base mt-0.5">{{ dateStr }}</div>
      </div>
    </header>

    <!-- Main Content -->
    <main
      class="flex-1 flex flex-col items-center w-full px-2 sm:px-4 md:px-8 py-3 md:py-8 gap-4 md:gap-8 overflow-auto"
    >
      <!-- Loading/Error -->
      <div v-if="isLoading" class="flex flex-col items-center justify-center min-h-[200px]">
        <span class="pi pi-spinner pi-spin text-white text-5xl mb-2" />
        <p class="text-white/60 text-lg">กำลังโหลดข้อมูล...</p>
      </div>
      <div v-else-if="hasError" class="flex flex-col items-center justify-center min-h-[200px]">
        <span class="pi pi-exclamation-triangle text-red-400 text-6xl mb-2" />
        <p class="text-white/80 text-xl">ไม่สามารถโหลดข้อมูลได้</p>
        <p class="text-white/40 text-sm">ระบบจะลองใหม่อัตโนมัติ</p>
      </div>
      <template v-else>
        <section v-for="sys in SYSTEMS" :key="sys.type" class="w-full max-w-7xl mb-8">
          <template
            v-if="
              selectedWidgetTypes.includes(sys.type) &&
              widgetsByType.value &&
              widgetsByType.value[sys.type]
            "
          >
            <div :class="`mb-4 border-l-8 pl-4 ${sys.color}`">
              <h2 class="text-white text-2xl font-bold drop-shadow mb-1">{{ sys.label }}</h2>
              <p class="text-white/60 text-xs md:text-sm mb-2">
                ข้อมูลเดือน {{ currentMonthLabel }}
              </p>
            </div>
            <div
              class="tv-system-card rounded-2xl p-4 flex flex-col items-center shadow-lg"
              :style="`background: linear-gradient(135deg, ${WIDGET_META[sys.type]?.gradientFrom ?? '#222'} 0%, ${WIDGET_META[sys.type]?.gradientTo ?? '#111'} 100%)`"
            >
              <div
                class="w-14 h-14 md:w-20 md:h-20 rounded-full flex items-center justify-center mb-3 shadow-xl"
                :style="`background: ${WIDGET_META[sys.type]?.color ?? '#fff'}22; border: 2px solid ${WIDGET_META[sys.type]?.color ?? '#fff'}55`"
              >
                <span
                  class="pi text-3xl md:text-5xl"
                  :class="WIDGET_META[sys.type]?.icon"
                  :style="`color: ${WIDGET_META[sys.type]?.color ?? '#fff'}`"
                />
              </div>
              <h4 class="text-white text-lg md:text-xl font-bold mb-1 drop-shadow">
                {{ getWidgetLabel(sys.type) }}
              </h4>
              <div
                v-if="
                  widgetsByType.value &&
                  widgetsByType.value[sys.type] &&
                  widgetHasError(widgetsByType.value[sys.type])
                "
                class="flex flex-col items-center opacity-60"
              >
                <span class="pi pi-database text-2xl md:text-3xl text-white/40 mb-2" />
                <p class="text-white/50 text-sm md:text-base">ไม่สามารถโหลดข้อมูลได้</p>
              </div>
              <div
                v-else-if="widgetsByType.value && widgetsByType.value[sys.type]"
                class="w-full grid grid-cols-1 gap-2"
              >
                <div
                  v-for="(metric, mi) in getMetrics(widgetsByType.value[sys.type])"
                  :key="mi"
                  class="flex flex-col items-center"
                >
                  <span
                    class="text-white font-bold text-xl md:text-2xl"
                    :class="metric.accent ? 'text-2xl md:text-3xl text-yellow-300' : ''"
                    >{{ metric.value }}</span
                  >
                  <span class="text-white/70 text-xs md:text-sm"
                    >{{ metric.label }} <span v-if="metric.unit">({{ metric.unit }})</span></span
                  >
                </div>
              </div>
            </div>
          </template>
        </section>

        <!-- System Sections: แยกแต่ละระบบ ไม่รวมกัน -->
        <section v-for="sys in SYSTEMS" :key="sys.type" class="w-full max-w-7xl mb-8">
          <template
            v-if="
              selectedWidgetTypes.includes(sys.type) &&
              widgetsByType.value &&
              widgetsByType.value[sys.type]
            "
          >
            <div :class="`mb-4 border-l-8 pl-4 ${sys.color}`">
              <h2 class="text-white text-2xl font-bold drop-shadow mb-1">{{ sys.label }}</h2>
              <p class="text-white/60 text-xs md:text-sm mb-2">
                ข้อมูลเดือน {{ currentMonthLabel }}
              </p>
            </div>
            <div
              class="tv-system-card rounded-2xl p-4 flex flex-col items-center shadow-lg"
              :style="`background: linear-gradient(135deg, ${WIDGET_META[sys.type]?.gradientFrom ?? '#222'} 0%, ${WIDGET_META[sys.type]?.gradientTo ?? '#111'} 100%)`"
            >
              <div
                class="w-14 h-14 md:w-20 md:h-20 rounded-full flex items-center justify-center mb-3 shadow-xl"
                :style="`background: ${WIDGET_META[sys.type]?.color ?? '#fff'}22; border: 2px solid ${WIDGET_META[sys.type]?.color ?? '#fff'}55`"
              >
                <span
                  class="pi text-3xl md:text-5xl"
                  :class="WIDGET_META[sys.type]?.icon"
                  :style="`color: ${WIDGET_META[sys.type]?.color ?? '#fff'}`"
                />
              </div>
              <h4 class="text-white text-lg md:text-xl font-bold mb-1 drop-shadow">
                {{ getWidgetLabel(sys.type) }}
              </h4>
              <div
                v-if="
                  widgetsByType.value &&
                  widgetsByType.value[sys.type] &&
                  widgetHasError(widgetsByType.value[sys.type])
                "
                class="flex flex-col items-center opacity-60"
              >
                <span class="pi pi-database text-2xl md:text-3xl text-white/40 mb-2" />
                <p class="text-white/50 text-sm md:text-base">ไม่สามารถโหลดข้อมูลได้</p>
              </div>
              <div
                v-else-if="widgetsByType.value && widgetsByType.value[sys.type]"
                class="w-full grid grid-cols-1 gap-2"
              >
                <div
                  v-for="(metric, mi) in getMetrics(widgetsByType.value[sys.type])"
                  :key="mi"
                  class="flex flex-col items-center"
                >
                  <span
                    class="text-white font-bold text-xl md:text-2xl"
                    :class="metric.accent ? 'text-2xl md:text-3xl text-yellow-300' : ''"
                    >{{ metric.value }}</span
                  >
                  <span class="text-white/70 text-xs md:text-sm"
                    >{{ metric.label }} <span v-if="metric.unit">({{ metric.unit }})</span></span
                  >
                </div>
              </div>
            </div>
          </template>
        </section>
      </template>
    </main>

    <!-- Footer: Progress & Slide Dots -->
    <footer class="shrink-0 bg-black/20 backdrop-blur-sm px-2 sm:px-8 py-2 md:py-4 mt-auto">
      <div class="w-full bg-white/10 rounded-full h-1 mb-2 md:mb-4 overflow-hidden">
        <div
          class="h-full rounded-full transition-none"
          :style="`width: ${progress}%; background: ${currentMeta?.color ?? '#818CF8'}`"
        />
      </div>
      <div class="flex items-center justify-center gap-1 md:gap-2">
        <button
          v-for="(type, i) in displayedWidgetTypes"
          :key="type"
          class="rounded-full transition-all duration-300"
          :class="i === currentSlide ? 'w-8 h-3' : 'w-3 h-3'"
          :style="`background: ${i === currentSlide ? (currentMeta?.color ?? '#818CF8') : 'rgba(255,255,255,0.25)'}`"
          @click="goTo(i)"
          :title="getWidgetLabel(type)"
        />
      </div>
      <p class="text-center text-white/30 text-xs mt-1 md:mt-2">
        {{ currentSlide + 1 }} / {{ displayedWidgetTypes.length }} — คลิกจุดเพื่อข้ามสไลด์
      </p>
    </footer>
  </div>
</template>

<style scoped>
.tv-bg {
  background: radial-gradient(ellipse at 60% 0%, #a9772b 0%, #1c1917 100%);
}
.tv-kpi-card {
  background: rgba(255, 255, 255, 0.08) !important;
  border-radius: 1.2rem !important;
  box-shadow: 0 2px 16px 0 #0002;
  border: none !important;
}
.tv-system-card {
  min-height: 220px;
  transition: box-shadow 0.2s;
}
.tv-system-card:hover {
  box-shadow: 0 4px 32px 0 #0004;
}
@media (max-width: 640px) {
  .max-w-6xl {
    max-width: 100vw !important;
  }
  .tv-system-card {
    min-height: 180px;
  }
}
.fade-enter-active,
.fade-leave-active {
  transition:
    opacity 0.5s ease,
    transform 0.5s ease;
}
.fade-enter-from {
  opacity: 0;
  transform: translateY(20px);
}
.fade-leave-to {
  opacity: 0;
  transform: translateY(-20px);
}
</style>
