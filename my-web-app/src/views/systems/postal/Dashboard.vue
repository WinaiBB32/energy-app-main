<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import api from '@/services/api'
import { useAppToast } from '@/composables/useAppToast'

import Card from 'primevue/card'
import Chart from 'primevue/chart'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'

defineOptions({ name: 'PostalDashboard' })
const toast = useAppToast()

// ─── Interfaces ─────────────────────────────────────────────────────────────
interface PostalRecord {
  id: string;
  recordMonth: string;
    incomingNormalMail: number;
    incomingRegisteredMail: number;
    incomingEmsMail: number;
    incomingTotalMail: number;
  normalMail: number;
    normalMailUnitPrice: number;
  registeredMail: number;
    registeredMailUnitPrice: number;
  emsMail: number;
    emsMailUnitPrice: number;
  totalMail: number;
}

interface MonthlyData {
    outgoingNormal: number
    outgoingRegistered: number
    outgoingEms: number
    outgoingTotal: number
    incomingTotal: number
}

// ─── State ──────────────────────────────────────────────────────────────────
const rawRecords = ref<PostalRecord[]>([])
const isLoading = ref<boolean>(true)

const sumNormal = ref<number>(0)
const sumRegistered = ref<number>(0)
const sumEms = ref<number>(0)
const sumTotal = ref<number>(0)
const sumIncomingTotal = ref<number>(0)
const sumNormalCost = ref<number>(0)
const sumRegisteredCost = ref<number>(0)
const sumEmsCost = ref<number>(0)
const sumTotalCost = ref<number>(0)

