<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import Chart from 'primevue/chart'
import DatePicker from 'primevue/datepicker'
import MultiSelect from 'primevue/multiselect'
import Button from 'primevue/button'

defineOptions({ name: 'SarabanDashboard' })

// ─── Interfaces ───────────────────────────────────────────────────────────────
interface SarabanStat {
    id: string
    departmentId: string | null
    bookType: string
    bookName: string
    recordMonth: string
    receiverName: string
    receivedCount: number
    internalPaperCount: number
    internalDigitalCount: number
    externalPaperCount: number
    externalDigitalCount: number
    forwardedCount: number
    recordedBy: string
    createdAt: string
}

// ─── Auth ─────────────────────────────────────────────────────────────────────
const authStore = useAuthStore()
const toast = useAppToast()
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('saraban')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

// ─── Raw Data ─────────────────────────────────────────────────────────────────
const rawRecords = ref<SarabanStat[]>([])
const isLoading = ref(true)

// ─── Filters ──────────────────────────────────────────────────────────────────
const getThisYearRange = (): Date[] => {
    const now = new Date()
    const first = new Date(now.getFullYear(), now.getMonth() - 1, 1)
    const last = new Date(now.getFullYear(), now.getMonth(), 0)
    return [first, last]
}
const selectedDateRange = ref<Date[] | null>(getThisYearRange())
const selectedBookNames = ref<string[]>(['สำนักงานคณะกรรมการอาหารและยา'])
const selectedPersonNames = ref<string[]>([])

const thaiMonthShort = ['ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.', 'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.']

const dateRangeLabel = computed(() => {
    const r = selectedDateRange.value
    if (!r || r.length < 2 || !r[0] || !r[1]) return 'ทุกช่วงเวลา'
    const fmt = (d: Date) => `${d.getDate()} ${thaiMonthShort[d.getMonth()]} ${d.getFullYear() + 543}`
    return `${fmt(r[0])} – ${fmt(r[1])}`
})

const bookOptions = computed(() => {
    const names = new Set<string>()
    rawRecords.value.forEach(r => { if (r.bookName) names.add(r.bookName) })
    return Array.from(names).sort().map(n => ({ label: n, value: n }))
})

const personOptions = computed(() => {
    const names = new Set<string>()
    rawRecords.value.forEach(r => { if (r.receiverName) names.add(r.receiverName) })
    return Array.from(names).sort().map(n => ({ label: n, value: n }))
})

const hasActiveFilters = computed(() =>
    selectedBookNames.value.length > 0 || selectedPersonNames.value.length > 0
)

const clearFilters = () => {
    selectedDateRange.value = getThisYearRange()
    selectedBookNames.value = []
    selectedPersonNames.value = []
}

// ─── Filtered Records ─────────────────────────────────────────────────────────
const filteredRecords = computed(() => {
    const start = selectedDateRange.value?.[0] ?? null
    const endRaw = selectedDateRange.value?.[1] ?? null
    const end = endRaw ? new Date(new Date(endRaw).setHours(23, 59, 59, 999)) : null

    return rawRecords.value.filter(r => {
        if (!isAdmin && r.departmentId !== currentUserDepartment.value) return false

        const d = new Date(r.recordMonth)
        if (start && d < start) return false
        if (end && d > end) return false
        if (selectedBookNames.value.length > 0 && !selectedBookNames.value.includes(r.bookName)) return false
        if (selectedPersonNames.value.length > 0 && !selectedPersonNames.value.includes(r.receiverName)) return false
        return true
    })
})

