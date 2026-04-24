<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import api from '@/services/api'
import { toUtcDateOnly, toUtcEndOfDay } from '@/utils/dateUtils'

defineOptions({ name: 'IPPhoneDashboard' })
const toast = useAppToast()

import Card from 'primevue/card'
import Chart from 'primevue/chart'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Select from 'primevue/select'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'

// ─── 1. Interfaces ────────────────────────────────────────────────────────────
interface FetchedCallLog {
  statId: string
  reportMonth: string | null
  extension: string
  answeredInbound: number
  noAnswerInbound: number
  busyInbound: number
  failedInbound: number
  voicemailInbound: number
  totalInbound: number
  answeredOutbound: number
  noAnswerOutbound: number
  busyOutbound: number
  failedOutbound: number
  voicemailOutbound: number
  totalOutbound: number
  totalCalls: number
  totalTalkDuration: string
}
interface DirectoryInfo {
  ownerName: string
  departmentId: string
}
interface MonthlyData {
  inbound: number
  outbound: number
  total: number
}

// ─── 2. State & Auth ──────────────────────────────────────────────────────────
const authStore = useAuthStore()
const currentUserRole = computed(() => authStore.userProfile?.role || 'user')
const isLoading = ref<boolean>(true)

const rawLogs = ref<FetchedCallLog[]>([])
const directoryMap = ref<Record<string, DirectoryInfo>>({})

// KPIs for Overall tab
const totalCallVolume = ref<number>(0)
const sumInbound = ref<number>(0)
const sumOutbound = ref<number>(0)
const sumAnsweredInbound = ref<number>(0)
const sumMissedInbound = ref<number>(0)
const sumBusyInbound = ref<number>(0)
const sumAnsweredOutbound = ref<number>(0)
const sumMissedOutbound = ref<number>(0)
const sumBusyOutbound = ref<number>(0)

// Filters
const getLastMonthRange = (): Date[] => {
  const now = new Date()
  const first = new Date(now.getFullYear(), now.getMonth() - 1, 1)
  const last = new Date(now.getFullYear(), now.getMonth(), 0)
  return [first, last]
}
const selectedDateRange = ref<Date[] | null>(getLastMonthRange())
const filterExt = ref<string>('')
const filterDept = ref<string>('')
const deptList = ref<{ id: string; name: string }[]>([])
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

// Charts for Overall tab
const trendInChartData = ref()
const trendOutChartData = ref()
const trendChartOptions = ref()
const ratioInChartData = ref()
const ratioOutChartData = ref()
const ratioChartOptions = ref()
const topCallerInChartData = ref()
const topCallerOutChartData = ref()
const topCallerChartOptions = ref()

// ─── Pinned Extensions (Tab 2) ───────────────────────────────────────────────────
const PINNED_EXTENSIONS = ['70131', '79905', '79906']
interface ExtStat {
  extension: string
  ownerName: string
  totalCalls: number
  answered: number
  missed: number
  answerRate: number
  missRate: number
}

const pinnedExtStats = computed((): ExtStat[] => {
  return PINNED_EXTENSIONS.map((ext) => {
    const logsForExt = rawLogs.value.filter((l) => {
      if (l.extension !== ext) return false
      if (!l.reportMonth) return false
      const d = new Date(l.reportMonth)
      const startDate = selectedDateRange.value?.[0] || null
      let endDate = selectedDateRange.value?.[1] ? new Date(selectedDateRange.value[1]) : null
      if (endDate)
        endDate = new Date(endDate.getFullYear(), endDate.getMonth() + 1, 0, 23, 59, 59, 999)
      if (startDate && d < startDate) return false
      if (endDate && d > endDate) return false
      return true
    })
    const totalCalls = logsForExt.reduce((s, l) => s + (l.totalCalls || 0), 0)
    const answered = logsForExt.reduce(
      (s, l) => s + (l.answeredInbound || 0) + (l.answeredOutbound || 0),
      0,
    )
    const missed = logsForExt.reduce(
      (s, l) => s + (l.noAnswerInbound || 0) + (l.busyInbound || 0),
      0,
    )
    return {
      extension: ext,
      ownerName: directoryMap.value[ext]?.ownerName || ext,
      totalCalls,
      answered,
      missed,
      answerRate: totalCalls > 0 ? (answered / totalCalls) * 100 : 0,
      missRate: totalCalls > 0 ? (missed / totalCalls) * 100 : 0,
    }
  })
})

const pinnedTrendChartData = ref<object | undefined>(undefined)
const pinnedTrendChartOptions = ref<object | undefined>(undefined)
const pinnedRateChartData = ref<object | undefined>(undefined)
const pinnedRateChartOptions = ref<object | undefined>(undefined)
const pinnedRankChartData = ref<object | undefined>(undefined)
const pinnedRankChartOptions = ref<object | undefined>(undefined)

// ─── OSS (One Stop Service) Extensions (Tab 2) ───────────────────────────────
const OSS_EXTENSIONS = [
  '79901', '79902', '97627', '79909', '79910', '79911', '79912', '79913', '79914', '79915',
  '79916', '79917', '79918', '79919', '79920', '79921', '79922', '79923', '79924', '79925',
  '79926', '79927', '79928', '79929', '79930', '79931', '79932', '79933', '79934', '79935',
  '79936', '79937', '79938', '71801', '71807', '97430', '71803', '71804', '97630', '97628',
  '71802', '71819', '71805', '71806', '71821', '19998', '71822', '71812', '71813', '71815',
  '71816', '71817', '71818', '98042', '71820', '71808', '71809', '71810', '71811', '71814',
]

