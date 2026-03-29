<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import {
    collection,
    query,
    getDocs,
    where,
    orderBy,
    Timestamp,
    type QueryConstraint // <-- Import Type ที่ถูกต้องมาใช้
} from 'firebase/firestore'
import { db } from '@/firebase/config'
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'
import type { MeetingRoom, MeetingRecord } from '@/types'

import Card from 'primevue/card'
import Chart from 'primevue/chart'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'

defineOptions({ name: 'MeetingDashboard' })
const toast = useAppToast()

// ─── Auth & State ───────────────────────────────────────────────────────────
const authStore = useAuthStore()
const currentUserRole = computed(() => authStore.userProfile?.role || 'user')

const rawRecords = ref<MeetingRecord[]>([])
const meetingRooms = ref<MeetingRoom[]>([])
const isLoading = ref<boolean>(true)

// ฟิลเตอร์ช่วงเวลาแบบ เดือน/ปี (Default: เดือนที่ผ่านมา)
const getDefaultDateRange = (): Date[] => {
    const now = new Date()
    const firstDayOfLastMonth = new Date(now.getFullYear(), now.getMonth() - 1, 1)
    const lastDayOfLastMonth = new Date(now.getFullYear(), now.getMonth(), 0)
    return [firstDayOfLastMonth, lastDayOfLastMonth]
}
const selectedDateRange = ref<Date[] | null>(getDefaultDateRange())

// ─── Data Processed State ───────────────────────────────────────────────────
interface RoomStat {
    roomId: string
    roomName: string
    usageCount: number
}

const roomStatsList = ref<RoomStat[]>([])
const totalUsage = ref<number>(0)

// Chart (กำหนด Type แบบ Record แทน any)
const barChartData = ref<Record<string, unknown> | undefined>()
const barChartOptions = ref<Record<string, unknown> | undefined>()

const thaiMonthShort = ['ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.', 'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.']
const dateRangeLabel = computed(() => {
    const r = selectedDateRange.value
    if (!r || r.length < 2 || !r[0] || !r[1]) return 'ทุกช่วงเวลา'
    const s = `${thaiMonthShort[r[0].getMonth()]} ${r[0].getFullYear() + 543}`
    const e = `${thaiMonthShort[r[1].getMonth()]} ${r[1].getFullYear() + 543}`
    return s === e ? s : `${s} - ${e}`
})

// ─── Chart Setup ────────────────────────────────────────────────────────────
const setupCharts = (stats: RoomStat[]): void => {
    const displayStats = stats.filter(s => s.usageCount > 0)

    barChartData.value = {
        labels: displayStats.map(s => s.roomName),
        datasets: [
            {
                label: 'จำนวนครั้งที่ใช้งาน',
                data: displayStats.map(s => s.usageCount),
                backgroundColor: '#0ea5e9',
                borderRadius: 4
            }
        ]
    }

    barChartOptions.value = {
        indexAxis: 'y',
        maintainAspectRatio: false,
        aspectRatio: 0.8,
        plugins: { legend: { display: false } },
        scales: {
            x: { grid: { color: '#f3f4f6' }, title: { display: true, text: 'ครั้ง' } },
            y: { grid: { display: false }, ticks: { font: { weight: 'bold' } } }
        }
    }
}

// ─── Process Data (จัดกลุ่มตามห้อง) ──────────────────────────────────────────
const processData = (): void => {
    const statsMap: Record<string, RoomStat> = {}
    meetingRooms.value.forEach(r => {
        statsMap[r.id] = { roomId: r.id, roomName: r.name, usageCount: 0 }
    })

    let sum = 0
    rawRecords.value.forEach(record => {
        const rId = record.roomId
        const count = record.usageCount || 0
        if (statsMap[rId]) {
            statsMap[rId].usageCount += count
        } else {
            statsMap[rId] = { roomId: rId, roomName: record.roomName || 'ไม่ทราบชื่อ', usageCount: count }
        }
        sum += count
    })

    const resultList = Object.values(statsMap).sort((a, b) => b.usageCount - a.usageCount)
    roomStatsList.value = resultList
    totalUsage.value = sum

    setupCharts(resultList)
}

// ─── Fetch Data ─────────────────────────────────────────────────────────────
const fetchData = async (): Promise<void> => {
    isLoading.value = true
    try {
        const startDate = selectedDateRange.value?.[0] || null
        // ใช้ startDate เป็น endDate หากผู้ใช้เลือกแค่เดือนเดียว
        let endDate = selectedDateRange.value?.[1] || startDate

        if (endDate) {
            endDate = new Date(endDate.getFullYear(), endDate.getMonth() + 1, 0)
            endDate.setHours(23, 59, 59, 999)
        }

        // 1. ดึงรายชื่อห้องประชุม (Master Data)
        const roomSnap = await getDocs(query(collection(db, 'meeting_rooms'), orderBy('name')))
        meetingRooms.value = roomSnap.docs.map(d => ({
            id: d.id,
            name: d.data().name as string,
            description: d.data().description as string
        }))

        // 2. ดึงสถิติการใช้งาน
        const recordsRef = collection(db, 'meeting_records')

        // เปลี่ยนจาก any[] เป็น QueryConstraint[]
        const constraints: QueryConstraint[] = []

        if (startDate && endDate) {
            constraints.push(where('recordMonthTs', '>=', Timestamp.fromDate(startDate)))
            constraints.push(where('recordMonthTs', '<=', Timestamp.fromDate(endDate)))
        }

        const snapshot = await getDocs(query(recordsRef, ...constraints))
        rawRecords.value = snapshot.docs.map(doc => doc.data() as MeetingRecord)

        processData()
    } catch (error: unknown) {
        toast.fromError(error, 'ไม่สามารถโหลดข้อมูล Dashboard ห้องประชุมได้')
    } finally {
        isLoading.value = false
    }
}