// ─── KPIs ─────────────────────────────────────────────────────────────────────
const kpi = computed(() => {
    let received = 0, intPaper = 0, intDigital = 0, extPaper = 0, extDigital = 0, forwarded = 0

    filteredRecords.value.forEach(r => {
        received   += r.receivedCount
        intPaper   += r.internalPaperCount
        intDigital += r.internalDigitalCount
        extPaper   += r.externalPaperCount
        extDigital += r.externalDigitalCount
        forwarded  += r.forwardedCount
    })

    const totalDigital = intDigital + extDigital
    const totalPaper   = intPaper + extPaper
    const total = totalDigital + totalPaper
    const paperlessPct = total > 0 ? ((totalDigital / total) * 100).toFixed(1) : '0.0'
    const paperPct     = total > 0 ? ((totalPaper   / total) * 100).toFixed(1) : '0.0'

    return { received, intPaper, intDigital, extPaper, extDigital, forwarded, totalDigital, totalPaper, paperlessPct, paperPct }
})

// ─── Charts ───────────────────────────────────────────────────────────────────
const trendChartData = ref()
const trendChartOptions = ref()
const ratioChartData = ref()
const ratioChartOptions = ref()
const topPersonChartData = ref()
const topPersonChartOptions = ref()
const intExtChartData = ref()
const intExtChartOptions = ref()
const yoyChartData = ref()
const yoyChartOptions = ref()

watch(filteredRecords, buildCharts, { immediate: false })

