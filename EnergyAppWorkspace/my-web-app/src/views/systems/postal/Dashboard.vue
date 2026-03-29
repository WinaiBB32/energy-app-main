<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useAppToast } from '@/composables/useAppToast'
import { toMonthKey } from '@/utils/monthlySummary'
import { collection, query, where, getDocs, type QueryConstraint, type QueryDocumentSnapshot } from 'firebase/firestore'
import { db } from '@/firebase/config'

import Card from 'primevue/card'
import Chart from 'primevue/chart'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'

defineOptions({ name: 'PostalDashboard' })
const toast = useAppToast()

// ─── Interfaces ─────────────────────────────────────────────────────────────
interface MonthlySummary {
    postal?: {
        normalMail?: number
        registeredMail?: number
        emsMail?: number
        totalAmount?: number
        count?: number
    }
}

interface MonthlyData {
    normal: number
    registered: number
    ems: number
    total: number
}

// ─── Auth & State ───────────────────────────────────────────────────────────

const monthlySummaries = ref<Record<string, MonthlySummary>>({})
const isLoading = ref<boolean>(true)

// KPIs
const sumNormal = ref<number>(0)
const sumRegistered = ref<number>(0)
const sumEms = ref<number>(0)
const sumTotal = ref<number>(0)

// Filters (Default: ย้อนหลัง 6 เดือน)
const getDefaultDateRange = (): Date[] => {
    const now = new Date()
    const first = new Date(now.getFullYear(), now.getMonth() - 5, 1) // ย้อนหลัง 5 เดือน + เดือนนี้
    const last = new Date(now.getFullYear(), now.getMonth() + 1, 0)
    return [first, last]
}
const selectedDateRange = ref<Date[] | null>(getDefaultDateRange())

const thaiMonthShort = ['ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.', 'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.']
const dateRangeLabel = computed(() => {
    const r = selectedDateRange.value
    if (!r || r.length < 2 || !r[0] || !r[1]) return 'ทุกช่วงเวลา'
    const fmt = (d: Date) => `${thaiMonthShort[d.getMonth()]} ${d.getFullYear() + 543}`
    const s = fmt(r[0]), e = fmt(r[1])
    return s === e ? s : `${s} – ${e}`
})

// Charts State
const trendChartData = ref()
const trendChartOptions = ref()
const proportionChartData = ref()
const proportionChartOptions = ref()

// ─── ดึงข้อมูล (One-time Fetch) ─────────────────────────────────────────────
const fetchData = async (): Promise<void> => {
    isLoading.value = true
    try {
        const startDate = selectedDateRange.value?.[0] || null
        const endDate = selectedDateRange.value?.[1] ? new Date(selectedDateRange.value[1]) : null
        if (endDate) endDate.setHours(23, 59, 59, 999)

        const startMonthKey = startDate ? toMonthKey(startDate) : null
        const endMonthKey = endDate ? toMonthKey(endDate) : null

        // 1. ดึงข้อมูล Summary (Aggregation) - ประหยัด Read มหาศาล
        const summaryRef = collection(db, 'monthly_summaries')
        const summaryConstraints: QueryConstraint[] = []
        if (startMonthKey) summaryConstraints.push(where('__name__', '>=', startMonthKey))
        if (endMonthKey) summaryConstraints.push(where('__name__', '<=', endMonthKey))

        const summarySnap = await getDocs(query(summaryRef, ...summaryConstraints))
        monthlySummaries.value = Object.fromEntries(
            summarySnap.docs.map((doc: QueryDocumentSnapshot) => [doc.id, doc.data() as MonthlySummary])
        )

        processData()
    } catch (error: unknown) {
        toast.fromError(error, 'ไม่สามารถโหลดข้อมูล Dashboard ไปรษณีย์ได้')
    } finally {
        isLoading.value = false
    }
}

onMounted(() => fetchData())
watch(selectedDateRange, () => fetchData())
const clearDateFilter = () => { selectedDateRange.value = getDefaultDateRange() }