const ossExtStats = computed((): ExtStat[] => {
  const startDate = selectedDateRange.value?.[0] || null
  let endDate = selectedDateRange.value?.[1] ? new Date(selectedDateRange.value[1]) : null
  if (endDate) endDate = new Date(endDate.getFullYear(), endDate.getMonth() + 1, 0, 23, 59, 59, 999)

  return OSS_EXTENSIONS.map((ext) => {
    const logsForExt = rawLogs.value.filter((l) => {
      if (l.extension !== ext || !l.reportMonth) return false
      const d = new Date(l.reportMonth)
      if (startDate && d < startDate) return false
      if (endDate && d > endDate) return false
      return true
    })
    const totalCalls = logsForExt.reduce((s, l) => s + (l.totalCalls || 0), 0)
    const answered = logsForExt.reduce((s, l) => s + (l.answeredInbound || 0) + (l.answeredOutbound || 0), 0)
    const missed = logsForExt.reduce((s, l) => s + (l.noAnswerInbound || 0) + (l.busyInbound || 0), 0)
    return {
      extension: ext,
      ownerName: directoryMap.value[ext]?.ownerName || '-',
      totalCalls,
      answered,
      missed,
      answerRate: totalCalls > 0 ? (answered / totalCalls) * 100 : 0,
      missRate: totalCalls > 0 ? (missed / totalCalls) * 100 : 0,
    }
  })
})

const ossCombinedStats = computed(() => {
  const stats = ossExtStats.value
  const totalCalls = stats.reduce((s, e) => s + e.totalCalls, 0)
  const answered = stats.reduce((s, e) => s + e.answered, 0)
  const missed = stats.reduce((s, e) => s + e.missed, 0)
  return {
    totalCalls, answered, missed,
    answerRate: totalCalls > 0 ? (answered / totalCalls) * 100 : 0,
    missRate: totalCalls > 0 ? (missed / totalCalls) * 100 : 0,
  }
})

const ossTrendChartData = ref<object | undefined>(undefined)
const ossTrendChartOptions = ref<object | undefined>(undefined)
const ossRankChartData = ref<object | undefined>(undefined)
const ossRankChartOptions = ref<object | undefined>(undefined)
const ossRateChartData = ref<object | undefined>(undefined)
const ossRateChartOptions = ref<object | undefined>(undefined)

const buildOssCharts = () => {
  const startDate: Date | null = selectedDateRange.value?.[0] || null
  let endDate: Date | null = selectedDateRange.value?.[1] ? new Date(selectedDateRange.value[1]) : null
  if (endDate) endDate = new Date(endDate.getFullYear(), endDate.getMonth() + 1, 0, 23, 59, 59, 999)

  const monthlyTotals: Record<string, number> = {}
  rawLogs.value.forEach((log) => {
    if (!OSS_EXTENSIONS.includes(log.extension) || !log.reportMonth) return
    const d = new Date(log.reportMonth)
    if (startDate && d < startDate) return
    if (endDate && d > endDate) return
    const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
    monthlyTotals[key] = (monthlyTotals[key] || 0) + (log.totalCalls || 0)
  })

  const sortedKeys = Object.keys(monthlyTotals).sort()
  ossTrendChartData.value = {
    labels: sortedKeys.map((k) => formatChartLabel(k)),
    datasets: [{ type: 'bar', label: 'สายทั้งหมด OSS (ครั้ง)', backgroundColor: '#8b5cf6', data: sortedKeys.map((k) => monthlyTotals[k] || 0) }],
  }
  ossTrendChartOptions.value = {
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { y: { title: { display: true, text: 'จำนวนสาย (ครั้ง)' } } },
  }

  const sorted = [...ossExtStats.value].sort((a, b) => b.totalCalls - a.totalCalls).filter(s => s.totalCalls > 0).slice(0, 20)
  ossRankChartData.value = {
    labels: sorted.map((s) => s.extension),
    datasets: [{ label: 'สายทั้งหมด (ครั้ง)', data: sorted.map((s) => s.totalCalls), backgroundColor: '#8b5cf6' }],
  }
  ossRankChartOptions.value = {
    indexAxis: 'y',
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { x: { title: { display: true, text: 'จำนวนสาย' } } },
  }

  const topByTotal = [...ossExtStats.value].filter(s => s.totalCalls > 0).sort((a, b) => b.totalCalls - a.totalCalls).slice(0, 15)
  ossRateChartData.value = {
    labels: topByTotal.map((s) => s.extension),
    datasets: [
      { label: 'รับสาย (%)', data: topByTotal.map((s) => parseFloat(s.answerRate.toFixed(1))), backgroundColor: '#10b981' },
      { label: 'ไม่รับสาย(สายซ่อน) (%)', data: topByTotal.map((s) => parseFloat(s.missRate.toFixed(1))), backgroundColor: '#ef4444' },
    ],
  }
  ossRateChartOptions.value = {
    maintainAspectRatio: false,
    plugins: { legend: { position: 'top' } },
    scales: { y: { max: 100, title: { display: true, text: 'เปอร์เซ็นต์ (%)' } } },
  }
}

const pinnedCombinedStats = computed(() => {
  const stats = pinnedExtStats.value
  const totalCalls = stats.reduce((s, e) => s + e.totalCalls, 0)
  const answered = stats.reduce((s, e) => s + e.answered, 0)
  const missed = stats.reduce((s, e) => s + e.missed, 0)
  return {
    totalCalls,
    answered,
    missed,
    answerRate: totalCalls > 0 ? (answered / totalCalls) * 100 : 0,
    missRate: totalCalls > 0 ? (missed / totalCalls) * 100 : 0,
  }
})