function buildCharts() {
    const monthly: Record<string, {
        received: number; intPaper: number; intDigital: number
        extPaper: number; extDigital: number; forwarded: number
    }> = {}
    const personStats: Record<string, number> = {}

    filteredRecords.value.forEach(r => {
        const d = new Date(r.recordMonth)
        const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`

        if (!monthly[key]) monthly[key] = { received: 0, intPaper: 0, intDigital: 0, extPaper: 0, extDigital: 0, forwarded: 0 }
        monthly[key].received   += r.receivedCount
        monthly[key].intPaper   += r.internalPaperCount
        monthly[key].intDigital += r.internalDigitalCount
        monthly[key].extPaper   += r.externalPaperCount
        monthly[key].extDigital += r.externalDigitalCount
        monthly[key].forwarded  += r.forwardedCount

        const name = r.receiverName || 'ไม่ระบุ'
        personStats[name] = (personStats[name] || 0) + r.receivedCount
    })

    const sortedKeys = Object.keys(monthly).sort()
    const monthLabels = sortedKeys.map(k => {
        const [y, m] = k.split('-')
        return `${thaiMonthShort[parseInt(m ?? '1') - 1] ?? ''} ${y ?? ''}`
    })

    // Trend chart: รับเข้า (line) + กระดาษรวม + ดิจิทัลรวม + ส่งต่อ
    trendChartData.value = {
        labels: monthLabels,
        datasets: [
            {
                type: 'line',
                label: 'รับเข้าทั้งหมด',
                borderColor: '#9333ea',
                backgroundColor: '#9333ea22',
                borderWidth: 2,
                tension: 0.4,
                fill: true,
                data: sortedKeys.map(k => monthly[k]?.received ?? 0),
            },
            {
                type: 'bar',
                label: 'กระดาษ (รวม)',
                backgroundColor: '#f97316cc',
                borderRadius: 4,
                data: sortedKeys.map(k => (monthly[k]?.intPaper ?? 0) + (monthly[k]?.extPaper ?? 0)),
            },
            {
                type: 'bar',
                label: 'ดิจิทัล (รวม)',
                backgroundColor: '#3b82f6cc',
                borderRadius: 4,
                data: sortedKeys.map(k => (monthly[k]?.intDigital ?? 0) + (monthly[k]?.extDigital ?? 0)),
            },
            {
                type: 'bar',
                label: 'ส่งต่อ',
                backgroundColor: '#10b981cc',
                borderRadius: 4,
                data: sortedKeys.map(k => monthly[k]?.forwarded ?? 0),
            },
        ],
    }
    trendChartOptions.value = {
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } },
        scales: {
            x: { grid: { display: false } },
            y: { title: { display: true, text: 'จำนวน (ฉบับ)' }, beginAtZero: true },
        },
    }

    // Ratio doughnut (กระดาษ vs ดิจิทัล)
    ratioChartData.value = {
        labels: ['ดิจิทัล (ภายใน+ภายนอก)', 'กระดาษ (ภายใน+ภายนอก)'],
        datasets: [{
            data: [kpi.value.totalDigital, kpi.value.totalPaper],
            backgroundColor: ['#3b82f6', '#f97316'],
            borderWidth: 0,
        }],
    }
    ratioChartOptions.value = { plugins: { legend: { position: 'bottom' } }, cutout: '65%' }

    // กระดาษ vs ดิจิทัล stacked bar (รวม ภายใน+ภายนอก)
    intExtChartData.value = {
        labels: monthLabels,
        datasets: [
            {
                label: 'กระดาษ (รวม)',
                backgroundColor: '#f97316',
                data: sortedKeys.map(k => (monthly[k]?.intPaper ?? 0) + (monthly[k]?.extPaper ?? 0)),
                borderRadius: 4,
            },
            {
                label: 'ดิจิทัล (รวม)',
                backgroundColor: '#3b82f6',
                data: sortedKeys.map(k => (monthly[k]?.intDigital ?? 0) + (monthly[k]?.extDigital ?? 0)),
                borderRadius: 4,
            },
        ],
    }
    intExtChartOptions.value = {
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } },
        scales: {
            x: { stacked: true, grid: { display: false } },
            y: { stacked: true, title: { display: true, text: 'จำนวน (ฉบับ)' }, beginAtZero: true },
        },
    }

    // Top 10 persons
    const top10 = Object.entries(personStats).sort((a, b) => b[1] - a[1]).slice(0, 10)
    topPersonChartData.value = {
        labels: top10.map(x => x[0]),
        datasets: [{
            label: 'จำนวนเอกสารรับเข้า (ฉบับ)',
            data: top10.map(x => x[1]),
            backgroundColor: '#8b5cf6',
            borderRadius: 4,
        }],
    }
    topPersonChartOptions.value = {
        indexAxis: 'y',
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
            x: { grid: { color: '#f3f4f6' }, beginAtZero: true },
            y: { grid: { display: false }, ticks: { font: { size: 12 } } },
        },
    }

    // YoY: เปรียบเทียบ กระดาษ vs ดิจิทัล แต่ละปี (ใช้ rawRecords ไม่กรองวันที่)
    const yoyData: Record<number, Record<number, { paper: number; digital: number }>> = {}
    rawRecords.value.forEach(r => {
        if (!isAdmin && r.departmentId !== currentUserDepartment.value) return
        if (selectedBookNames.value.length > 0 && !selectedBookNames.value.includes(r.bookName)) return
        if (selectedPersonNames.value.length > 0 && !selectedPersonNames.value.includes(r.receiverName)) return

        const utcStr = /Z|[+-]\d{2}:\d{2}$/.test(r.recordMonth) ? r.recordMonth : r.recordMonth + 'Z'
        const d = new Date(utcStr)
        const yr = d.getUTCFullYear()
        const mo = d.getUTCMonth() + 1

        if (!yoyData[yr]) yoyData[yr] = {}
        if (!yoyData[yr][mo]) yoyData[yr][mo] = { paper: 0, digital: 0 }
        yoyData[yr][mo].paper   += r.internalPaperCount + r.externalPaperCount
        yoyData[yr][mo].digital += r.internalDigitalCount + r.externalDigitalCount
    })

    const years = Object.keys(yoyData).map(Number).sort()

    // สีต่อปี: ปีล่าสุด = ทึบ, ปีก่อน = เส้นประ
    const paperColors  = ['#f97316', '#fb923c', '#fdba74']
    const digitalColors = ['#3b82f6', '#60a5fa', '#93c5fd']

    const datasets: object[] = []
    years.forEach((yr, i) => {
        const isCurrent = i === years.length - 1
        const dash = isCurrent ? [] : [5, 4]
        const bw   = isCurrent ? 2.5 : 1.5
        const pColor = paperColors[years.length - 1 - i] ?? '#f97316'
        const dColor = digitalColors[years.length - 1 - i] ?? '#3b82f6'

        datasets.push({
            label: `กระดาษ ${yr + 543}`,
            data: Array.from({ length: 12 }, (_, m) => yoyData[yr]?.[m + 1]?.paper ?? 0),
            backgroundColor: pColor + (isCurrent ? 'dd' : '66'),
            borderColor: pColor,
            borderWidth: 1,
            borderRadius: 3,
        })
        datasets.push({
            label: `ดิจิทัล ${yr + 543}`,
            data: Array.from({ length: 12 }, (_, m) => yoyData[yr]?.[m + 1]?.digital ?? 0),
            backgroundColor: dColor + (isCurrent ? 'dd' : '66'),
            borderColor: dColor,
            borderWidth: 1,
            borderRadius: 3,
        })
    })

    yoyChartData.value = { labels: thaiMonthShort, datasets }
    yoyChartOptions.value = {
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: { position: 'bottom', labels: { boxWidth: 14, padding: 12 } },
        },
        scales: {
            x: { grid: { display: false } },
            y: { title: { display: true, text: 'จำนวน (ฉบับ)' }, beginAtZero: true },
        },
    }
}

const fetchData = async () => {
    isLoading.value = true
    try {
        const { data } = await api.get('/SarabanStat', { params: { take: 20000 } })
        rawRecords.value = data.items || []
        buildCharts()
    } catch (err) {
        toast.fromError(err, 'ไม่สามารถโหลดข้อมูลสารบรรณได้')
    } finally {
        isLoading.value = false
    }
}

onMounted(() => { fetchData() })
</script>

<template>
    <div class="max-w-7xl mx-auto pb-10">

        <!-- Header -->
        <div class="mb-6 flex flex-col xl:flex-row xl:items-end justify-between gap-4">
            <div>
                <h2 class="text-3xl font-bold text-gray-800">
                    <i class="pi pi-chart-bar text-purple-600 mr-2"></i>ภาพรวมงานสารบรรณ
                </h2>
                <p class="text-gray-500 mt-1">ติดตามปริมาณเอกสารรับ-ลงรับ-ส่งต่อ และสัดส่วน Paperless รายบุคคล</p>
            </div>
        </div>

        <!-- Filters -->
        <div class="bg-white rounded-xl shadow-sm border border-gray-100 p-4 mb-4 flex flex-wrap items-end gap-3">
            <div class="flex flex-col gap-1 min-w-[200px]">
                <label class="text-xs font-semibold text-gray-500">ช่วงวันที่</label>
                <DatePicker v-model="selectedDateRange" selectionMode="range" dateFormat="dd/mm/yy"
                    placeholder="เริ่มต้น – สิ้นสุด" :manualInput="false" showIcon class="w-full" />
            </div>
            <div class="flex flex-col gap-1 min-w-[220px]">
                <label class="text-xs font-semibold text-gray-500">
                    เล่มทะเบียน
                    <span v-if="selectedBookNames.length > 0"
                        class="ml-1 bg-purple-100 text-purple-700 px-1.5 py-0.5 rounded-full text-[10px] font-bold">
                        {{ selectedBookNames.length }}
                    </span>
                </label>
                <MultiSelect v-model="selectedBookNames" :options="bookOptions" optionLabel="label" optionValue="value"
                    placeholder="ทุกเล่ม" :maxSelectedLabels="2" selectedItemsLabel="{0} เล่ม"
                    filter filterPlaceholder="ค้นหาเล่ม..." class="w-full" />
            </div>
            <div class="flex flex-col gap-1 min-w-[220px]">
                <label class="text-xs font-semibold text-gray-500">
                    รายชื่อผู้รับ
                    <span v-if="selectedPersonNames.length > 0"
                        class="ml-1 bg-purple-100 text-purple-700 px-1.5 py-0.5 rounded-full text-[10px] font-bold">
                        {{ selectedPersonNames.length }}
                    </span>
                </label>
                <MultiSelect v-model="selectedPersonNames" :options="personOptions" optionLabel="label" optionValue="value"
                    placeholder="ทุกคน" :maxSelectedLabels="2" selectedItemsLabel="{0} คน"
                    filter filterPlaceholder="ค้นหาชื่อ..." class="w-full" />
            </div>
            <Button v-if="hasActiveFilters" icon="pi pi-times" severity="secondary" text rounded
                v-tooltip.top="'ล้างตัวกรอง'" @click="clearFilters" />
        </div>

        <!-- Notice + date label -->
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 mb-5">
            <div class="flex items-center gap-2 px-3 py-2 bg-amber-50 border border-amber-200 rounded-lg">
                <i class="pi pi-clock text-amber-500 text-sm"></i>
                <span class="text-sm text-amber-700 font-medium">อัพเดทข้อมูลเวลา 12:00 น. ทุกวัน ยกเว้นวันหยุดราชการ</span>
            </div>
            <div class="flex items-center gap-2">
                <i class="pi pi-calendar-clock text-purple-400 text-sm"></i>
                <span class="text-sm text-gray-500">แสดงข้อมูล:</span>
                <span class="text-sm font-bold text-purple-600 bg-purple-50 px-3 py-0.5 rounded-full border border-purple-200">
                    {{ dateRangeLabel }}
                </span>
                <span class="text-sm text-gray-400">({{ filteredRecords.length.toLocaleString() }} รายการ)</span>
            </div>
        </div>

        <!-- KPI Cards (แถวแรก) -->
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-4">
            <Card class="shadow-sm border-t-4 border-purple-500 bg-purple-50/20 col-span-2 lg:col-span-1">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-[11px] text-purple-600 font-bold uppercase tracking-wide mb-1">รับเข้าทั้งหมด</p>
                            <p class="text-2xl font-black text-purple-900">{{ kpi.received.toLocaleString() }}</p>
                            <p class="text-xs text-purple-400 mt-0.5">ฉบับ</p>
                        </div>
                        <div class="w-10 h-10 bg-purple-100 rounded-full flex items-center justify-center text-purple-600 shrink-0">
                            <i class="pi pi-inbox"></i>
                        </div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-emerald-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-[11px] text-emerald-600 font-bold uppercase tracking-wide mb-1">ส่งต่อ</p>
                            <p class="text-2xl font-black text-emerald-700">{{ kpi.forwarded.toLocaleString() }}</p>
                            <p class="text-xs text-emerald-300 mt-0.5">ฉบับ</p>
                        </div>
                        <div class="w-10 h-10 bg-emerald-50 rounded-full flex items-center justify-center text-emerald-500 shrink-0">
                            <i class="pi pi-send"></i>
                        </div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-blue-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-[11px] text-blue-600 font-bold uppercase tracking-wide mb-1">รวมดิจิทัล</p>
                            <p class="text-2xl font-black text-blue-700">{{ kpi.totalDigital.toLocaleString() }}</p>
                            <p class="text-xs text-blue-300 mt-0.5">ภายใน {{ kpi.intDigital.toLocaleString() }} + ภายนอก {{ kpi.extDigital.toLocaleString() }}</p>
                        </div>
                        <div class="w-10 h-10 bg-blue-50 rounded-full flex items-center justify-center text-blue-500 shrink-0">
                            <i class="pi pi-desktop"></i>
                        </div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-teal-500 bg-teal-50/20">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-[11px] text-teal-600 font-bold uppercase tracking-wide mb-1">Paperless</p>
                            <p class="text-2xl font-black text-teal-700">{{ kpi.paperlessPct }}%</p>
                            <p class="text-xs text-teal-400 mt-0.5">สัดส่วนดิจิทัล</p>
                        </div>
                        <div class="w-10 h-10 bg-teal-100 rounded-full flex items-center justify-center text-teal-600 shrink-0">
                            <i class="pi pi-check-circle"></i>
                        </div>
                    </div>
                </template>
            </Card>
        </div>

        <!-- KPI Cards (แถวสอง: ภายใน/ภายนอก) -->
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
            <Card class="shadow-sm border-t-4 border-amber-400">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-[11px] text-amber-600 font-bold uppercase tracking-wide mb-1">ภายใน (กระดาษ)</p>
                            <p class="text-2xl font-black text-amber-600">{{ kpi.intPaper.toLocaleString() }}</p>
                            <p class="text-xs text-amber-300 mt-0.5">ฉบับ</p>
                        </div>
                        <div class="w-10 h-10 bg-amber-50 rounded-full flex items-center justify-center text-amber-500 shrink-0">
                            <i class="pi pi-file"></i>
                        </div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-cyan-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-[11px] text-cyan-600 font-bold uppercase tracking-wide mb-1">ภายใน (ดิจิทัล)</p>
                            <p class="text-2xl font-black text-cyan-700">{{ kpi.intDigital.toLocaleString() }}</p>
                            <p class="text-xs text-cyan-300 mt-0.5">ฉบับ</p>
                        </div>
                        <div class="w-10 h-10 bg-cyan-50 rounded-full flex items-center justify-center text-cyan-500 shrink-0">
                            <i class="pi pi-desktop"></i>
                        </div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-orange-400">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-[11px] text-orange-600 font-bold uppercase tracking-wide mb-1">ภายนอก (กระดาษ)</p>
                            <p class="text-2xl font-black text-orange-600">{{ kpi.extPaper.toLocaleString() }}</p>
                            <p class="text-xs text-orange-300 mt-0.5">ฉบับ</p>
                        </div>
                        <div class="w-10 h-10 bg-orange-50 rounded-full flex items-center justify-center text-orange-500 shrink-0">
                            <i class="pi pi-file-export"></i>
                        </div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-blue-400">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-[11px] text-blue-600 font-bold uppercase tracking-wide mb-1">ภายนอก (ดิจิทัล)</p>
                            <p class="text-2xl font-black text-blue-600">{{ kpi.extDigital.toLocaleString() }}</p>
                            <p class="text-xs text-blue-300 mt-0.5">ฉบับ</p>
                        </div>
                        <div class="w-10 h-10 bg-blue-50 rounded-full flex items-center justify-center text-blue-500 shrink-0">
                            <i class="pi pi-cloud"></i>
                        </div>
                    </div>
                </template>
            </Card>
        </div>

        <!-- Charts row 1: Trend + Ratio -->
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
            <Card class="shadow-sm border-none lg:col-span-2">
                <template #title>
                    <span class="text-base font-bold text-gray-700">แนวโน้มปริมาณเอกสารรายเดือน</span>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-80 flex items-center justify-center">
                        <i class="pi pi-spin pi-spinner text-4xl text-purple-400"></i>
                    </div>
                    <div v-else-if="!trendChartData?.labels?.length" class="h-80 flex flex-col items-center justify-center text-gray-300">
                        <i class="pi pi-chart-bar text-4xl mb-2"></i><p>ไม่มีข้อมูลในช่วงเวลานี้</p>
                    </div>
                    <div v-else class="h-80">
                        <Chart type="bar" :data="trendChartData" :options="trendChartOptions" class="h-full w-full" />
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-none lg:col-span-1">
                <template #title>
                    <span class="text-base font-bold text-gray-700">สัดส่วน กระดาษ vs ดิจิทัล</span>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-80 flex items-center justify-center">
                        <i class="pi pi-spin pi-spinner text-4xl text-purple-400"></i>
                    </div>
                    <div v-else-if="kpi.totalDigital + kpi.totalPaper === 0" class="h-80 flex flex-col items-center justify-center text-gray-300">
                        <i class="pi pi-chart-pie text-4xl mb-2"></i><p>ไม่มีข้อมูล</p>
                    </div>
                    <div v-else class="relative flex items-center justify-center py-4">
                        <Chart type="doughnut" :data="ratioChartData" :options="ratioChartOptions"
                            class="w-full max-w-[260px]" />
                        <div class="absolute pointer-events-none flex flex-col items-center gap-0.5"
                            style="top:50%; left:50%; transform:translate(-50%,-50%)">
                            <span class="text-base font-black text-blue-600 leading-none">{{ kpi.paperlessPct }}%</span>
                            <span class="text-[9px] text-gray-400 leading-none">ดิจิทัล</span>
                            <div class="w-5 h-px bg-gray-200 my-0.5"></div>
                            <span class="text-base font-black text-orange-500 leading-none">{{ kpi.paperPct }}%</span>
                            <span class="text-[9px] text-gray-400 leading-none">กระดาษ</span>
                        </div>
                    </div>
                </template>
            </Card>
        </div>

        <!-- Charts row 2: กระดาษ vs ดิจิทัล + Top 10 -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
            <Card class="shadow-sm border-none">
                <template #title>
                    <span class="text-base font-bold text-gray-700">เอกสารลงรับ กระดาษ vs ดิจิทัล รายเดือน</span>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-72 flex items-center justify-center">
                        <i class="pi pi-spin pi-spinner text-4xl text-purple-400"></i>
                    </div>
                    <div v-else-if="!intExtChartData?.labels?.length" class="h-72 flex flex-col items-center justify-center text-gray-300">
                        <i class="pi pi-chart-bar text-4xl mb-2"></i><p>ไม่มีข้อมูล</p>
                    </div>
                    <div v-else class="h-72">
                        <Chart type="bar" :data="intExtChartData" :options="intExtChartOptions" class="h-full w-full" />
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-none">
                <template #title>
                    <span class="text-base font-bold text-gray-700">Top 10 รายชื่อที่มีเอกสารรับเข้ามากที่สุด</span>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-72 flex items-center justify-center">
                        <i class="pi pi-spin pi-spinner text-4xl text-purple-400"></i>
                    </div>
                    <div v-else-if="!topPersonChartData?.labels?.length" class="h-72 flex flex-col items-center justify-center text-gray-300">
                        <i class="pi pi-users text-4xl mb-2"></i><p>ไม่มีข้อมูล</p>
                    </div>
                    <div v-else class="h-72">
                        <Chart type="bar" :data="topPersonChartData" :options="topPersonChartOptions" class="h-full w-full" />
                    </div>
                </template>
            </Card>
        </div>

        <!-- Charts row 3: YoY เปรียบเทียบปี -->
        <div class="mb-6">
            <Card class="shadow-sm border-none">
                <template #title>
                    <span class="text-base font-bold text-gray-700">
                        <i class="pi pi-chart-line text-purple-500 mr-2"></i>
                        แนวโน้มปริมาณเอกสารรายเดือน เปรียบเทียบปีก่อนหน้า
                    </span>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-80 flex items-center justify-center">
                        <i class="pi pi-spin pi-spinner text-4xl text-purple-400"></i>
                    </div>
                    <div v-else-if="!yoyChartData?.datasets?.length" class="h-80 flex flex-col items-center justify-center text-gray-300">
                        <i class="pi pi-chart-line text-4xl mb-2"></i><p>ไม่มีข้อมูลเพียงพอสำหรับเปรียบเทียบ</p>
                    </div>
                    <div v-else class="h-80">
                        <Chart type="bar" :data="yoyChartData" :options="yoyChartOptions" class="h-full w-full" />
                    </div>
                </template>
            </Card>
        </div>
    </div>
</template>

<style scoped>
.animate-fade-in { animation: fadeIn 0.3s ease-out; }
@keyframes fadeIn {
    from { opacity: 0; transform: translateX(-8px); }
    to   { opacity: 1; transform: translateX(0); }
}
</style>
