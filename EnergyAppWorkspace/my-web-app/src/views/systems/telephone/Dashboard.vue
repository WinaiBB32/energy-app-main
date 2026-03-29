<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { collection, query, getDocs, where, orderBy, Timestamp } from 'firebase/firestore'
import { db } from '@/firebase/config'
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
    departmentId: string
    customerId: string
    adslCost: number
    sipTrunkCost: number
    otherCost: number
    vatAmount: number
    totalAmount: number
    billingCycle: Timestamp | null
}

interface MonthlyData {
    adsl: number
    sipTrunk: number
    other: number
    total: number // เพิ่มฟิลด์สำหรับเก็บยอดรวม
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
const totalAdslCost = ref<number>(0)
const totalSipTrunkCost = ref<number>(0)
const totalCustomers = ref<number>(0)

const sumOther = ref<number>(0)
const sumVat = ref<number>(0)

// เริ่มต้นด้วยช่วงเดือนที่แล้ว
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
const customerChartData = ref()
const customerChartOptions = ref()

const isLoading = ref<boolean>(true)

// 3. ดึงข้อมูลแบบ One-time (ลด Reads)
const fetchData = async (): Promise<void> => {
    isLoading.value = true
    try {
        const startDate = selectedDateRange.value?.[0] || null
        const endDate = selectedDateRange.value?.[1] ? new Date(selectedDateRange.value[1]) : null
        if (endDate) endDate.setHours(23, 59, 59, 999)

        const phoneRef = collection(db, 'telephone_records')
        const constraints: Parameters<typeof query>[1][] = []
        if (!isAdmin) constraints.push(where('departmentId', '==', currentUserDepartment.value))
        if (startDate && endDate) {
            constraints.push(where('billingCycle', '>=', Timestamp.fromDate(startDate)))
            constraints.push(where('billingCycle', '<=', Timestamp.fromDate(endDate)))
        }
        constraints.push(orderBy('billingCycle', 'desc'))
        const snapshot = await getDocs(query(phoneRef, ...constraints))
        rawRecords.value = snapshot.docs.map((doc) => doc.data() as FetchedTelephoneRecord)
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

// 4. ประมวลผลข้อมูล
const processData = (): void => {
    let tempExpense = 0
    let tempAdsl = 0
    let tempSipTrunk = 0
    let tempOther = 0
    let tempVat = 0

    const monthlyData: Record<string, MonthlyData> = {}
    const customerExpenses: Record<string, number> = {}
    const uniqueCustomers = new Set<string>()

    rawRecords.value.forEach((data) => {
        if (!data.billingCycle) return
        const recordDateObj = data.billingCycle.toDate()

        const sortKey = `${recordDateObj.getFullYear()}-${String(recordDateObj.getMonth() + 1).padStart(2, '0')}`
        if (!monthlyData[sortKey]) monthlyData[sortKey] = { adsl: 0, sipTrunk: 0, other: 0, total: 0 }

        const amount = data.totalAmount || 0
        const adsl = data.adslCost || 0
        const sipTrunk = data.sipTrunkCost || 0
        const other = data.otherCost || 0
        const vat = data.vatAmount || 0

        tempExpense += amount
        tempAdsl += adsl
        tempSipTrunk += sipTrunk
        tempOther += other
        tempVat += vat

        monthlyData[sortKey].adsl += adsl
        monthlyData[sortKey].sipTrunk += sipTrunk
        monthlyData[sortKey].other += other
        monthlyData[sortKey].total += amount // สะสมยอดรวมรายเดือน

        const cId = data.customerId || 'ไม่ระบุ'
        if (!customerExpenses[cId]) customerExpenses[cId] = 0
        customerExpenses[cId] += amount

        if (data.customerId) uniqueCustomers.add(data.customerId)
    })

    totalExpense.value = tempExpense
    totalAdslCost.value = tempAdsl
    totalSipTrunkCost.value = tempSipTrunk
    totalCustomers.value = uniqueCustomers.size

    sumOther.value = tempOther
    sumVat.value = tempVat

    setupCharts(monthlyData, customerExpenses)
}

const formatChartLabel = (sortKey: string): string => {
    const [yearStr = '', monthStr = '1'] = sortKey.split('-')
    const monthNames = ['ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.', 'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.']
    return `${monthNames[parseInt(monthStr, 10) - 1]} ${yearStr}`
}

// 5. เตรียมกราฟ
const setupCharts = (monthlyData: Record<string, MonthlyData>, customerExpenses: Record<string, number>): void => {
    const sortedKeys = Object.keys(monthlyData).sort()
    const labels = sortedKeys.map((key) => formatChartLabel(key))

    // กราฟที่ 1: กราฟผสม (Mixed Chart) แสดงยอดรวมแบบเส้น + สัดส่วนแบบแท่ง
    trendChartData.value = {
        labels,
        datasets: [
            {
                type: 'line', // กราฟเส้นแสดงยอดรวมสุทธิ
                label: 'ยอดรวมสุทธิ (รวม VAT)',
                borderColor: '#10b981', // สีเขียว
                backgroundColor: '#10b981',
                borderWidth: 3,
                tension: 0.4,
                data: sortedKeys.map((key) => monthlyData[key]?.total ?? 0),
            },
            {
                type: 'bar',
                label: 'ค่าบริการ ADSL (บาท)',
                backgroundColor: '#3b82f6', // สีน้ำเงิน
                data: sortedKeys.map((key) => monthlyData[key]?.adsl ?? 0),
            },
            {
                type: 'bar',
                label: 'ค่าบริการ SIP Trunk (บาท)',
                backgroundColor: '#8b5cf6', // สีม่วง
                data: sortedKeys.map((key) => monthlyData[key]?.sipTrunk ?? 0),
            },
            {
                type: 'bar',
                label: 'บริการอื่นๆ (บาท)',
                backgroundColor: '#f59e0b', // สีส้ม
                data: sortedKeys.map((key) => monthlyData[key]?.other ?? 0),
            }
        ],
    }
    trendChartOptions.value = {
        maintainAspectRatio: false,
        aspectRatio: 0.6,
        interaction: {
            mode: 'index',
            intersect: false,
        },
        scales: {
            x: { stacked: true, grid: { display: false } },
            y: { stacked: true, display: true, title: { display: true, text: 'จำนวนเงิน (บาท)' } },
        },
    }

    // กราฟที่ 2: โดนัท สัดส่วนค่าใช้จ่ายทั้งหมด
    breakdownChartData.value = {
        labels: ['ADSL', 'SIP Trunk', 'บริการอื่นๆ', 'ภาษี (VAT)'],
        datasets: [
            {
                data: [totalAdslCost.value, totalSipTrunkCost.value, sumOther.value, sumVat.value],
                backgroundColor: ['#3b82f6', '#8b5cf6', '#f59e0b', '#9ca3af'],
                borderWidth: 0,
            },
        ],
    }
    breakdownChartOptions.value = { plugins: { legend: { position: 'bottom' } }, cutout: '50%' }

    // กราฟที่ 3: จัดอันดับลูกค้าที่มียอดชำระสูงสุด
    const sortedCustomers = Object.entries(customerExpenses)
        .sort((a, b) => b[1] - a[1])
        .slice(0, 10)

    customerChartData.value = {
        labels: sortedCustomers.map(item => item[0]),
        datasets: [
            {
                label: 'ยอดชำระสะสม (บาท)',
                data: sortedCustomers.map(item => item[1]),
                backgroundColor: '#10b981', // สีเขียว
                borderRadius: 4,
            },
        ],
    }
    customerChartOptions.value = {
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
                <p class="text-gray-500 mt-1">วิเคราะห์ค่าใช้จ่าย ADSL, SIP Trunk และจัดอันดับรหัสลูกค้า</p>
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
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ค่าบริการ ADSL รวม</p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ formatCurrency(totalAdslCost) }}</h3>
                        </div>
                        <div class="w-10 h-10 bg-blue-50 rounded-full flex items-center justify-center text-blue-500"><i
                                class="pi pi-globe"></i></div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-purple-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">ค่าบริการ SIP Trunk รวม</p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ formatCurrency(totalSipTrunkCost) }}</h3>
                        </div>
                        <div
                            class="w-10 h-10 bg-purple-50 rounded-full flex items-center justify-center text-purple-500">
                            <i class="pi pi-server"></i></div>
                    </div>
                </template>
            </Card>

            <Card class="shadow-sm border-t-4 border-orange-500">
                <template #content>
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">จำนวนรหัสลูกค้า</p>
                            <h3 class="text-2xl font-bold text-gray-800">{{ totalCustomers.toLocaleString() }} <span
                                    class="text-sm font-normal text-gray-500">บัญชี</span></h3>
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
                <div class="text-lg font-bold text-gray-700">จัดอันดับรหัสลูกค้าที่มียอดชำระสูงสุด (10 อันดับแรก)</div>
            </template>
            <template #content>
                <div v-if="isLoading" class="h-64 flex items-center justify-center"><i
                        class="pi pi-spin pi-spinner text-4xl text-green-500"></i></div>
                <div v-else-if="customerChartData?.labels?.length === 0"
                    class="h-64 flex flex-col items-center justify-center text-gray-400">
                    <i class="pi pi-align-left text-3xl mb-2"></i>
                    <p>ไม่มีข้อมูล</p>
                </div>
                <div v-else class="h-64 relative">
                    <Chart type="bar" :data="customerChartData" :options="customerChartOptions" class="h-full w-full" />
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