const buildPinnedCharts = () => {
  const startDate: Date | null = selectedDateRange.value?.[0] || null
  let endDate: Date | null = selectedDateRange.value?.[1]
    ? new Date(selectedDateRange.value[1])
    : null
  if (endDate) endDate = new Date(endDate.getFullYear(), endDate.getMonth() + 1, 0, 23, 59, 59, 999)

  const monthlyByExt: Record<string, Record<string, number>> = {}
  PINNED_EXTENSIONS.forEach((ext) => {
    monthlyByExt[ext] = {}
  })

  rawLogs.value.forEach((log) => {
    if (!PINNED_EXTENSIONS.includes(log.extension) || !log.reportMonth) return
    const d = new Date(log.reportMonth)
    if (startDate && d < startDate) return
    if (endDate && d > endDate) return
    const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
    const extData = monthlyByExt[log.extension]
    if (extData) {
      if (!extData[key]) extData[key] = 0
      extData[key] += log.totalCalls || 0
    }
  })

  const allKeys = [
    ...new Set(PINNED_EXTENSIONS.flatMap((ext) => Object.keys(monthlyByExt[ext] || {}))),
  ].sort()
  const trendLabels = allKeys.map((k) => formatChartLabel(k))
  const COLORS = ['#0d9488', '#f59e0b', '#8b5cf6'] // teal, amber, purple

  pinnedTrendChartData.value = {
    labels: trendLabels,
    datasets: PINNED_EXTENSIONS.map((ext, i) => ({
      type: 'line',
      label: `${ext} (${directoryMap.value[ext]?.ownerName || ext})`,
      borderColor: COLORS[i],
      borderWidth: 2.5,
      tension: 0.4,
      data: allKeys.map((k) => (monthlyByExt[ext] as Record<string, number>)[k] || 0),
    })),
  }
  pinnedTrendChartOptions.value = {
    maintainAspectRatio: false,
    plugins: { legend: { position: 'top' } },
    scales: { y: { title: { display: true, text: 'จำนวนสาย (ครั้ง)' } } },
  }

  const stats = pinnedExtStats.value
  pinnedRateChartData.value = {
    labels: stats.map((s) => `${s.extension}\n${s.ownerName}`),
    datasets: [
      {
        label: 'รับสาย (%)',
        data: stats.map((s) => parseFloat(s.answerRate.toFixed(1))),
        backgroundColor: '#10b981',
      },
      {
        label: 'ไม่รับสาย(สายซ่อน) (%)',
        data: stats.map((s) => parseFloat(s.missRate.toFixed(1))),
        backgroundColor: '#ef4444',
      },
    ],
  }
  pinnedRateChartOptions.value = {
    maintainAspectRatio: false,
    plugins: { legend: { position: 'top' } },
    scales: { y: { max: 100, title: { display: true, text: 'เปอร์เซ็นต์ (%)' } } },
  }

  const sorted = [...stats].sort((a, b) => b.totalCalls - a.totalCalls)
  // เพิ่มชื่อหน่วยงานใน label
  pinnedRankChartData.value = {
    labels: sorted.map((s) => {
      const deptName = directoryMap.value[s.extension]?.departmentId
        ? deptList.value.find((d) => d.id === directoryMap.value[s.extension]?.departmentId)
            ?.name || 'ไม่พบหน่วยงาน'
        : 'ไม่พบหน่วยงาน'
      return `${s.extension} ${deptName}`
    }),
    datasets: [
      {
        label: 'สายทั้งหมด (ครั้ง)',
        data: sorted.map((s) => s.totalCalls),
        backgroundColor: COLORS.slice(0, sorted.length),
      },
    ],
  }
  pinnedRankChartOptions.value = {
    indexAxis: 'y',
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { x: { title: { display: true, text: 'จำนวนสาย' } } },
  }
}

// ─── 3. Fetch Data ────────────────────────────────────────────────────────────
const fetchData = async (): Promise<void> => {
  isLoading.value = true
  try {
    const startDate = selectedDateRange.value?.[0] || null
    const endDate = selectedDateRange.value?.[1] || null

    const params: Record<string, string> = {}
    if (startDate) params.fromDate = toUtcDateOnly(startDate)
    if (endDate) params.toDate = toUtcEndOfDay(endDate)

    const [logsRes, dirRes] = await Promise.all([
      api.get('/IPPhoneMonthStat/logs', { params }),
      api.get('/Directory', { params: { take: '2000' } }),
    ])

    const logs = Array.isArray(logsRes.data) ? logsRes.data : (logsRes.data.items ?? [])
    rawLogs.value = logs

    const dirItems = Array.isArray(dirRes.data) ? dirRes.data : (dirRes.data.items ?? [])
    const map: Record<string, DirectoryInfo> = {}
    for (const d of dirItems) {
      if (d.ipPhoneNumber)
        map[d.ipPhoneNumber] = {
          ownerName: d.ownerName ?? d.ipPhoneNumber,
          departmentId: d.departmentId ?? '',
        }
    }
    directoryMap.value = map

    processData()
    buildPinnedCharts()
    buildOssCharts()
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูลสถิติ IP-Phone ได้')
  } finally {
    isLoading.value = false
  }
}

const clearDateFilter = (): void => {
  selectedDateRange.value = null
}

onMounted(async () => {
  // โหลดหน่วยงานก่อน เพื่อให้ setupCharts() ใช้ข้อมูลได้ทันที
  try {
    const deptRes = await api.get('/Department')
    deptList.value = Array.isArray(deptRes.data) ? deptRes.data : (deptRes.data.items ?? [])
  } catch (e) {
    toast.fromError(e, 'โหลดรายชื่อหน่วยงานไม่สำเร็จ')
  }
  await fetchData()
})
watch(selectedDateRange, () => fetchData())
watch([filterExt, filterDept], () => processData())