// ─── ประมวลผลข้อมูล ─────────────────────────────────────────────────────────
const processData = (): void => {
    let tNormal = 0, tRegistered = 0, tEms = 0, tTotal = 0
    const monthlyTrend: Record<string, MonthlyData> = {}

    // ประมวลผลจาก Summary
    Object.entries(monthlySummaries.value).forEach(([monthKey, summary]) => {
        if (summary.postal) {
            const n = summary.postal.normalMail || 0
            const r = summary.postal.registeredMail || 0
            const e = summary.postal.emsMail || 0
            const t = summary.postal.totalAmount || 0

            tNormal += n
            tRegistered += r
            tEms += e
            tTotal += t

            monthlyTrend[monthKey] = { normal: n, registered: r, ems: e, total: t }
        }
    })

    sumNormal.value = tNormal
    sumRegistered.value = tRegistered
    sumEms.value = tEms
    sumTotal.value = tTotal

    setupCharts(monthlyTrend)
}

const formatChartLabel = (sortKey: string): string => {
    const parts = sortKey.split('-')
    const yearStr = parts[0] || ''
    const monthStr = parts[1] || '1'
    return `${thaiMonthShort[parseInt(monthStr, 10) - 1]} ${yearStr}`
}

// ─── ตั้งค่า Chart.js ────────────────────────────────────────────────────────
const setupCharts = (monthlyData: Record<string, MonthlyData>): void => {
    const sortedKeys = Object.keys(monthlyData).sort()
    const labels = sortedKeys.map(k => formatChartLabel(k))

    // 1. กราฟแนวโน้ม (Stacked Bar)
    trendChartData.value = {
        labels,
        datasets: [
            { type: 'bar', label: 'ไปรษณีย์ธรรมดา', backgroundColor: '#94a3b8', data: sortedKeys.map(k => monthlyData[k]?.normal ?? 0) },
            { type: 'bar', label: 'ลงทะเบียน', backgroundColor: '#3b82f6', data: sortedKeys.map(k => monthlyData[k]?.registered ?? 0) },
            { type: 'bar', label: 'EMS', backgroundColor: '#f43f5e', borderRadius: { topLeft: 4, topRight: 4 }, data: sortedKeys.map(k => monthlyData[k]?.ems ?? 0) },
        ]
    }
    trendChartOptions.value = {
        maintainAspectRatio: false, aspectRatio: 0.6,
        interaction: { mode: 'index', intersect: false },
        scales: { x: { stacked: true, grid: { display: false } }, y: { stacked: true, title: { display: true, text: 'จำนวน (ชิ้น)' } } }
    }

    // 2. กราฟสัดส่วน (Doughnut)
    proportionChartData.value = {
        labels: ['ธรรมดา', 'ลงทะเบียน', 'EMS'],
        datasets: [{
            data: [sumNormal.value, sumRegistered.value, sumEms.value],
            backgroundColor: ['#94a3b8', '#3b82f6', '#f43f5e'],
            borderWidth: 0
        }]
    }
    proportionChartOptions.value = { plugins: { legend: { position: 'bottom' } }, cutout: '60%' }
}
</script>

