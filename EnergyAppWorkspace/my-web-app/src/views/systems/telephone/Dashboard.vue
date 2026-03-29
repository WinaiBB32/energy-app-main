<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import Chart from 'primevue/chart'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'

defineOptions({ name: 'TelephoneDashboard' })

// 1. Interfaces
interface FetchedTelephoneRecord {
    id: string
    billingCycle: string | null
    phoneNumber: string
    providerName: string
    usageAmount: number
    vatAmount: number
    totalAmount: number
    recordedBy: string
    createdAt: string
}

interface MonthlyData {
    usage: number
    vat: number
    total: number
}

// 2. State & Auth
const authStore = useAuthStore()
const toast = useAppToast()
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('telephone')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const rawRecords = ref<FetchedTelephoneRecord[]>([])

// KPIs
const totalExpense = ref<number>(0)
const totalUsageAmount = ref<number>(0)
const totalVatAmount = ref<number>(0)
const totalProviders = ref<number>(0)

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

// Charts
const trendChartData = ref()
const trendChartOptions = ref()
const breakdownChartData = ref()
const breakdownChartOptions = ref()
const providerChartData = ref()
const providerChartOptions = ref()

const isLoading = ref<boolean>(true)

// 3. Fetch data
const fetchData = async (): Promise<void> => {
    isLoading.value = true
    try {
        const params: Record<string, unknown> = { take: 100 }
        const startDate = selectedDateRange.value?.[0] || null
        const endDate = selectedDateRange.value?.[1] ? new Date(selectedDateRange.value[1]) : null
        if (endDate) endDate.setHours(23, 59, 59, 999)
        if (startDate) params.fromDate = startDate.toISOString()
        if (endDate) params.toDate = endDate.toISOString()

        const response = await api.get('/TelephoneRecord', { params })
        rawRecords.value = response.data as FetchedTelephoneRecord[]
        processData()
    } catch (error: unknown) {
        toast.fromError(error, 'ไม่สามารถโหลดข้อมูล Dashboard ค่าโทรศัพท์ได้')
    } finally {
        isLoading.value = false
    }
}

onMounted(() => fetchData())
watch(selectedDateRange, () => fetchData())
const clearDateFilter = (): void => { selectedDateRange.value = getLastMonthRange() }

// 4. Process data
const processData = (): void => {
    let tempExpense = 0
    let tempUsage = 0
    let tempVat = 0

    const monthlyData: Record<string, MonthlyData> = {}
    const providerExpenses: Record<string, number> = {}
    const uniqueProviders = new Set<string>()

    rawRecords.value.forEach((data) => {
        if (!data.billingCycle) return
        const recordDateObj = new Date(data.billingCycle)
        const sortKey = `${recordDateObj.getFullYear()}-${String(recordDateObj.getMonth() + 1).padStart(2, '0')}`

        if (!monthlyData[sortKey]) monthlyData[sortKey] = { usage: 0, vat: 0, total: 0 }

        const amount = data.totalAmount || 0
        const usage = data.usageAmount || 0
        const vat = data.vatAmount || 0

        tempExpense += amount
        tempUsage += usage
        tempVat += vat

        monthlyData[sortKey].usage += usage
        monthlyData[sortKey].vat += vat
        monthlyData[sortKey].total += amount

        const provider = data.providerName || 'ไม่ระบุ'
        if (!providerExpenses[provider]) providerExpenses[provider] = 0
        providerExpenses[provider] += amount

        if (data.providerName) uniqueProviders.add(data.providerName)
    })

    totalExpense.value = tempExpense
    totalUsageAmount.value = tempUsage
    totalVatAmount.value = tempVat
    totalProviders.value = uniqueProviders.size

    setupCharts(monthlyData, providerExpenses)
}

const formatChartLabel = (sortKey: string): string => {
    const [yearStr = '', monthStr = '1'] = sortKey.split('-')
    const monthNames = ['ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.', 'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.']
    return `${monthNames[parseInt(monthStr, 10) - 1]} ${yearStr}`
}

// 5. Setup charts
const setupCharts = (monthlyData: Record<string, MonthlyData>, providerExpenses: Record<string, number>): void => {
    const sortedKeys = Object.keys(monthlyData).sort()
    const labels = sortedKeys.map((key) => formatChartLabel(key))

    trendChartData.value = {
        labels,
        datasets: [
            {
                type: 'line',
                label: 'ยอดรวมสุทธิ (รวม VAT)',
                borderColor: '#10b981',
                backgroundColor: '#10b981',
                borderWidth: 3,
                tension: 0.4,
                data: sortedKeys.map((key) => monthlyData[key]?.total ?? 0),
            },
            {
                type: 'bar',
                label: 'ค่าใช้บริการ (บาท)',
                backgroundColor: '#3b82f6',
                data: sortedKeys.map((key) => monthlyData[key]?.usage ?? 0),
            },
            {
                type: 'bar',
                label: 'VAT 7% (บาท)',
                backgroundColor: '#9ca3af',
                data: sortedKeys.map((key) => monthlyData[key]?.vat ?? 0),
            },
        ],
    }
    trendChartOptions.value = {
        maintainAspectRatio: false,
        aspectRatio: 0.6,
        interaction: { mode: 'index', intersect: false },
        scales: {
            x: { stacked: true, grid: { display: false } },
            y: { stacked: true, display: true, title: { display: true, text: 'จำนวนเงิน (บาท)' } },
        },
    }

    breakdownChartData.value = {
        labels: ['ค่าบริการ', 'ภาษี (VAT)'],
        datasets: [
            {
                data: [totalUsageAmount.value, totalVatAmount.value],
                backgroundColor: ['#3b82f6', '#9ca3af'],
                borderWidth: 0,
            },
        ],
    }
    breakdownChartOptions.value = { plugins: { legend: { position: 'bottom' } }, cutout: '50%' }

    const sortedProviders = Object.entries(providerExpenses)
        .sort((a, b) => b[1] - a[1])
        .slice(0, 10)

    providerChartData.value = {
        labels: sortedProviders.map(item => item[0]),
        datasets: [
            {
                label: 'ยอดชำระสะสม (บาท)',
                data: sortedProviders.map(item => item[1]),
                backgroundColor: '#10b981',
                borderRadius: 4,
            },
        ],
    }
    providerChartOptions.value = {
        indexAxis: 'y',
        maintainAspectRatio: false,
        aspectRatio: 0.8,
        plugins: { legend: { display: false } },
    }
}