// ─── 4. Data Processing ───────────────────────────────────────────────────────
const processData = (): void => {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  let tCalls = 0,
    tInbound = 0,
    tOutbound = 0,
    tAnswered = 0
  let tAnsweredIn = 0,
    tMissedIn = 0,
    tBusyIn = 0,
    tAnsweredOut = 0,
    tMissedOut = 0,
    tBusyOut = 0
  const monthlyData: Record<string, MonthlyData> = {},
    callerStatsIn: Record<string, number> = {},
    callerStatsOut: Record<string, number> = {}

  rawLogs.value.forEach((log) => {
    if (!log.extension || !/^\d+$/.test(log.extension.trim())) return
    if (filterExt.value.trim() && !log.extension.includes(filterExt.value.trim())) return
    if (filterDept.value) {
      const info = directoryMap.value[log.extension]
      if (!info || info.departmentId !== filterDept.value) return
    }

    const recordDateObj = new Date(log.reportMonth!)
    const sortKey = `${recordDateObj.getFullYear()}-${String(recordDateObj.getMonth() + 1).padStart(2, '0')}`
    if (!monthlyData[sortKey]) monthlyData[sortKey] = { inbound: 0, outbound: 0, total: 0 }

    const inbound = log.totalInbound || 0,
      outbound = log.totalOutbound || 0,
      calls = log.totalCalls || 0
    const ansIn = log.answeredInbound || 0,
      missIn = (log.noAnswerInbound || 0) + (log.busyInbound || 0),
      busyIn = (log.failedInbound || 0) + (log.voicemailInbound || 0)
    const ansOut = log.answeredOutbound || 0,
      missOut = (log.noAnswerOutbound || 0) + (log.busyOutbound || 0),
      busyOut = (log.failedOutbound || 0) + (log.voicemailOutbound || 0)

    tAnswered += ansIn + ansOut
    tAnsweredIn += ansIn
    tMissedIn += missIn
    tBusyIn += busyIn
    tAnsweredOut += ansOut
    tMissedOut += missOut
    tBusyOut += busyOut
    tInbound += inbound
    tOutbound += outbound
    tCalls += calls

    monthlyData[sortKey].inbound += inbound
    monthlyData[sortKey].outbound += outbound
    monthlyData[sortKey].total += calls
    callerStatsIn[log.extension] = (callerStatsIn[log.extension] ?? 0) + inbound
    callerStatsOut[log.extension] = (callerStatsOut[log.extension] ?? 0) + outbound
  })

  totalCallVolume.value = tCalls
  sumInbound.value = tInbound
  sumOutbound.value = tOutbound
  sumAnsweredInbound.value = tAnsweredIn
  sumMissedInbound.value = tMissedIn
  sumBusyInbound.value = tBusyIn
  sumAnsweredOutbound.value = tAnsweredOut
  sumMissedOutbound.value = tMissedOut
  sumBusyOutbound.value = tBusyOut

  setupCharts(monthlyData, callerStatsIn, callerStatsOut)
}

const formatChartLabel = (sortKey: string): string => {
  const [yearStr = '', monthStr = '1'] = sortKey.split('-')
  return `${thaiMonthShort[parseInt(monthStr, 10) - 1]} ${yearStr}`
}