const getDefaultDateRange = (): Date[] => {
    const now = new Date()
    const first = new Date(now.getFullYear(), now.getMonth() - 5, 1)
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

const outgoingTrendChartData = ref()
const incomingTrendChartData = ref()
const trendChartOptions = ref()
const outgoingProportionChartData = ref()
const proportionChartOptions = ref()

// ─── Data Fetching & Processing ─────────────────────────────────────────────
const fetchData = async (): Promise<void> => {
    isLoading.value = true
    try {
        const { data } = await api.get('/PostalRecord?take=10000')
        rawRecords.value = data.items || []
        processData()
    } catch (error: unknown) {
        toast.fromError(error, 'ไม่สามารถโหลดข้อมูล Dashboard ไปรษณีย์ได้')
    } finally {
        isLoading.value = false
    }
}

const processData = (): void => {
    let tNormal = 0, tRegistered = 0, tEms = 0
    let tIncomingTotal = 0
    let tNormalCost = 0, tRegisteredCost = 0, tEmsCost = 0
    const monthlyTrend: Record<string, MonthlyData> = {}

    const start = selectedDateRange.value?.[0]
    const end = selectedDateRange.value?.[1]
    const endOfDay = end ? new Date(end.getFullYear(), end.getMonth(), end.getDate(), 23, 59, 59, 999) : null

    rawRecords.value.forEach(record => {
        if (!record.recordMonth) return;
        const utcStr = /Z|[+-]\d{2}:\d{2}$/.test(record.recordMonth) ? record.recordMonth : record.recordMonth + 'Z'
        const recordDate = new Date(utcStr)
        if (start && endOfDay && (recordDate < start || recordDate > endOfDay)) return

        const monthKey = `${recordDate.getFullYear()}-${String(recordDate.getMonth() + 1).padStart(2, '0')}`
        if (!monthlyTrend[monthKey]) {
            monthlyTrend[monthKey] = { outgoingNormal: 0, outgoingRegistered: 0, outgoingEms: 0, outgoingTotal: 0, incomingTotal: 0 }
        }

        const n = record.normalMail || 0
        const r = record.registeredMail || 0
        const e = record.emsMail || 0
        const inTotal = record.incomingTotalMail || 0
        const nTotalPrice = record.normalMailUnitPrice || 0
        const rTotalPrice = record.registeredMailUnitPrice || 0
        const eTotalPrice = record.emsMailUnitPrice || 0

        monthlyTrend[monthKey].outgoingNormal += n
        monthlyTrend[monthKey].outgoingRegistered += r
        monthlyTrend[monthKey].outgoingEms += e
        monthlyTrend[monthKey].outgoingTotal += (n + r + e)
        monthlyTrend[monthKey].incomingTotal += inTotal

        tNormal += n
        tRegistered += r
        tEms += e
        tIncomingTotal += inTotal
        tNormalCost += nTotalPrice
        tRegisteredCost += rTotalPrice
        tEmsCost += eTotalPrice
    })

    sumNormal.value = tNormal
    sumRegistered.value = tRegistered
    sumEms.value = tEms
    sumTotal.value = tNormal + tRegistered + tEms
    sumIncomingTotal.value = tIncomingTotal
    sumNormalCost.value = tNormalCost
    sumRegisteredCost.value = tRegisteredCost
    sumEmsCost.value = tEmsCost
    sumTotalCost.value = tNormalCost + tRegisteredCost + tEmsCost

    setupCharts(monthlyTrend)
}

onMounted(() => fetchData())
watch(selectedDateRange, () => processData())
const clearDateFilter = () => { selectedDateRange.value = getDefaultDateRange() }

const formatChartLabel = (sortKey: string): string => {
    const parts = sortKey.split('-')
    const yearStr = parts[0] || ''
    const monthStr = parts[1] || '1'
    return `${thaiMonthShort[parseInt(monthStr, 10) - 1]} ${yearStr}`
}

// ─── Chart Setup ────────────────────────────────────────────────────────
const setupCharts = (monthlyData: Record<string, MonthlyData>): void => {
    const sortedKeys = Object.keys(monthlyData).sort()
    const labels = sortedKeys.map(k => formatChartLabel(k))

    outgoingTrendChartData.value = {
        labels,
        datasets: [
            { type: 'bar', label: 'ไปรษณีย์ธรรมดา', backgroundColor: '#94a3b8', data: sortedKeys.map(k => monthlyData[k]?.outgoingNormal ?? 0) },
            { type: 'bar', label: 'ลงทะเบียน', backgroundColor: '#3b82f6', data: sortedKeys.map(k => monthlyData[k]?.outgoingRegistered ?? 0) },
            { type: 'bar', label: 'EMS', backgroundColor: '#f43f5e', borderRadius: { topLeft: 4, topRight: 4 }, data: sortedKeys.map(k => monthlyData[k]?.outgoingEms ?? 0) },
        ]
    }

    incomingTrendChartData.value = {
        labels,
        datasets: [
            { type: 'bar', label: 'ไปรษณีย์เข้า (รวม)', backgroundColor: '#10b981', borderRadius: { topLeft: 4, topRight: 4 }, data: sortedKeys.map(k => monthlyData[k]?.incomingTotal ?? 0) },
        ]
    }

    trendChartOptions.value = {
        maintainAspectRatio: false, aspectRatio: 0.6,
        interaction: { mode: 'index', intersect: false },
        scales: { x: { stacked: true, grid: { display: false } }, y: { stacked: true, title: { display: true, text: 'จำนวน (ชิ้น)' } } }
    }

    outgoingProportionChartData.value = {
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
                <p class="text-gray-500 mt-1">วิเคราะห์ทั้งไปรษณีย์ออก และไปรษณีย์เข้าหน่วยงานในช่วงเวลาที่เลือก</p>
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
                            <p class="text-xs text-gray-500 mt-1">{{ sumNormalCost.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} บาท</p>
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
                            <p class="text-xs text-gray-500 mt-1">{{ sumRegisteredCost.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} บาท</p>
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
                            <p class="text-xs text-gray-500 mt-1">{{ sumEmsCost.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} บาท</p>
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
                            <p class="text-sm font-bold text-sky-700 mt-1">{{ sumTotalCost.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} บาท</p>
                        </div>
                        <div class="w-10 h-10 bg-sky-100 rounded-full flex items-center justify-center text-sky-600"><i
                                class="pi pi-box"></i></div>
                    </div>
                </template>
            </Card>
        </div>

        <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
            <Card class="shadow-sm border-t-4 border-emerald-600 bg-emerald-50/30 lg:col-span-1">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-emerald-700 font-black mb-1 uppercase">รวมไปรษณีย์เข้าทั้งหมด</p>
                            <h3 class="text-3xl font-black text-emerald-700">{{ sumIncomingTotal.toLocaleString() }} <span
                                    class="text-sm font-bold">ชิ้น</span></h3>
                        </div>
                        <div class="w-10 h-10 bg-emerald-100 rounded-full flex items-center justify-center text-emerald-600"><i
                                class="pi pi-inbox"></i></div>
                    </div>
                </template>
            </Card>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
            <Card class="shadow-sm border-none lg:col-span-2">
                <template #title>
                    <div class="text-lg font-bold text-gray-700">แนวโน้มปริมาณไปรษณีย์ออก</div>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-80 flex items-center justify-center"><i
                            class="pi pi-spin pi-spinner text-4xl text-blue-500"></i></div>
                    <div v-else-if="outgoingTrendChartData?.labels?.length === 0"
                        class="h-80 flex flex-col items-center justify-center text-gray-400"><i
                            class="pi pi-box text-3xl mb-2"></i>
                        <p>ไม่มีข้อมูล</p>
                    </div>
                    <div v-else class="h-80 relative">
                        <Chart type="bar" :data="outgoingTrendChartData" :options="trendChartOptions" class="h-full w-full" />
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-none lg:col-span-1">
                <template #title>
                    <div class="text-lg font-bold text-gray-700">สัดส่วนประเภทไปรษณีย์ออก</div>
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
                        <Chart type="doughnut" :data="outgoingProportionChartData" :options="proportionChartOptions"
                            class="w-full max-w-xs" />
                    </div>
                </template>
            </Card>
        </div>

        <div class="grid grid-cols-1 gap-6 mb-6">
            <Card class="shadow-sm border-none">
                <template #title>
                    <div class="text-lg font-bold text-gray-700">แนวโน้มปริมาณไปรษณีย์เข้าหน่วยงาน</div>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-80 flex items-center justify-center"><i
                            class="pi pi-spin pi-spinner text-4xl text-emerald-500"></i></div>
                    <div v-else-if="incomingTrendChartData?.labels?.length === 0"
                        class="h-80 flex flex-col items-center justify-center text-gray-400"><i
                            class="pi pi-box text-3xl mb-2"></i>
                        <p>ไม่มีข้อมูล</p>
                    </div>
                    <div v-else class="h-80 relative">
                        <Chart type="bar" :data="incomingTrendChartData" :options="trendChartOptions" class="h-full w-full" />
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