const formatCurrency = (val: number): string => new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val)
</script>

<template>
    <div class="max-w-7xl mx-auto pb-10">
        <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
            <div>
                <h2 class="text-3xl font-bold text-gray-800">
                    <i class="pi pi-chart-bar text-green-500 mr-2"></i>ภาพรวมค่าบริการสื่อสาร
                </h2>
                <p class="text-gray-500 mt-1">วิเคราะห์ค่าใช้จ่ายโทรศัพท์และอินเทอร์เน็ต</p>
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

        <!-- ช่วงเวลาที่แสดง -->
        <div class="flex items-center gap-2 mb-4">
            <i class="pi pi-calendar-clock text-green-400 text-sm"></i>
            <span class="text-sm text-gray-500">กำลังแสดงข้อมูล: </span>
            <span class="text-sm font-bold text-green-600 bg-green-50 px-3 py-0.5 rounded-full border border-green-200">{{ dateRangeLabel }}</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
            <Card class="shadow-sm border-t-4 border-green-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ค่าบริการรวมสุทธิ (รวม VAT)
                            </p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ formatCurrency(totalExpense) }}</h3>
                        </div>
                        <div class="w-10 h-10 bg-green-50 rounded-full flex items-center justify-center text-green-500">
                            <i class="pi pi-money-bill"></i>
                        </div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-blue-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ค่าใช้บริการรวม</p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ formatCurrency(totalUsageAmount) }}</h3>
                        </div>
                        <div class="w-10 h-10 bg-blue-50 rounded-full flex items-center justify-center text-blue-500"><i
                                class="pi pi-globe"></i></div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-gray-400">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">VAT รวม</p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ formatCurrency(totalVatAmount) }}</h3>
                        </div>
                        <div class="w-10 h-10 bg-gray-100 rounded-full flex items-center justify-center text-gray-500">
                            <i class="pi pi-percentage"></i></div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-orange-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">จำนวนผู้ให้บริการ</p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ totalProviders.toLocaleString() }} <span
                                    class="text-sm font-normal text-gray-500">ราย</span></h3>
                        </div>
                        <div
                            class="w-10 h-10 bg-orange-50 rounded-full flex items-center justify-center text-orange-500">
                            <i class="pi pi-users"></i></div>
                    </div>
                </template>
            </Card>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
            <Card class="shadow-sm border-none lg:col-span-2">
                <template #title>
                    <div class="text-lg font-bold text-gray-700">แนวโน้มค่าบริการรายเดือน</div>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-80 flex items-center justify-center"><i
                            class="pi pi-spin pi-spinner text-4xl text-green-500"></i></div>
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
                    <div class="text-lg font-bold text-gray-700">โครงสร้างค่าใช้จ่าย</div>
                </template>
                <template #content>
                    <div v-if="isLoading" class="h-80 flex items-center justify-center"><i
                            class="pi pi-spin pi-spinner text-4xl text-green-500"></i></div>
                    <div v-else-if="totalExpense === 0"
                        class="h-80 flex flex-col items-center justify-center text-gray-400">
                        <i class="pi pi-chart-pie text-3xl mb-2"></i>
                        <p>ไม่มีข้อมูล</p>
                    </div>
                    <div v-else class="h-80 relative flex items-center justify-center">
                        <Chart type="doughnut" :data="breakdownChartData" :options="breakdownChartOptions"
                            class="w-full max-w-xs" />
                    </div>
                </template>
            </Card>
        </div>

        <Card class="shadow-sm border-none">
            <template #title>
                <div class="text-lg font-bold text-gray-700">จัดอันดับผู้ให้บริการที่มียอดชำระสูงสุด (10 อันดับแรก)</div>
            </template>
            <template #content>
                <div v-if="isLoading" class="h-64 flex items-center justify-center"><i
                        class="pi pi-spin pi-spinner text-4xl text-green-500"></i></div>
                <div v-else-if="providerChartData?.labels?.length === 0"
                    class="h-64 flex flex-col items-center justify-center text-gray-400">
                    <i class="pi pi-align-left text-3xl mb-2"></i>
                    <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-64 relative">
                    <Chart type="bar" :data="providerChartData" :options="providerChartOptions" class="h-full w-full" />
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