// ─── 5. Setup Charts ──────────────────────────────────────────────────────────
const setupCharts = (
  monthlyData: Record<string, MonthlyData>,
  callerStatsIn: Record<string, number>,
  callerStatsOut: Record<string, number>,
): void => {
  const sortedKeys = Object.keys(monthlyData).sort()
  const labels = sortedKeys.map((k) => formatChartLabel(k))
  trendInChartData.value = {
    labels,
    datasets: [
      {
        type: 'bar',
        label: 'สายโทรเข้า (Inbound)',
        backgroundColor: '#06b6d4',
        data: sortedKeys.map((k) => monthlyData[k]?.inbound ?? 0),
      },
    ],
  }
  trendOutChartData.value = {
    labels,
    datasets: [
      {
        type: 'bar',
        label: 'สายโทรออก (Outbound)',
        backgroundColor: '#f97316',
        data: sortedKeys.map((k) => monthlyData[k]?.outbound ?? 0),
      },
    ],
  }
  trendChartOptions.value = {
    maintainAspectRatio: false,
    aspectRatio: 0.6,
    interaction: { mode: 'index', intersect: false },
    scales: {
      x: { grid: { display: false } },
      y: { display: true, title: { display: true, text: 'จำนวนสาย (ครั้ง)' } },
    },
  }
  ratioInChartData.value = {
    labels: ['รับสายสำเร็จ', 'ไม่รับสาย(สายซ่อน)', 'ยุ่ง/ล้มเหลว'],
    datasets: [
      {
        data: [sumAnsweredInbound.value, sumMissedInbound.value, sumBusyInbound.value],
        backgroundColor: ['#10b981', '#ef4444', '#f59e0b'],
        borderWidth: 0,
      },
    ],
  }
  ratioOutChartData.value = {
    labels: ['โทรสำเร็จ', 'ไม่มีผู้รับ', 'ยุ่ง/ล้มเหลว'],
    datasets: [
      {
        data: [sumAnsweredOutbound.value, sumMissedOutbound.value, sumBusyOutbound.value],
        backgroundColor: ['#10b981', '#ef4444', '#f59e0b'],
        borderWidth: 0,
      },
    ],
  }
  ratioChartOptions.value = { plugins: { legend: { position: 'bottom' } }, cutout: '65%' }
  const getExtLabel = (ext: string): string => {
    const info = directoryMap.value[ext]
    const deptName = info?.departmentId
      ? deptList.value.find((d) => d.id === info.departmentId)?.name || 'ไม่พบหน่วยงาน'
      : 'ไม่พบหน่วยงาน'
    return `${ext} ${deptName}`
  }
  const topIn = Object.entries(callerStatsIn)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 10)
  topCallerInChartData.value = {
    labels: topIn.map((item) => getExtLabel(item[0])),
    datasets: [
      {
        label: 'ปริมาณสายเข้า (ครั้ง)',
        data: topIn.map((item) => item[1]),
        backgroundColor: '#06b6d4',
      },
    ],
  }
  const topOut = Object.entries(callerStatsOut)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 10)
  topCallerOutChartData.value = {
    labels: topOut.map((item) => getExtLabel(item[0])),
    datasets: [
      {
        label: 'ปริมาณสายออก (ครั้ง)',
        data: topOut.map((item) => item[1]),
        backgroundColor: '#f97316',
      },
    ],
  }
  topCallerChartOptions.value = {
    indexAxis: 'y',
    maintainAspectRatio: false,
    aspectRatio: 0.8,
    plugins: { legend: { display: false } },
    scales: { y: { grid: { display: false } } },
  }
}
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10">
    <!-- Header -->
    <div class="mb-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
      <div>
        <h2 class="text-3xl font-bold text-gray-800">
          <i class="pi pi-chart-bar text-teal-600 mr-2"></i>ภาพรวมสถิติ IP-Phone
        </h2>
        <p class="text-gray-500 mt-1">วิเคราะห์ปริมาณสายเข้า/ออก, อัตราการรับสาย และเวลาสนทนารวม</p>
      </div>
      <Tag
        :value="`ระดับสิทธิ์: ${currentUserRole.toUpperCase()}`"
        :severity="currentUserRole === 'superadmin' ? 'danger' : 'info'"
        rounded
      />
    </div>

    <!-- Filter Bar -->
    <div
      class="bg-white rounded-xl shadow-sm border border-gray-100 px-4 py-3 mb-4 flex flex-wrap items-end gap-4"
    >
      <div class="flex flex-col gap-1">
        <label class="text-xs font-semibold text-gray-500">กรองตามช่วงเวลา</label>
        <div class="flex items-center gap-1">
          <DatePicker
            v-model="selectedDateRange"
            selectionMode="range"
            dateFormat="dd/mm/yy"
            placeholder="ด/ว/ป - ด/ว/ป"
            class="w-60"
            showIcon
          />
          <Button
            v-if="selectedDateRange"
            icon="pi pi-times"
            severity="secondary"
            text
            rounded
            @click="clearDateFilter"
          />
        </div>
      </div>
      <div class="w-px h-10 bg-gray-200 hidden sm:block"></div>
      <div class="flex flex-col gap-1">
        <label class="text-xs font-semibold text-gray-500">กรองตามเบอร์ (ในภาพรวม)</label>
        <IconField>
          <InputIcon class="pi pi-phone" />
          <InputText v-model="filterExt" placeholder="เช่น 70131" class="w-36" />
        </IconField>
      </div>
      <div class="w-px h-10 bg-gray-200 hidden sm:block"></div>
      <div class="flex flex-col gap-1">
        <label class="text-xs font-semibold text-gray-500">กรองตามหน่วยงาน (ในภาพรวม)</label>
        <Select
          v-model="filterDept"
          :options="[{ id: '', name: 'ทั้งหมด' }, ...deptList]"
          optionLabel="name"
          optionValue="id"
          placeholder="-- ทั้งหมด --"
          class="w-52"
        />
      </div>
    </div>

    <Tabs value="0" lazy>
      <TabList>
        <Tab value="0">ภาพรวมสถิติ IP-Phone</Tab>
        <Tab value="1">สถิติเบอร์ลูก 7000 กด 0</Tab>
        <Tab value="2"><i class="pi pi-headphones mr-1"></i>One Stop Service</Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="0">
          <div v-if="isLoading" class="text-center p-10">
            <i class="pi pi-spin pi-spinner text-4xl text-gray-400"></i>
          </div>
          <div v-else>
            <!-- Overall KPIs -->
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4 my-4">
              <Card class="border-t-4 border-cyan-500"
                ><template #content>
                  <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                    สายเข้า (Inbound)
                  </p>
                  <h3 class="text-2xl font-bold text-cyan-700">
                    {{ sumInbound.toLocaleString() }} <span class="text-sm font-normal">ครั้ง</span>
                  </h3>
                </template>
              </Card>
              <Card class="border-t-4 border-orange-500"
                ><template #content>
                  <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">
                    สายออก (Outbound)
                  </p>
                  <h3 class="text-2xl font-bold text-orange-600">
                    {{ sumOutbound.toLocaleString() }}
                    <span class="text-sm font-normal">ครั้ง</span>
                  </h3>
                </template>
              </Card>
            </div>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
              <Card class="bg-cyan-50/20"
                ><template #content>
                  <div class="flex items-center gap-3 mb-4">
                    <div
                      class="w-10 h-10 bg-cyan-100 rounded-full flex items-center justify-center text-cyan-600"
                    >
                      <i class="pi pi-arrow-down-left"></i>
                    </div>
                    <div>
                      <p class="text-xs text-cyan-700 font-semibold uppercase">รายละเอียดสายเข้า</p>
                      <h3 class="text-xl font-bold text-cyan-900">
                        {{ sumInbound.toLocaleString() }}
                        <span class="text-sm font-normal">สาย</span>
                      </h3>
                    </div>
                  </div>
                  <div class="grid grid-cols-3 gap-2 text-center border-t border-cyan-200/50 pt-4">
                    <div>
                      <p class="text-[10px] uppercase font-bold text-emerald-600">
                        <i class="pi pi-check-circle mr-1"></i>รับสาย
                      </p>
                      <p class="text-lg font-bold text-gray-800">
                        {{ sumAnsweredInbound.toLocaleString() }}
                      </p>
                    </div>
                    <div class="border-l border-cyan-200/50">
                      <p class="text-[10px] uppercase font-bold text-red-500">
                        <i class="pi pi-phone-off mr-1"></i>ไม่รับสาย(สายซ่อน)
                      </p>
                      <p class="text-lg font-bold text-gray-800">
                        {{ sumMissedInbound.toLocaleString() }}
                      </p>
                    </div>
                    <div class="border-l border-cyan-200/50">
                      <p class="text-[10px] uppercase font-bold text-amber-500">
                        <i class="pi pi-ban mr-1"></i>ยุ่ง/ล้มเหลว
                      </p>
                      <p class="text-lg font-bold text-gray-800">
                        {{ sumBusyInbound.toLocaleString() }}
                      </p>
                    </div>
                  </div>
                </template></Card
              >
              <Card class="bg-orange-50/20"
                ><template #content>
                  <div class="flex items-center gap-3 mb-4">
                    <div
                      class="w-10 h-10 bg-orange-100 rounded-full flex items-center justify-center text-orange-600"
                    >
                      <i class="pi pi-arrow-up-right"></i>
                    </div>
                    <div>
                      <p class="text-xs text-orange-700 font-semibold uppercase">
                        รายละเอียดสายออก
                      </p>
                      <h3 class="text-xl font-bold text-orange-900">
                        {{ sumOutbound.toLocaleString() }}
                        <span class="text-sm font-normal">สาย</span>
                      </h3>
                    </div>
                  </div>
                  <div
                    class="grid grid-cols-3 gap-2 text-center border-t border-orange-200/50 pt-4"
                  >
                    <div>
                      <p class="text-[10px] uppercase font-bold text-emerald-600">
                        <i class="pi pi-check-circle mr-1"></i>โทรสำเร็จ
                      </p>
                      <p class="text-lg font-bold text-gray-800">
                        {{ sumAnsweredOutbound.toLocaleString() }}
                      </p>
                    </div>
                    <div class="border-l border-orange-200/50">
                      <p class="text-[10px] uppercase font-bold text-red-500">
                        <i class="pi pi-phone-off mr-1"></i>ไม่มีผู้รับ
                      </p>
                      <p class="text-lg font-bold text-gray-800">
                        {{ sumMissedOutbound.toLocaleString() }}
                      </p>
                    </div>
                    <div class="border-l border-orange-200/50">
                      <p class="text-[10px] uppercase font-bold text-amber-500">
                        <i class="pi pi-ban mr-1"></i>ยุ่ง/ล้มเหลว
                      </p>
                      <p class="text-lg font-bold text-gray-800">
                        {{ sumBusyOutbound.toLocaleString() }}
                      </p>
                    </div>
                  </div>
                </template></Card
              >
            </div>
            <!-- Charts -->
            <div class="mb-4 mt-8">
              <h3 class="text-xl font-bold text-cyan-700">
                <i class="pi pi-arrow-down-left mr-2 bg-cyan-100 p-2 rounded-full"></i
                >กราฟสถิติสายเข้า (Inbound)
              </h3>
            </div>
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
              <Card class="lg:col-span-2"
                ><template #title> <div class="text-lg">แนวโน้มสายโทรเข้ารายเดือน</div> </template
                ><template #content>
                  <div class="h-80 relative">
                    <Chart
                      type="bar"
                      :data="trendInChartData"
                      :options="trendChartOptions"
                      class="h-full"
                    />
                  </div>
                </template>
              </Card>
              <Card
                ><template #title> <div class="text-lg">อัตราการรับสาย</div> </template
                ><template #content>
                  <div class="h-80 relative flex items-center justify-center">
                    <div
                      class="absolute inset-0 flex flex-col items-center justify-center pointer-events-none mt-2"
                    >
                      <span class="text-2xl font-bold text-green-600"
                        >{{
                          sumInbound > 0
                            ? ((sumAnsweredInbound / sumInbound) * 100).toFixed(1)
                            : '0.0'
                        }}%</span
                      ><span class="text-[10px] text-gray-400 uppercase mt-0.5">รับสายสำเร็จ</span>
                    </div>
                    <Chart
                      type="doughnut"
                      :data="ratioInChartData"
                      :options="ratioChartOptions"
                      class="w-full max-w-xs"
                    />
                  </div>
                </template>
              </Card>
            </div>
            <Card
              ><template #title>
                <div class="text-lg">เบอร์ที่มีคนโทรเข้าสูงสุด (Top 10)</div> </template
              ><template #content>
                <div class="h-64 relative">
                  <Chart
                    type="bar"
                    :data="topCallerInChartData"
                    :options="topCallerChartOptions"
                    class="h-full"
                  />
                </div>
              </template>
            </Card>

            <div class="mb-4 mt-12">
              <h3 class="text-xl font-bold text-orange-700">
                <i class="pi pi-arrow-up-right mr-2 bg-orange-100 p-2 rounded-full"></i
                >กราฟสถิติสายออก (Outbound)
              </h3>
            </div>
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
              <Card class="lg:col-span-2"
                ><template #title> <div class="text-lg">แนวโน้มสายโทรออกรายเดือน</div> </template
                ><template #content>
                  <div class="h-80 relative">
                    <Chart
                      type="bar"
                      :data="trendOutChartData"
                      :options="trendChartOptions"
                      class="h-full"
                    />
                  </div>
                </template>
              </Card>
              <Card
                ><template #title> <div class="text-lg">อัตราการโทรสำเร็จ</div> </template
                ><template #content>
                  <div class="h-80 relative flex items-center justify-center">
                    <div
                      class="absolute inset-0 flex flex-col items-center justify-center pointer-events-none mt-2"
                    >
                      <span class="text-2xl font-bold text-green-600"
                        >{{
                          sumOutbound > 0
                            ? ((sumAnsweredOutbound / sumOutbound) * 100).toFixed(1)
                            : '0.0'
                        }}%</span
                      ><span class="text-[10px] text-gray-400 uppercase mt-0.5">โทรสำเร็จ</span>
                    </div>
                    <Chart
                      type="doughnut"
                      :data="ratioOutChartData"
                      :options="ratioChartOptions"
                      class="w-full max-w-xs"
                    />
                  </div>
                </template>
              </Card>
            </div>
            <Card
              ><template #title>
                <div class="text-lg">เจ้าหน้าที่ที่โทรออกสูงสุด (Top 10)</div> </template
              ><template #content>
                <div class="h-64 relative">
                  <Chart
                    type="bar"
                    :data="topCallerOutChartData"
                    :options="topCallerChartOptions"
                    class="h-full"
                  />
                </div>
              </template>
            </Card>
          </div>
        </TabPanel>
        <TabPanel value="1">
          <div v-if="isLoading" class="text-center p-10">
            <i class="pi pi-spin pi-spinner text-4xl text-gray-400"></i>
          </div>
          <div v-else>
            <div
              class="my-4 bg-linear-to-r from-teal-600 to-cyan-500 rounded-2xl p-5 text-white shadow-md"
            >
              <p class="text-teal-100 text-xs font-semibold uppercase tracking-wider mb-3">
                <i class="pi pi-objects-column mr-1"></i>สถิติภาพรวมรวม 3 เบอร์
              </p>
              <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div class="bg-white/15 rounded-xl p-3 text-center">
                  <p class="text-teal-100 text-[10px] uppercase font-semibold">สายทั้งหมด</p>
                  <p class="text-3xl font-black mt-1">
                    {{ pinnedCombinedStats.totalCalls.toLocaleString() }}
                  </p>
                </div>
                <div class="bg-white/15 rounded-xl p-3 text-center">
                  <p class="text-teal-100 text-[10px] uppercase font-semibold">รับสาย</p>
                  <p class="text-3xl font-black mt-1 text-green-200">
                    {{ pinnedCombinedStats.answered.toLocaleString() }}
                  </p>
                  <p class="text-xs text-green-200 font-bold">
                    {{ pinnedCombinedStats.answerRate.toFixed(1) }}%
                  </p>
                </div>
                <div class="bg-white/15 rounded-xl p-3 text-center">
                  <p class="text-teal-100 text-[10px] uppercase font-semibold">
                    ไม่รับสาย(สายซ่อน)
                  </p>
                  <p class="text-3xl font-black mt-1 text-red-200">
                    {{ pinnedCombinedStats.missed.toLocaleString() }}
                  </p>
                  <p class="text-xs text-red-200 font-bold">
                    {{ pinnedCombinedStats.missRate.toFixed(1) }}%
                  </p>
                </div>
                <div class="bg-white/15 rounded-xl p-3 text-center">
                  <p class="text-teal-100 text-[10px] uppercase font-semibold">อัตรารับสาย</p>
                  <div class="mt-2 bg-white/20 rounded-full h-2">
                    <div
                      class="bg-green-300 h-2 rounded-full"
                      :style="{ width: pinnedCombinedStats.answerRate.toFixed(0) + '%' }"
                    ></div>
                  </div>
                  <p class="text-xl font-black mt-1 text-green-200">
                    {{ pinnedCombinedStats.answerRate.toFixed(1) }}%
                  </p>
                </div>
              </div>
            </div>
            <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
              <div
                v-for="stat in pinnedExtStats"
                :key="stat.extension"
                class="bg-white rounded-2xl shadow-sm border border-gray-100 hover:shadow-md transition-shadow"
              >
                <div
                  class="bg-linear-to-r from-teal-600 to-cyan-500 px-5 py-4 flex items-center justify-between"
                >
                  <h2 class="text-white text-3xl font-black tracking-widest">
                    {{ stat.extension }}
                  </h2>
                  <div class="w-12 h-12 bg-white/20 rounded-full flex items-center justify-center">
                    <i class="pi pi-phone text-white text-xl"></i>
                  </div>
                </div>
                <div class="px-5 py-3 bg-teal-50 border-b border-teal-100">
                  <span class="text-xs text-teal-700 font-semibold">ผู้รับผิดชอบ: </span
                  ><span class="text-sm font-bold text-teal-900">{{ stat.ownerName }}</span>
                </div>
                <div class="grid grid-cols-2 divide-x divide-y divide-gray-100">
                  <div class="col-span-2 px-5 py-4 flex items-center gap-4 bg-gray-50">
                    <div
                      class="w-10 h-10 bg-teal-100 rounded-full flex items-center justify-center shrink-0"
                    >
                      <i class="pi pi-list text-teal-600"></i>
                    </div>
                    <div>
                      <p class="text-xs text-gray-500 font-semibold uppercase">จำนวนสายทั้งหมด</p>
                      <p class="text-2xl font-black text-gray-900">
                        {{ stat.totalCalls.toLocaleString() }}
                      </p>
                    </div>
                  </div>
                  <div class="px-4 py-4">
                    <p class="text-xs text-green-600 font-semibold uppercase mb-1">รับสาย</p>
                    <p class="text-xl font-bold text-gray-800">
                      {{ stat.answered.toLocaleString() }}
                    </p>
                    <div class="mt-2 bg-gray-100 rounded-full h-1.5">
                      <div
                        class="bg-green-500 h-1.5 rounded-full"
                        :style="{ width: stat.answerRate.toFixed(0) + '%' }"
                      ></div>
                    </div>
                    <p class="text-xs text-green-600 font-bold mt-1">
                      {{ stat.answerRate.toFixed(1) }}%
                    </p>
                  </div>
                  <div class="px-4 py-4">
                    <p class="text-xs text-red-500 font-semibold uppercase mb-1">
                      ไม่รับสาย(สายซ่อน)
                    </p>
                    <p class="text-xl font-bold text-gray-800">
                      {{ stat.missed.toLocaleString() }}
                    </p>
                    <div class="mt-2 bg-gray-100 rounded-full h-1.5">
                      <div
                        class="bg-red-400 h-1.5 rounded-full"
                        :style="{ width: stat.missRate.toFixed(0) + '%' }"
                      ></div>
                    </div>
                    <p class="text-xs text-red-500 font-bold mt-1">
                      {{ stat.missRate.toFixed(1) }}%
                    </p>
                  </div>
                </div>
              </div>
            </div>
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mt-8">
              <Card class="lg:col-span-2"
                ><template #title>
                  <div class="text-base font-bold text-gray-700">
                    <i class="pi pi-chart-line text-teal-500 mr-2"></i>แนวโน้มการใช้งานรายเดือน
                    (เปรียบเทียบ)
                  </div> </template
                ><template #content>
                  <div class="h-64 relative">
                    <Chart
                      type="line"
                      :data="pinnedTrendChartData"
                      :options="pinnedTrendChartOptions"
                      class="h-full"
                    />
                  </div>
                </template>
              </Card>
              <Card
                ><template #title>
                  <div class="text-base font-bold text-gray-700">
                    <i class="pi pi-percentage text-green-500 mr-2"></i>เปรียบเทียบอัตรารับสาย
                  </div> </template
                ><template #content>
                  <div class="h-56 relative">
                    <Chart
                      type="bar"
                      :data="pinnedRateChartData"
                      :options="pinnedRateChartOptions"
                      class="h-full"
                    />
                  </div>
                </template>
              </Card>
              <Card
                ><template #title>
                  <div class="text-base font-bold text-gray-700">
                    <i class="pi pi-sort-amount-down text-teal-500 mr-2"></i>เปรียบเทียบปริมาณการโทร
                  </div> </template
                ><template #content>
                  <div class="h-56 relative">
                    <Chart
                      type="bar"
                      :data="pinnedRankChartData"
                      :options="pinnedRankChartOptions"
                      class="h-full"
                    />
                  </div>
                </template>
              </Card>
            </div>
          </div>
        </TabPanel>
        <!-- Tab 2: One Stop Service -->
        <TabPanel value="2">
          <div v-if="isLoading" class="text-center p-10">
            <i class="pi pi-spin pi-spinner text-4xl text-gray-400"></i>
          </div>
          <div v-else>
            <!-- Summary banner -->
            <div class="my-4 bg-linear-to-r from-violet-600 to-purple-500 rounded-2xl p-5 text-white shadow-md">
              <p class="text-violet-100 text-xs font-semibold uppercase tracking-wider mb-3">
                <i class="pi pi-headphones mr-1"></i>สถิติภาพรวม One Stop Service ({{ OSS_EXTENSIONS.length }} เบอร์)
              </p>
              <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div class="bg-white/15 rounded-xl p-3 text-center">
                  <p class="text-violet-100 text-[10px] uppercase font-semibold">สายทั้งหมด</p>
                  <p class="text-3xl font-black mt-1">{{ ossCombinedStats.totalCalls.toLocaleString() }}</p>
                </div>
                <div class="bg-white/15 rounded-xl p-3 text-center">
                  <p class="text-violet-100 text-[10px] uppercase font-semibold">รับสาย</p>
                  <p class="text-3xl font-black mt-1 text-green-200">{{ ossCombinedStats.answered.toLocaleString() }}</p>
                  <p class="text-xs text-green-200 font-bold">{{ ossCombinedStats.answerRate.toFixed(1) }}%</p>
                </div>
                <div class="bg-white/15 rounded-xl p-3 text-center">
                  <p class="text-violet-100 text-[10px] uppercase font-semibold">ไม่รับสาย(สายซ่อน)</p>
                  <p class="text-3xl font-black mt-1 text-red-200">{{ ossCombinedStats.missed.toLocaleString() }}</p>
                  <p class="text-xs text-red-200 font-bold">{{ ossCombinedStats.missRate.toFixed(1) }}%</p>
                </div>
                <div class="bg-white/15 rounded-xl p-3 text-center">
                  <p class="text-violet-100 text-[10px] uppercase font-semibold">อัตรารับสาย</p>
                  <div class="mt-2 bg-white/20 rounded-full h-2">
                    <div class="bg-green-300 h-2 rounded-full" :style="{ width: ossCombinedStats.answerRate.toFixed(0) + '%' }"></div>
                  </div>
                  <p class="text-xl font-black mt-1 text-green-200">{{ ossCombinedStats.answerRate.toFixed(1) }}%</p>
                </div>
              </div>
            </div>

            <!-- Charts row -->
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
              <Card>
                <template #title><div class="text-base font-bold text-gray-700"><i class="pi pi-chart-bar text-violet-500 mr-2"></i>แนวโน้มสายทั้งหมดรายเดือน</div></template>
                <template #content>
                  <div class="h-64 relative">
                    <Chart type="bar" :data="ossTrendChartData" :options="ossTrendChartOptions" class="h-full" />
                  </div>
                </template>
              </Card>
              <Card>
                <template #title><div class="text-base font-bold text-gray-700"><i class="pi pi-sort-amount-down text-violet-500 mr-2"></i>Top 20 เบอร์ที่มีสายสูงสุด</div></template>
                <template #content>
                  <div class="h-64 relative">
                    <Chart type="bar" :data="ossRankChartData" :options="ossRankChartOptions" class="h-full" />
                  </div>
                </template>
              </Card>
            </div>
            <Card class="mb-6">
              <template #title><div class="text-base font-bold text-gray-700"><i class="pi pi-percentage text-green-500 mr-2"></i>อัตรารับสาย/ไม่รับสาย (Top 15 เบอร์)</div></template>
              <template #content>
                <div class="h-64 relative">
                  <Chart type="bar" :data="ossRateChartData" :options="ossRateChartOptions" class="h-full" />
                </div>
              </template>
            </Card>

            <!-- DataTable: all OSS extensions -->
            <Card>
              <template #title><div class="text-base font-bold text-gray-700"><i class="pi pi-table text-violet-500 mr-2"></i>รายละเอียดทุกเบอร์ One Stop Service</div></template>
              <template #content>
                <DataTable
                  :value="ossExtStats"
                  :rows="20"
                  paginator
                  stripedRows
                  sortField="totalCalls"
                  :sortOrder="-1"
                  responsiveLayout="scroll"
                  class="text-sm"
                >
                  <Column field="extension" header="เบอร์" sortable style="width: 100px">
                    <template #body="{ data }">
                      <span class="font-bold text-violet-600 tracking-wider">{{ data.extension }}</span>
                    </template>
                  </Column>
                  <Column field="ownerName" header="ผู้รับผิดชอบ" sortable style="min-width: 160px">
                    <template #body="{ data }">
                      <span class="text-gray-700">{{ data.ownerName }}</span>
                    </template>
                  </Column>
                  <Column field="totalCalls" header="สายทั้งหมด" sortable style="width: 120px">
                    <template #body="{ data }">
                      <span class="font-bold text-gray-800">{{ data.totalCalls.toLocaleString() }}</span>
                    </template>
                  </Column>
                  <Column field="answered" header="รับสาย" sortable style="width: 100px">
                    <template #body="{ data }">
                      <span class="text-green-600 font-semibold">{{ data.answered.toLocaleString() }}</span>
                    </template>
                  </Column>
                  <Column field="missed" header="ไม่รับสาย(สายซ่อน)" sortable style="width: 120px">
                    <template #body="{ data }">
                      <span class="text-red-500 font-semibold">{{ data.missed.toLocaleString() }}</span>
                    </template>
                  </Column>
                  <Column field="answerRate" header="อัตรารับสาย" sortable style="width: 140px">
                    <template #body="{ data }">
                      <div v-if="data.totalCalls > 0" class="flex items-center gap-2">
                        <div class="flex-1 bg-gray-100 rounded-full h-1.5">
                          <div class="bg-green-500 h-1.5 rounded-full" :style="{ width: data.answerRate.toFixed(0) + '%' }"></div>
                        </div>
                        <span class="text-xs font-bold text-green-600 w-10 text-right">{{ data.answerRate.toFixed(1) }}%</span>
                      </div>
                      <span v-else class="text-gray-400 text-xs">ไม่มีข้อมูล</span>
                    </template>
                  </Column>
                </DataTable>
              </template>
            </Card>
          </div>
        </TabPanel>
      </TabPanels>
    </Tabs>
  </div>
</template>

<style scoped>
:deep(.p-datepicker) {
  border: none;
}

:deep(.p-tablist-tab) {
  padding: 1rem 1.5rem;
}
</style>