onMounted(() => fetchData())
watch(selectedDateRange, () => fetchData())

const clearDateFilter = (): void => {
    selectedDateRange.value = null
}
</script>

<template>
    <div class="max-w-7xl mx-auto pb-10">
        <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
            <div>
                <h2 class="text-3xl font-bold text-gray-800">
                    <i class="pi pi-users text-teal-500 mr-2"></i>สถิติห้องประชุมส่วนกลาง
                </h2>
                <p class="text-gray-500 mt-1">วิเคราะห์ความถี่และปริมาณการใช้งานห้องประชุม</p>
            </div>
            <div class="flex items-center gap-3">
                <Tag :value="`ระดับสิทธิ์: ${currentUserRole.toUpperCase()}`"
                    :severity="currentUserRole === 'superadmin' ? 'danger' : currentUserRole === 'admin' ? 'warn' : 'info'"
                    rounded class="mr-2" />

                <div class="bg-white p-3 rounded-lg shadow-sm border border-gray-100 flex items-end gap-3 flex-wrap">
                    <div class="flex flex-col">
                        <label class="text-xs font-semibold text-gray-500 mb-1">กรองตามช่วงเวลา (เดือน/ปี)</label>
                        <DatePicker v-model="selectedDateRange" selectionMode="range" view="month" dateFormat="MM yy"
                            placeholder="เดือน/ปี - เดือน/ปี" class="w-64" :manualInput="false" showIcon />
                    </div>
                    <Button v-if="selectedDateRange" icon="pi pi-times" severity="secondary" text rounded
                        @click="clearDateFilter" />
                </div>
            </div>
        </div>

        <div class="flex items-center gap-2 mb-4">
            <i class="pi pi-calendar-clock text-teal-400 text-sm"></i>
            <span class="text-sm text-gray-500">กำลังแสดงข้อมูล: </span>
            <span class="text-sm font-bold text-teal-600 bg-teal-50 px-3 py-0.5 rounded-full border border-teal-200">{{
                dateRangeLabel }}</span>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
            <div class="lg:col-span-1 flex flex-col gap-4">
                <Card class="shadow-sm border-t-4 border-teal-500 bg-teal-50/30">
                    <template #content>
                        <div class="flex justify-between items-start">
                            <div>
                                <p class="text-xs text-teal-700 font-bold mb-1 uppercase tracking-wider">
                                    รวมการใช้งานทั้งหมด</p>
                                <h3 class="text-4xl font-black text-teal-700">{{ totalUsage.toLocaleString() }} <span
                                        class="text-lg font-bold">ครั้ง</span></h3>
                            </div>
                            <div
                                class="w-12 h-12 bg-teal-100 rounded-full flex items-center justify-center text-teal-600">
                                <i class="pi pi-check-square text-xl"></i>
                            </div>
                        </div>
                    </template>
                </Card>

                <Card class="shadow-sm border-none flex-1">
                    <template #title>
                        <div class="text-base font-bold text-gray-700 flex justify-between">
                            <span>สถิติรายห้อง</span>
                        </div>
                    </template>
                    <template #content>
                        <DataTable :value="roomStatsList" :loading="isLoading" stripedRows size="small"
                            class="border border-gray-200 rounded-lg overflow-hidden text-sm">
                            <Column header="ห้องประชุม" class="font-semibold text-gray-700">
                                <template #body="sp">{{ sp.data.roomName }}</template>
                            </Column>
                            <Column header="จำนวน (ครั้ง)" class="text-right w-24">
                                <template #body="sp">
                                    <span class="font-bold"
                                        :class="sp.data.usageCount > 0 ? 'text-teal-600' : 'text-gray-400'">
                                        {{ sp.data.usageCount }}
                                    </span>
                                </template>
                            </Column>
                        </DataTable>
                    </template>
                </Card>
            </div>

            <div class="lg:col-span-2 flex flex-col">
                <Card class="shadow-sm border-none flex-1">
                    <template #title>
                        <div class="text-lg font-bold text-gray-700">กราฟเปรียบเทียบการใช้งาน</div>
                    </template>
                    <template #content>
                        <div v-if="isLoading" class="h-[500px] flex items-center justify-center"><i
                                class="pi pi-spin pi-spinner text-4xl text-teal-500"></i></div>
                        <div v-else-if="totalUsage === 0"
                            class="h-[500px] flex flex-col items-center justify-center text-gray-400">
                            <i class="pi pi-chart-bar text-4xl mb-2"></i>
                            <p>ไม่มีข้อมูลการใช้งานในช่วงเวลานี้</p>
                        </div>
                        <div v-else class="h-[500px] relative w-full pt-4">
                            <Chart type="bar" :data="barChartData" :options="barChartOptions" class="h-full w-full" />
                        </div>
                    </template>
                </Card>
            </div>
        </div>
    </div>
</template>

<style scoped>
:deep(.p-datepicker) {
    border: none;
}

:deep(.p-datatable-header-cell) {
    background-color: #f0fdfa !important;
    color: #0f766e !important;
}
</style>