<template>
    <div class="max-w-7xl mx-auto pb-10">
        <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
            <div>
                <h2 class="text-3xl font-bold text-gray-800">
                    <i class="pi pi-chart-bar text-blue-500 mr-2"></i>ภาพรวมงานไปรษณีย์
                </h2>
                <p class="text-gray-500 mt-1">วิเคราะห์ปริมาณและประเภทการจัดส่งจดหมาย/พัสดุ</p>
            </div>
            <div class="flex items-center gap-3">
                <div class="bg-white p-3 rounded-lg shadow-sm border border-gray-100 flex items-center gap-3">
                    <div class="flex flex-col">
                        <label class="text-xs font-semibold text-gray-500 mb-1">กรองตามช่วงเวลา</label>
                        <DatePicker v-model="selectedDateRange" selectionMode="range" dateFormat="dd/mm/yy"
                            placeholder="ด/ว/ป - ด/ว/ป" class="w-64" :manualInput="false" showIcon />
                    </div>
                    <Button v-if="selectedDateRange && selectedDateRange.length > 0" icon="pi pi-times"
                        severity="secondary" text rounded @click="clearDateFilter" class="mt-4" />
                </div>
            </div>
        </div>

        <div class="flex items-center gap-2 mb-4">
            <i class="pi pi-calendar-clock text-blue-400 text-sm"></i>
            <span class="text-sm text-gray-500">กำลังแสดงข้อมูล: </span>
            <span class="text-sm font-bold text-blue-600 bg-blue-50 px-3 py-0.5 rounded-full border border-blue-200">{{
                dateRangeLabel }}</span>
        </div>

        <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
            <Card class="shadow-sm border-t-4 border-slate-400">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ธรรมดา</p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ sumNormal.toLocaleString() }} <span
                                    class="text-sm font-normal">ชิ้น</span></h3>
                        </div>
                        <div class="w-10 h-10 bg-slate-50 rounded-full flex items-center justify-center text-slate-500">
                            <i class="pi pi-file"></i>
                        </div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-blue-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ลงทะเบียน</p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ sumRegistered.toLocaleString() }} <span
                                    class="text-sm font-normal">ชิ้น</span></h3>
                        </div>
                        <div class="w-10 h-10 bg-blue-50 rounded-full flex items-center justify-center text-blue-500"><i
                                class="pi pi-book"></i></div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-rose-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">EMS (ด่วนพิเศษ)</p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ sumEms.toLocaleString() }} <span
                                    class="text-sm font-normal">ชิ้น</span></h3>
                        </div>
                        <div class="w-10 h-10 bg-rose-50 rounded-full flex items-center justify-center text-rose-500"><i
                                class="pi pi-send"></i></div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-sky-500 bg-sky-50/30">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-sky-700 font-black mb-1 uppercase">รวมการจัดส่งทั้งหมด</p>
                            <h3 class="text-3xl font-black text-sky-700">{{ sumTotal.toLocaleString() }} <span
                                    class="text-sm font-bold">ชิ้น</span></h3>
                        </div>
                        <div class="w-10 h-10 bg-sky-100 rounded-full flex items-center justify-center text-sky-600"><i
                                class="pi pi-box"></i></div>
                    </div>
                </template>
            </Card>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
            <Card class="shadow-sm border-none lg:col-span-2">
                <template #title>
                    <div class="text-lg font-bold text-gray-700">แนวโน้มปริมาณการส่งไปรษณีย์</div>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-80 flex items-center justify-center"><i
                            class="pi pi-spin pi-spinner text-4xl text-blue-500"></i></div>
                    <div v-else-if="trendChartData?.labels?.length === 0"
                        class="h-80 flex flex-col items-center justify-center text-gray-400"><i
                            class="pi pi-box text-3xl mb-2"></i>
                        <p>ไม่มีข้อมูล</p>
                    </div>
                    <div v-else class="h-80 relative">
                        <Chart type="bar" :data="trendChartData" :options="trendChartOptions" class="h-full w-full" />
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-none lg:col-span-1">
                <template #title>
                    <div class="text-lg font-bold text-gray-700">สัดส่วนประเภทการส่ง</div>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-80 flex items-center justify-center"><i
                            class="pi pi-spin pi-spinner text-4xl text-blue-500"></i></div>
                    <div v-else-if="sumTotal === 0"
                        class="h-80 flex flex-col items-center justify-center text-gray-400"><i
                            class="pi pi-chart-pie text-3xl mb-2"></i>
                        <p>ไม่มีข้อมูล</p>
                    </div>
                    <div v-else class="h-80 relative flex items-center justify-center">
                        <Chart type="doughnut" :data="proportionChartData" :options="proportionChartOptions"
                            class="w-full max-w-xs" />
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